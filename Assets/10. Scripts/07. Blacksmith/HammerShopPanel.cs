using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StudyXR.Blacksmith
{
    /// <summary>
    /// 망치 선반 위의 상점 UI. &lt; / &gt; 로 망치를 넘기고, 가운데 버튼으로 코인을 써서 해금(BUY)한다.
    /// 선택은 여기서 하지 않는다 — 해금된 망치를 직접 스탠드에 올려야 선택된다.
    /// </summary>
    public class HammerShopPanel : MonoBehaviour
    {
        [SerializeField] private HammerRack rack;
        [SerializeField] private TMP_Text infoText;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionLabel;

        private int index;

        private void Awake()
        {
            if (rack == null) rack = FindAnyObjectByType<HammerRack>();
            if (prevButton != null) prevButton.onClick.AddListener(() => Move(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Move(1));
            if (actionButton != null) actionButton.onClick.AddListener(OnAction);
            if (rack != null) rack.Changed += Refresh;
        }

        private void OnDestroy()
        {
            if (rack != null) rack.Changed -= Refresh;
        }

        private void OnEnable()
        {
            // 처음에는 아직 해금하지 않은 첫 망치부터 보여준다
            if (rack != null)
            {
                index = 0;
                for (int i = 0; i < rack.Count; ++i)
                    if (rack.IsUnlocked(i) == false) { index = i; break; }
            }
            Refresh();
        }

        private void Move(int step)
        {
            if (rack == null || rack.Count == 0) return;
            index = ((index + step) % rack.Count + rack.Count) % rack.Count;
            Refresh();
        }

        private void OnAction()
        {
            if (rack == null) return;
            rack.Unlock(index);
            Refresh();
        }

        private void Refresh()
        {
            var entry = rack != null ? rack.Get(index) : null;
            if (entry == null)
            {
                if (infoText != null) infoText.text = "망치 없음";
                if (actionButton != null) actionButton.interactable = false;
                return;
            }

            bool unlocked = rack.IsUnlocked(index);
            if (infoText != null)
            {
                // 구매할 망치의 이름과 간략한 스펙 (코인은 거래소 상단에 따로 표시)
                infoText.text =
                    $"{entry.displayName}  <size=60%>({index + 1}/{rack.Count})</size>\n" +
                    $"<size=70%>공격력  {entry.damage}   |   {(unlocked ? "보유 중" : $"가격  {entry.cost} 코인")}</size>";
            }
            if (actionLabel != null) actionLabel.text = unlocked ? "OWNED" : $"BUY  {entry.cost}";
            if (actionButton != null) actionButton.interactable = unlocked == false && rack.CanAfford(index);
        }
    }
}
