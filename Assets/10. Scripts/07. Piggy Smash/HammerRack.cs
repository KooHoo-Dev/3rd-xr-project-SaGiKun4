using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace StudyXR.PiggySmash
{
    /// <summary>
    /// 망치 선반 (WeaponRack). 코인 · 망치 해금 · 선택을 관리한다 (PlayerPrefs 에 저장).
    /// - 1번 망치는 처음부터 해금 + 선택.
    /// - 라운드가 끝나면 점수만큼 코인이 쌓이고, 코인으로 다음 망치를 해금한다.
    /// - 선택한 망치는 Neck 트랜스폼이 거치대 기준점(anchor)에 오도록 옮겨지고, 잡을 수 있게 된다.
    /// - 선택되지 않은 망치는 선반 원래 자리에 고정되며 잡을 수 없다.
    /// </summary>
    public class HammerRack : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public string displayName = "";
            public GameObject hammer;
            [Min(0)] public int cost;
            [Tooltip("한 번 맞힐 때 깎는 타수")]
            [Min(1)] public int damage = 1;

            [NonSerialized] public Transform rackParent;
            [NonSerialized] public Vector3 rackLocalPosition;
            [NonSerialized] public Quaternion rackLocalRotation;
            [NonSerialized] public Transform neck;
        }

        [SerializeField] private PiggySmashGame game;
        [Tooltip("선택한 망치의 Neck 이 놓일 자리 (기존 Mallet Stand 의 망치 거치 위치)")]
        [SerializeField] private Transform anchor;
        [SerializeField] private string neckName = "Neck";
        [SerializeField] private List<Entry> hammers = new List<Entry>();
        [Tooltip("라운드 점수 1점당 코인")]
        [Min(0f)] [SerializeField] private float coinsPerPoint = 1f;

        private const string CoinsKey = "Blacksmith.Coins";
        private const string SelectedKey = "Blacksmith.Selected";
        private const string UnlockPrefix = "Blacksmith.Unlocked.";

        public IReadOnlyList<Entry> Hammers => hammers;
        public int Count => hammers.Count;
        public int Coins { get; private set; }
        public int Selected { get; private set; }
        public int LastEarned { get; private set; }
        public bool CanChange => game == null || game.IsRunning == false;

        /// <summary>코인 · 해금 · 선택이 바뀔 때</summary>
        public event Action Changed;

        private void Awake()
        {
            if (game == null) game = FindAnyObjectByType<PiggySmashGame>();
            Coins = PlayerPrefs.GetInt(CoinsKey, 0);

            foreach (var e in hammers)
            {
                if (e.hammer == null) continue;
                var t = e.hammer.transform;
                e.rackParent = t.parent;
                e.rackLocalPosition = t.localPosition;
                e.rackLocalRotation = t.localRotation;
                e.neck = FindChild(t, neckName);

                var stats = e.hammer.GetComponent<HammerStats>();
                if (stats == null) stats = e.hammer.AddComponent<HammerStats>();
                stats.SetDamage(e.damage);
            }

            Selected = Mathf.Clamp(PlayerPrefs.GetInt(SelectedKey, 0), 0, Mathf.Max(0, hammers.Count - 1));
            if (IsUnlocked(Selected) == false) Selected = 0;

            if (game != null) game.RoundEnded += OnRoundEnded;

            // 거치대(XRI StickStand)의 소켓은 망치를 손잡이 Attach 기준으로 다시 끼워 넣으므로 끈다.
            // 망치 위치는 Neck 기준으로만 정한다.
            if (anchor != null)
                foreach (var socket in anchor.GetComponentsInParent<XRSocketInteractor>(true))
                    socket.enabled = false;
        }

        private void OnDestroy()
        {
            if (game != null) game.RoundEnded -= OnRoundEnded;
        }

        private void Start()
        {
            for (int i = 0; i < hammers.Count; ++i)
                if (i != Selected) PutOnRack(i);
            PutOnStand(Selected);
            Changed?.Invoke();
        }

        public Entry Get(int index) => index >= 0 && index < hammers.Count ? hammers[index] : null;

        public bool IsUnlocked(int index)
        {
            var e = Get(index);
            if (e == null) return false;
            return index == 0 || e.cost <= 0 || PlayerPrefs.GetInt(UnlockPrefix + index, 0) == 1;
        }

        public bool CanAfford(int index)
        {
            var e = Get(index);
            return e != null && Coins >= e.cost;
        }

        /// <summary>코인을 써서 해금한다. 선택은 바꾸지 않는다.</summary>
        public bool Unlock(int index)
        {
            if (IsUnlocked(index)) return true;
            var e = Get(index);
            if (e == null || Coins < e.cost) return false;
            Coins -= e.cost;
            PlayerPrefs.SetInt(CoinsKey, Coins);
            PlayerPrefs.SetInt(UnlockPrefix + index, 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>해금된 망치를 거치대로 옮기고, 이전 망치는 선반으로 돌려놓는다.</summary>
        public bool Select(int index)
        {
            if (CanChange == false || IsUnlocked(index) == false) return false;
            if (index != Selected)
            {
                PutOnRack(Selected);
                Selected = index;
                PutOnStand(Selected);
                PlayerPrefs.SetInt(SelectedKey, Selected);
                PlayerPrefs.Save();
            }
            Changed?.Invoke();
            return true;
        }

        private void PutOnRack(int index)
        {
            var e = Get(index);
            if (e == null || e.hammer == null) return;
            SetGrabbable(e.hammer, false);
            var t = e.hammer.transform;
            t.SetParent(e.rackParent, false);
            t.localPosition = e.rackLocalPosition;
            t.localRotation = e.rackLocalRotation;
        }

        private void PutOnStand(int index)
        {
            var e = Get(index);
            if (e == null || e.hammer == null) return;
            var t = e.hammer.transform;
            t.SetParent(null, true); // 선반을 따라 움직이지 않도록
            SetGrabbable(e.hammer, true);

            // 위치는 Neck 기준 한 곳에서만 정한다: ReturnHome 의 기준점 = Neck, 제자리 = anchor
            var home = e.hammer.GetComponent<ReturnHome>();
            if (home != null && anchor != null)
            {
                home.SetPivot(e.neck);
                home.SetHome(anchor.position, anchor.rotation);
                home.GoHome();
            }
            else if (anchor != null && e.neck != null)
            {
                Quaternion neckLocal = Quaternion.Inverse(t.rotation) * e.neck.rotation;
                t.rotation = anchor.rotation * Quaternion.Inverse(neckLocal);
                t.position += anchor.position - e.neck.position;
            }
        }

        private static void SetGrabbable(GameObject hammer, bool on)
        {
            var grab = hammer.GetComponent<XRGrabInteractable>();
            if (grab != null) grab.enabled = on;
            var body = hammer.GetComponent<Rigidbody>();
            if (body != null)
            {
                if (body.isKinematic == false)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = on == false; // 선반에 있는 망치는 고정
            }
            var home = hammer.GetComponent<ReturnHome>();
            if (home != null) home.enabled = on;
        }

        private void OnRoundEnded()
        {
            LastEarned = Mathf.RoundToInt(game.Score * coinsPerPoint);
            Coins += LastEarned;
            PlayerPrefs.SetInt(CoinsKey, Coins);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (var t in parent.GetComponentsInChildren<Transform>(true))
                if (t != parent && t.name == name) return t;
            return null;
        }

        [ContextMenu("Reset Progress (코인·해금 초기화)")]
        private void ResetProgress()
        {
            PlayerPrefs.DeleteKey(CoinsKey);
            PlayerPrefs.DeleteKey(SelectedKey);
            for (int i = 0; i < hammers.Count; ++i) PlayerPrefs.DeleteKey(UnlockPrefix + i);
            PlayerPrefs.Save();
            Coins = 0;
            if (Application.isPlaying) Select(0);
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
