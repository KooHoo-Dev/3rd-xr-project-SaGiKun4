using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StudyXR.PiggySmash
{
    /// <summary>
    /// 시작 패널 안의 망치 상점 (선택 사항). &lt; / &gt; 로 망치를 고르고, 가운데 버튼으로 코인을 써서 해금한다.
    /// 해금된 망치는 선반의 HammerSlot 에서 잡을 수 있게 된다.
    /// </summary>
    public class HammerShopPanel : MonoBehaviour
    {
        [SerializeField] private HammerProgress progress;
        [SerializeField] private TMP_Text infoText;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionLabel;

        private int index;

        private void Awake()
        {
            if (progress == null) progress = FindAnyObjectByType<HammerProgress>();
            if (prevButton != null) prevButton.onClick.AddListener(() => Move(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Move(1));
            if (actionButton != null) actionButton.onClick.AddListener(OnAction);
            if (progress != null) progress.Changed += Refresh;
        }

        private void OnDestroy()
        {
            if (progress != null) progress.Changed -= Refresh;
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Move(int step)
        {
            if (progress == null || progress.Catalog == null || progress.Catalog.Count == 0) return;
            int count = progress.Catalog.Count;
            index = ((index + step) % count + count) % count;
            Refresh();
        }

        private void OnAction()
        {
            var entry = Current();
            if (entry == null) return;
            progress.Unlock(entry.id);
            Refresh();
        }

        private HammerCatalog.Entry Current()
        {
            return progress != null && progress.Catalog != null ? progress.Catalog.Get(index) : null;
        }

        private void Refresh()
        {
            var entry = Current();
            if (entry == null)
            {
                if (infoText != null) infoText.text = "NO HAMMERS";
                if (actionButton != null) actionButton.interactable = false;
                return;
            }

            int count = progress.Catalog.Count;
            string range = entry.splashRadius > 0f ? $"{Mathf.RoundToInt(entry.splashRadius * 100f)}cm" : "-";
            if (infoText != null)
            {
                infoText.text =
                    $"<size=70%>COINS  {progress.Coins}</size>\n" +
                    $"{entry.displayName}  <size=60%>({index + 1}/{count})</size>\n" +
                    $"<size=70%>POWER  {entry.damage}   |   RANGE  {range}</size>";
            }

            bool unlocked = progress.IsUnlocked(entry.id);
            if (actionLabel != null) actionLabel.text = unlocked ? "UNLOCKED" : $"BUY  {entry.cost}";
            if (actionButton != null) actionButton.interactable = unlocked == false && progress.CanAfford(entry.id);
        }
    }
}
