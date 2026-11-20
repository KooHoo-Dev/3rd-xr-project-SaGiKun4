using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StudyXR.PiggySmash
{
    /// <summary>
    /// 시작 패널 안의 망치 고르기. &lt; / &gt; 로 망치를 넘기고, 가운데 버튼으로
    /// 잠긴 망치는 코인으로 해금(BUY), 해금된 망치는 선택(SELECT)한다.
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
            // 패널이 다시 보일 때는 지금 선택된 망치부터
            if (rack != null) index = rack.Selected;
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
            if (rack.IsUnlocked(index)) rack.Select(index);
            else rack.Unlock(index);
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

            if (infoText != null)
            {
                infoText.text =
                    $"<size=70%>코인  {rack.Coins}</size>\n" +
                    $"{entry.displayName}  <size=60%>({index + 1}/{rack.Count})</size>\n" +
                    $"<size=70%>공격력  {entry.damage}</size>";
            }

            bool unlocked = rack.IsUnlocked(index);
            bool selected = index == rack.Selected;
            string label;
            bool interactable;
            if (selected) { label = "SELECTED"; interactable = false; }
            else if (unlocked) { label = "SELECT"; interactable = true; }
            else { label = $"BUY  {entry.cost}"; interactable = rack.CanAfford(index); }

            if (actionLabel != null) actionLabel.text = label;
            if (actionButton != null) actionButton.interactable = interactable && rack.CanChange;
        }
    }
}
