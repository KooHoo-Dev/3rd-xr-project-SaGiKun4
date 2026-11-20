using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace StudyXR.Blacksmith
{
    /// <summary>
    /// 캔 광물 보관함 (브론즈 · 실버 · 골드 개수, PlayerPrefs 에 저장).
    /// 라운드에서 캔 광물이 쌓이고, 거래소(OreExchange) 의뢰에 납품하면 줄어든다.
    /// Ore Board(3D 텍스트)에 보유량을 표시한다.
    /// </summary>
    public class OreInventory : MonoBehaviour
    {
        // 광물 이름(OreSpawner 의 종류 이름)과 표시 이름. 순서 = 보드 표시 순서
        public static readonly string[] Ores = { "Bronze", "Silver", "Gold" };
        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { "Bronze", "브론즈" }, { "Silver", "실버" }, { "Gold", "골드" },
        };

        [Tooltip("보유량을 표시할 텍스트 (Ore Board)")]
        [SerializeField] private TMP_Text board;
        [SerializeField] private string boardTitle = "광물 보유량";

        private const string KeyPrefix = "Blacksmith.Ore.";
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>();

        /// <summary>보유량이 바뀔 때</summary>
        public event Action Changed;

        public static string Label(string ore) => Labels.TryGetValue(ore, out var l) ? l : ore;

        private void Awake()
        {
            foreach (var ore in Ores) counts[ore] = PlayerPrefs.GetInt(KeyPrefix + ore, 0);
            RefreshBoard();
        }

        public int Get(string ore) => counts.TryGetValue(ore, out int n) ? n : 0;

        public void Add(string ore, int amount = 1)
        {
            if (amount <= 0) return;
            Set(ore, Get(ore) + amount);
        }

        /// <summary>충분하면 빼고 true, 모자라면 그대로 두고 false</summary>
        public bool TryRemove(string ore, int amount)
        {
            if (amount <= 0 || Get(ore) < amount) return false;
            Set(ore, Get(ore) - amount);
            return true;
        }

        private void Set(string ore, int value)
        {
            counts[ore] = Mathf.Max(0, value);
            PlayerPrefs.SetInt(KeyPrefix + ore, counts[ore]);
            PlayerPrefs.Save();
            RefreshBoard();
            Changed?.Invoke();
        }

        private void RefreshBoard()
        {
            if (board == null) return;
            var text = boardTitle;
            foreach (var ore in Ores) text += $"\n<size=80%>{Label(ore)}  {Get(ore)}</size>";
            board.text = text;
        }

        [ContextMenu("Reset Ores (광물 초기화)")]
        private void ResetOres()
        {
            foreach (var ore in Ores) PlayerPrefs.DeleteKey(KeyPrefix + ore);
            foreach (var ore in Ores) counts[ore] = 0;
            RefreshBoard();
            Changed?.Invoke();
        }

        [ContextMenu("Add 10 Each (테스트)")]
        private void AddTest()
        {
            foreach (var ore in Ores) Add(ore, 10);
        }
    }
}
