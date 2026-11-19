using System;
using UnityEngine;

namespace StudyXR.PiggySmash
{
    /// <summary>
    /// 코인과 망치 해금 상태 (PlayerPrefs 에 저장).
    /// - 라운드가 끝나면 그 라운드 점수만큼 코인이 쌓인다.
    /// - Unlock(id) 로 코인을 써서 망치를 해금한다. 해금된 망치는 HammerSlot 에서 잡을 수 있게 된다.
    /// 씬에 하나만 두고, 상점 UI · 선반 버튼 등은 이 컴포넌트의 함수를 호출하면 된다.
    /// </summary>
    public class HammerProgress : MonoBehaviour
    {
        [SerializeField] private HammerCatalog catalog;
        [SerializeField] private PiggySmashGame game;
        [Tooltip("라운드 점수 1점당 코인")]
        [Min(0f)] [SerializeField] private float coinsPerPoint = 1f;

        private const string CoinsKey = "PiggySmash.Coins";
        private const string UnlockPrefix = "PiggySmash.Unlocked.";

        public HammerCatalog Catalog => catalog;
        public int Coins { get; private set; }
        public int LastEarned { get; private set; }

        /// <summary>코인 또는 해금 상태가 바뀔 때</summary>
        public event Action Changed;

        private void Awake()
        {
            if (game == null) game = FindAnyObjectByType<PiggySmashGame>();
            Coins = PlayerPrefs.GetInt(CoinsKey, 0);
            if (game != null) game.RoundEnded += OnRoundEnded;
        }

        private void OnDestroy()
        {
            if (game != null) game.RoundEnded -= OnRoundEnded;
        }

        public bool IsUnlocked(string id)
        {
            var entry = catalog != null ? catalog.Find(id) : null;
            if (entry == null) return false;
            return entry.cost <= 0 || PlayerPrefs.GetInt(UnlockPrefix + id, 0) == 1;
        }

        public bool CanAfford(string id)
        {
            var entry = catalog != null ? catalog.Find(id) : null;
            return entry != null && Coins >= entry.cost;
        }

        /// <summary>코인을 써서 해금한다. 이미 해금돼 있으면 true.</summary>
        public bool Unlock(string id)
        {
            if (IsUnlocked(id)) return true;
            var entry = catalog != null ? catalog.Find(id) : null;
            if (entry == null || Coins < entry.cost) return false;

            Coins -= entry.cost;
            PlayerPrefs.SetInt(CoinsKey, Coins);
            PlayerPrefs.SetInt(UnlockPrefix + id, 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        private void OnRoundEnded()
        {
            LastEarned = Mathf.RoundToInt(game.Score * coinsPerPoint);
            Coins += LastEarned;
            PlayerPrefs.SetInt(CoinsKey, Coins);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        [ContextMenu("Reset Progress (코인·해금 초기화)")]
        private void ResetProgress()
        {
            PlayerPrefs.DeleteKey(CoinsKey);
            if (catalog != null)
                foreach (var e in catalog.Hammers) PlayerPrefs.DeleteKey(UnlockPrefix + e.id);
            PlayerPrefs.Save();
            Coins = 0;
            Changed?.Invoke();
        }

        [ContextMenu("Add 1000 Coins (테스트)")]
        private void AddTestCoins()
        {
            Coins += 1000;
            PlayerPrefs.SetInt(CoinsKey, Coins);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
