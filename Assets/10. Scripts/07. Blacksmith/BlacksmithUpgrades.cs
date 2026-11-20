using System;
using UnityEngine;

namespace StudyXR.Blacksmith
{
    /// <summary>
    /// 광물 장부(OreLedgerPanel)에서 올리는 영구 강화 레벨 (PlayerPrefs 에 저장).
    /// - 추가 계약 : 한 번에 생성되는 광물 수 +1 / 레벨 (OreSpawner)
    /// - 무른 광물 : 플레이어 기본 데미지 +1 / 레벨 (OreVein)
    /// - 빠른 연락 : 광물 생성 주기 단축 (OreSpawner)
    /// </summary>
    public static class BlacksmithUpgrades
    {
        public enum Kind { Contract = 0, SoftOre = 1, FastContact = 2 }

        private const string KeyPrefix = "Blacksmith.Upgrade.";
        private static readonly int[] levels = new int[3];
        private static bool loaded;

        /// <summary>레벨이 바뀔 때</summary>
        public static event Action Changed;

        /// <summary>빠른 연락 레벨당 생성 주기 배율 (0.85 → 레벨마다 15% 빨라짐)</summary>
        public const float IntervalStep = 0.85f;

        public static int Level(Kind kind)
        {
            Load();
            return levels[(int)kind];
        }

        public static void SetLevel(Kind kind, int level)
        {
            Load();
            levels[(int)kind] = Mathf.Max(0, level);
            PlayerPrefs.SetInt(KeyPrefix + kind, levels[(int)kind]);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>한 번에 더 생성되는 광물 수</summary>
        public static int ExtraPerWave => Level(Kind.Contract);

        /// <summary>망치 한 번 맞힐 때 더해지는 데미지</summary>
        public static int DamageBonus => Level(Kind.SoftOre);

        /// <summary>생성 주기에 곱하는 배율 (1 = 기본)</summary>
        public static float IntervalMultiplier => Mathf.Pow(IntervalStep, Level(Kind.FastContact));

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            foreach (Kind k in Enum.GetValues(typeof(Kind)))
                levels[(int)k] = PlayerPrefs.GetInt(KeyPrefix + k, 0);
        }

        // 도메인 리로드를 꺼도 플레이할 때마다 다시 읽도록
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            loaded = false;
            Changed = null;
        }
    }
}
