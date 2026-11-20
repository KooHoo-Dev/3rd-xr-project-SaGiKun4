using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StudyXR.Blacksmith
{
    /// <summary>
    /// 광물 장부 (Ore Panel). 코인을 써서 광물 관련 강화를 체결한다.
    /// - 추가 계약 : 한 번에 생성되는 광물 +1
    /// - 무른 광물 : 플레이어 기본 데미지 +1
    /// - 빠른 연락 : 광물 생성 주기 단축
    /// 레벨은 BlacksmithUpgrades 가 PlayerPrefs 에 저장한다. 코인은 망치 선반(HammerRack)과 공유.
    /// </summary>
    public class OreLedgerPanel : MonoBehaviour
    {
        [Serializable]
        public class Row
        {
            public BlacksmithUpgrades.Kind kind;
            public string title = "추가 계약";
            public string description = "광물의 생성량을 늘립니다.";
            [Tooltip("Lv.0 → 1 가격")]
            [Min(0)] public int baseCost = 60;
            [Tooltip("레벨마다 가격 배율")]
            [Min(1f)] public float costGrowth = 1.6f;
            [Tooltip("최대 레벨 (0 이면 제한 없음)")]
            [Min(0)] public int maxLevel = 10;
            public TMP_Text text;
            public Button button;
        }

        [SerializeField] private HammerRack rack;
        [Tooltip("보유 코인 표시")]
        [SerializeField] private TMP_Text coinsText;

        [SerializeField] private Row[] rows =
        {
            new Row { kind = BlacksmithUpgrades.Kind.Contract, title = "추가 계약", description = "광물의 생성량을 늘립니다.", baseCost = 60, costGrowth = 1.6f, maxLevel = 10 },
            new Row { kind = BlacksmithUpgrades.Kind.SoftOre, title = "무른 광물", description = "광물이 더욱 캐기 쉬워집니다.", baseCost = 100, costGrowth = 1.8f, maxLevel = 5 },
            new Row { kind = BlacksmithUpgrades.Kind.FastContact, title = "빠른 연락", description = "광물의 생성 주기가 빨라집니다.", baseCost = 80, costGrowth = 1.6f, maxLevel = 8 },
        };

        [Header("체결 버튼 색")]
        [SerializeField] private Color availableColor = new Color(0.784f, 0.784f, 0.784f, 0.502f);
        [SerializeField] private Color unavailableColor = new Color(0.85f, 0.25f, 0.25f, 0.55f);

        private void Awake()
        {
            if (rack == null) rack = FindAnyObjectByType<HammerRack>();
            for (int i = 0; i < rows.Length; ++i)
            {
                int index = i;
                if (rows[i].button == null) continue;
                rows[i].button.onClick.AddListener(() => Sign(index));
                ApplyButtonColors(rows[i].button);
            }
            if (rack != null) rack.Changed += Refresh;
            BlacksmithUpgrades.Changed += Refresh;
        }

        private void OnDestroy()
        {
            if (rack != null) rack.Changed -= Refresh;
            BlacksmithUpgrades.Changed -= Refresh;
        }

        private void OnEnable() => Refresh();

        public int Cost(Row row)
        {
            int level = BlacksmithUpgrades.Level(row.kind);
            float raw = row.baseCost * Mathf.Pow(row.costGrowth, level);
            return Mathf.Max(1, Mathf.RoundToInt(raw / 5f) * 5); // 5 단위로
        }

        private static bool IsMax(Row row) => row.maxLevel > 0 && BlacksmithUpgrades.Level(row.kind) >= row.maxLevel;

        /// <summary>i 번 강화 체결: 코인이 충분하면 빼고 레벨을 올린다.</summary>
        public void Sign(int i)
        {
            var row = rows[i];
            if (rack == null || IsMax(row)) return;
            if (rack.TrySpendCoins(Cost(row)) == false) return;
            BlacksmithUpgrades.SetLevel(row.kind, BlacksmithUpgrades.Level(row.kind) + 1); // Changed → Refresh
        }

        private static string Effect(BlacksmithUpgrades.Kind kind, int level)
        {
            switch (kind)
            {
                case BlacksmithUpgrades.Kind.Contract: return $"생성량 +{level}";
                case BlacksmithUpgrades.Kind.SoftOre: return $"데미지 +{level}";
                default:
                    int percent = Mathf.RoundToInt((1f - Mathf.Pow(BlacksmithUpgrades.IntervalStep, level)) * 100f);
                    return $"주기 -{percent}%";
            }
        }

        private void Refresh()
        {
            int coins = rack != null ? rack.Coins : 0;
            if (coinsText != null) coinsText.text = $"<size=70%>보유 코인</size>  {coins}";

            foreach (var row in rows)
            {
                int level = BlacksmithUpgrades.Level(row.kind);
                bool max = IsMax(row);
                int cost = Cost(row);
                bool affordable = max == false && coins >= cost;

                if (row.text != null)
                {
                    string effect = level > 0 ? $"  <color=#FFD27A>{Effect(row.kind, level)}</color>" : "";
                    row.text.text =
                        $"{row.title}  <size=60%>Lv.{level}</size>\n" +
                        $"<size=55%><color=#D8D8D8>{row.description}</color>{effect}</size>";
                }
                if (row.button != null)
                {
                    row.button.interactable = affordable;
                    var label = row.button.GetComponentInChildren<TMP_Text>(true);
                    if (label != null)
                        label.text = max ? "MAX" : $"체결\n<size=60%>{cost} 코인</size>";
                }
            }
        }

        private void ApplyButtonColors(Button button)
        {
            var c = button.colors;
            c.normalColor = availableColor;
            c.selectedColor = availableColor;
            c.highlightedColor = new Color(availableColor.r, availableColor.g, availableColor.b, Mathf.Min(1f, availableColor.a + 0.2f));
            c.pressedColor = new Color(availableColor.r * 0.8f, availableColor.g * 0.8f, availableColor.b * 0.8f, Mathf.Min(1f, availableColor.a + 0.2f));
            c.disabledColor = unavailableColor;
            c.colorMultiplier = 1f;
            button.colors = c;
        }

        [ContextMenu("Reset Upgrades (강화 초기화)")]
        private void ResetUpgrades()
        {
            foreach (BlacksmithUpgrades.Kind k in Enum.GetValues(typeof(BlacksmithUpgrades.Kind)))
                BlacksmithUpgrades.SetLevel(k, 0);
        }
    }
}
