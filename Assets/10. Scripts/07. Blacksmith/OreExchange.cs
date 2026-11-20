using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace StudyXR.Blacksmith
{
    /// <summary>
    /// 거래소의 납품 의뢰 3칸. 손님 주문처럼 "광물 N개 → 코인" 의뢰가 뜨고, 납품하면 그 칸에 새 의뢰가 들어온다.
    /// - 1번 칸: 브론즈 또는 실버
    /// - 2번 칸: 실버 또는 골드
    /// - 3번 칸: 아무 광물
    /// → 어떤 광물만 잔뜩 가지고 있어도 팔 곳이 생기도록.
    /// 의뢰는 PlayerPrefs 에 저장돼 다시 실행해도 유지된다.
    /// </summary>
    public class OreExchange : MonoBehaviour
    {
        [Serializable]
        public class OrePrice
        {
            public string ore = "Bronze";
            [Tooltip("광물 1개 기본 가격 (코인)")]
            [Min(1)] public int unitPrice = 6;
            [Min(1)] public int minAmount = 1;
            [Min(1)] public int maxAmount = 3;
        }

        [Serializable]
        public class Slot
        {
            [Tooltip("이 칸에 나올 수 있는 광물")]
            public string[] ores = { "Bronze", "Silver" };
            public TMP_Text text;
            public Button button;
            [NonSerialized] public string ore;
            [NonSerialized] public int amount;
            [NonSerialized] public int reward;
        }

        [SerializeField] private OreInventory inventory;
        [SerializeField] private HammerRack rack;
        [Tooltip("보유 코인 표시")]
        [SerializeField] private TMP_Text coinsText;

        [SerializeField] private OrePrice[] prices =
        {
            new OrePrice { ore = "Bronze", unitPrice = 6, minAmount = 3, maxAmount = 6 },
            new OrePrice { ore = "Silver", unitPrice = 20, minAmount = 1, maxAmount = 3 },
            new OrePrice { ore = "Gold", unitPrice = 45, minAmount = 1, maxAmount = 2 },
        };
        [Tooltip("의뢰 보상 = 개수 × 단가 × 이 범위의 랜덤 배율")]
        [SerializeField] private Vector2 rewardMultiplier = new Vector2(0.9f, 1.3f);

        [SerializeField] private Slot[] slots =
        {
            new Slot { ores = new[] { "Bronze", "Silver" } },
            new Slot { ores = new[] { "Silver", "Gold" } },
            new Slot { ores = new[] { "Bronze", "Silver", "Gold" } },
        };

        private const string OrderKey = "Blacksmith.Order.";

        private void Awake()
        {
            if (inventory == null) inventory = FindAnyObjectByType<OreInventory>();
            if (rack == null) rack = FindAnyObjectByType<HammerRack>();

            for (int i = 0; i < slots.Length; ++i)
            {
                int index = i;
                if (slots[i].button != null) slots[i].button.onClick.AddListener(() => Deliver(index));
                if (Load(i) == false) Roll(i);
            }
            if (inventory != null) inventory.Changed += Refresh;
            if (rack != null) rack.Changed += Refresh;
        }

        private void OnDestroy()
        {
            if (inventory != null) inventory.Changed -= Refresh;
            if (rack != null) rack.Changed -= Refresh;
        }

        private void OnEnable() => Refresh();

        /// <summary>i 번 칸 의뢰 납품: 광물이 충분하면 빼고 코인을 받은 뒤 새 의뢰를 받는다.</summary>
        public void Deliver(int i)
        {
            var slot = slots[i];
            if (inventory == null || rack == null) return;
            if (inventory.TryRemove(slot.ore, slot.amount) == false) return;
            rack.AddCoins(slot.reward);
            Roll(i);
            Refresh();
        }

        private void Roll(int i)
        {
            var slot = slots[i];
            var pool = slot.ores != null && slot.ores.Length > 0 ? slot.ores : OreInventory.Ores;
            slot.ore = pool[Random.Range(0, pool.Length)];
            var price = Price(slot.ore);
            slot.amount = Random.Range(price.minAmount, Mathf.Max(price.minAmount, price.maxAmount) + 1);
            float mult = Random.Range(Mathf.Min(rewardMultiplier.x, rewardMultiplier.y), Mathf.Max(rewardMultiplier.x, rewardMultiplier.y));
            slot.reward = Mathf.Max(1, Mathf.RoundToInt(slot.amount * price.unitPrice * mult));
            PlayerPrefs.SetString(OrderKey + i, $"{slot.ore}|{slot.amount}|{slot.reward}");
            PlayerPrefs.Save();
        }

        private bool Load(int i)
        {
            var data = PlayerPrefs.GetString(OrderKey + i, "");
            var parts = data.Split('|');
            if (parts.Length != 3 || int.TryParse(parts[1], out int amount) == false || int.TryParse(parts[2], out int reward) == false)
                return false;
            if (Array.IndexOf(slots[i].ores, parts[0]) < 0) return false; // 칸 규칙이 바뀌었으면 새로
            slots[i].ore = parts[0];
            slots[i].amount = amount;
            slots[i].reward = reward;
            return true;
        }

        private OrePrice Price(string ore)
        {
            foreach (var p in prices) if (p.ore == ore) return p;
            return new OrePrice { ore = ore };
        }

        private void Refresh()
        {
            if (coinsText != null && rack != null) coinsText.text = $"<size=70%>보유 코인</size>  {rack.Coins}";
            for (int i = 0; i < slots.Length; ++i)
            {
                var slot = slots[i];
                int have = inventory != null ? inventory.Get(slot.ore) : 0;
                bool enough = have >= slot.amount;
                if (slot.text != null)
                {
                    string color = enough ? "#FFFFFF" : "#FFB0A0";
                    slot.text.text =
                        $"{OreInventory.Label(slot.ore)} × {slot.amount}  →  {slot.reward} 코인\n" +
                        $"<size=65%><color={color}>보유 {have}개</color></size>";
                }
                if (slot.button != null) slot.button.interactable = enough;
            }
        }

        [ContextMenu("Reroll Orders (의뢰 새로 받기)")]
        private void RerollAll()
        {
            for (int i = 0; i < slots.Length; ++i) Roll(i);
            Refresh();
        }
    }
}
