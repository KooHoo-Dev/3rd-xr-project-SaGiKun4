using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace StudyXR.Blacksmith
{
    /// <summary>
    /// 망치 선반 (상점). 코인 · 해금 · 선택을 관리한다 (PlayerPrefs 에 저장).
    /// - 잠긴 망치: 검정 실루엣, 잡을 수 없음. 선반 위 상점 UI 에서 코인으로 해금한다.
    /// - 해금된 망치: 원래 색, 잡을 수 있음. 잡을 수 있는 대상이 된 망치 하나만 껍데기 외곽선으로 강조한다.
    /// - 선택은 플레이어가 직접 한다:
    ///     망치를 스탠드(anchor)에 놓으면 선택 — 스탠드에는 망치 하나만 올릴 수 있다.
    ///     스탠드의 망치를 선반에 가져다 놓으면 선택 해제.
    /// - 선택된 망치로만 광석을 캘 수 있다 (HammerStats.CanHit).
    /// - 망치 위치는 모두 Neck 기준이다 (선반 자리 = 처음 Neck 위치, 스탠드 자리 = anchor).
    /// </summary>
    public class HammerRack : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public string displayName = "";
            public GameObject hammer;
            [Min(0)] public int cost;
            [Tooltip("한 번 맞힐 때 깎는 기본 타수")]
            [Min(1)] public int damage = 1;

            [NonSerialized] public Transform neck;
            [NonSerialized] public Vector3 slotPosition;   // 선반 자리 (Neck 월드 위치)
            [NonSerialized] public Quaternion slotRotation;
            [NonSerialized] public XRGrabInteractable grab;
            [NonSerialized] public Rigidbody body;
            [NonSerialized] public ReturnHome home;
            [NonSerialized] public HammerVisual visual;
            [NonSerialized] public HammerStats stats;
        }

        [SerializeField] private BlacksmithGame game;
        [Tooltip("스탠드 위 망치 자리 (선택한 망치의 Neck 이 놓이는 곳)")]
        [SerializeField] private Transform anchor;
        [SerializeField] private string neckName = "Neck";
        [SerializeField] private List<Entry> hammers = new List<Entry>();
        [Tooltip("라운드 점수 1점당 코인")]
        [Min(0f)] [SerializeField] private float coinsPerPoint = 1f;

        [Header("놓기 판정")]
        [Tooltip("망치를 놓았을 때 Neck 이 anchor 에서 이 거리(m) 안이면 스탠드에 올린다")]
        [SerializeField] private float standRadius = 0.3f;
        [Tooltip("선반 판정 범위 (비우면 이 오브젝트의 Collider)")]
        [SerializeField] private Collider rackArea;
        [Tooltip("선반 범위를 이만큼(m) 넓혀서 판정")]
        [SerializeField] private float rackMargin = 0.2f;

        [Header("표시")]
        [Tooltip("잠긴 망치에 씌울 머티리얼 (검정)")]
        [SerializeField] private Material lockedMaterial;
        [Tooltip("잡을 수 있는 망치에 덧붙일 외곽선 머티리얼 (Blacksmith/HammerHullOutline)")]
        [SerializeField] private Material outlineMaterial;

        private const string CoinsKey = "Blacksmith.Coins";
        private const string SelectedKey = "Blacksmith.Selected";
        private const string UnlockPrefix = "Blacksmith.Unlocked.";

        private readonly List<(XRGrabInteractable grab, UnityEngine.Events.UnityAction<SelectExitEventArgs> action)> releaseHandlers =
            new List<(XRGrabInteractable, UnityEngine.Events.UnityAction<SelectExitEventArgs>)>();
        private readonly Dictionary<Entry, float> hoverStart = new Dictionary<Entry, float>();

        public IReadOnlyList<Entry> Hammers => hammers;
        public int Count => hammers.Count;
        public int Coins { get; private set; }
        /// <summary>스탠드에 올려진 망치 번호. 없으면 -1</summary>
        public int Selected { get; private set; } = -1;
        public bool HasSelection => Selected >= 0;
        public int LastEarned { get; private set; }

        /// <summary>코인 · 해금 · 선택이 바뀔 때</summary>
        public event Action Changed;

        private void Awake()
        {
            if (game == null) game = FindAnyObjectByType<BlacksmithGame>();
            if (rackArea == null) rackArea = GetComponent<Collider>();
            Coins = PlayerPrefs.GetInt(CoinsKey, 0);

            foreach (var e in hammers)
            {
                if (e.hammer == null) continue;
                e.neck = FindChild(e.hammer.transform, neckName);
                var pivot = e.neck != null ? e.neck : e.hammer.transform;
                e.slotPosition = pivot.position;
                e.slotRotation = pivot.rotation;
                e.grab = e.hammer.GetComponent<XRGrabInteractable>();
                e.body = e.hammer.GetComponent<Rigidbody>();
                e.home = e.hammer.GetComponent<ReturnHome>();
                if (e.home != null) e.home.SetPivot(e.neck);

                var stats = e.hammer.GetComponent<HammerStats>();
                if (stats == null) stats = e.hammer.AddComponent<HammerStats>();
                stats.SetDamage(e.damage);
                e.stats = stats;

                e.visual = e.hammer.GetComponent<HammerVisual>();
                if (e.visual == null) e.visual = e.hammer.AddComponent<HammerVisual>();
                e.visual.Init(lockedMaterial, outlineMaterial);
            }

            // 처음 실행이면 1번 망치가 스탠드에 올라가 있다
            Selected = PlayerPrefs.GetInt(SelectedKey, 0);
            if (Selected >= hammers.Count || (Selected >= 0 && IsUnlocked(Selected) == false)) Selected = -1;

            if (game != null) game.RoundEnded += OnRoundEnded;

            // 스탠드(XRI StickStand)의 소켓은 손잡이 Attach 기준으로 다시 끼워 넣으므로 끈다. 위치는 Neck 기준으로만 정한다.
            if (anchor != null)
                foreach (var socket in anchor.GetComponentsInParent<XRSocketInteractor>(true))
                    socket.enabled = false;
        }

        private void OnDestroy()
        {
            if (game != null) game.RoundEnded -= OnRoundEnded;
            foreach (var h in releaseHandlers)
                if (h.grab != null) h.grab.selectExited.RemoveListener(h.action);
        }

        private void Start()
        {
            for (int i = 0; i < hammers.Count; ++i)
            {
                var e = hammers[i];
                if (e.hammer == null) continue;
                int index = i;
                if (e.grab != null)
                {
                    UnityEngine.Events.UnityAction<SelectExitEventArgs> action = _ => OnReleased(index);
                    e.grab.selectExited.AddListener(action);
                    releaseHandlers.Add((e.grab, action));
                }
                if (i == Selected) PutOnStand(i);
                else PutOnSlot(i);
                ApplyLock(i);
            }
            RefreshCanHit();
            Changed?.Invoke();
        }

        private void Update()
        {
            UpdateHighlight();
        }

        // ───────────────────────── 해금 ─────────────────────────

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

        /// <summary>코인을 써서 해금한다. 해금된 망치는 선반 자리에서 잡을 수 있게 된다.</summary>
        public bool Unlock(int index)
        {
            if (IsUnlocked(index)) return true;
            var e = Get(index);
            if (e == null || Coins < e.cost) return false;
            Coins -= e.cost;
            PlayerPrefs.SetInt(CoinsKey, Coins);
            PlayerPrefs.SetInt(UnlockPrefix + index, 1);
            PlayerPrefs.Save();
            ApplyLock(index);
            Changed?.Invoke();
            return true;
        }

        private void ApplyLock(int index)
        {
            var e = Get(index);
            if (e == null || e.hammer == null) return;
            bool unlocked = IsUnlocked(index);
            if (e.grab != null) e.grab.enabled = unlocked;
            if (e.home != null) e.home.enabled = unlocked;
            if (unlocked == false && e.body != null) e.body.isKinematic = true; // 선반에 고정
            e.visual.SetLocked(unlocked == false);
        }

        // ───────────────────────── 놓기 / 선택 ─────────────────────────

        private void OnReleased(int index)
        {
            var e = Get(index);
            if (e == null || e.hammer == null) return;
            Vector3 neck = (e.neck != null ? e.neck : e.hammer.transform).position;

            if (anchor != null && (neck - anchor.position).sqrMagnitude <= standRadius * standRadius)
            {
                if (Selected < 0 || Selected == index) Select(index);
                else if (e.home != null) e.home.GoHome(); // 스탠드에는 망치 하나만 — 원래 자리로 돌려보낸다
                return;
            }

            if (IsInRack(neck))
            {
                if (Selected == index) Deselect();
                else PutOnSlot(index);
            }
            // 그 밖의 곳: 그대로 두고, ReturnHome 이 일정 시간 뒤 제자리(선반 또는 스탠드)로 돌린다
        }

        private bool IsInRack(Vector3 point)
        {
            if (rackArea == null) return false;
            var b = rackArea.bounds;
            b.Expand(rackMargin * 2f);
            return b.Contains(point);
        }

        private void Select(int index)
        {
            Selected = index;
            PutOnStand(index);
            Save();
        }

        private void Deselect()
        {
            int old = Selected;
            Selected = -1;
            PutOnSlot(old);
            Save();
        }

        // 선택된 망치만 광석을 캘 수 있다
        private void RefreshCanHit()
        {
            for (int i = 0; i < hammers.Count; ++i)
                if (hammers[i].stats != null) hammers[i].stats.SetCanHit(i == Selected);
        }

        private void Save()
        {
            RefreshCanHit();
            PlayerPrefs.SetInt(SelectedKey, Selected);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private void PutOnStand(int index)
        {
            var e = Get(index);
            if (e == null || anchor == null) return;
            Place(e, anchor.position, anchor.rotation);
        }

        private void PutOnSlot(int index)
        {
            var e = Get(index);
            if (e == null) return;
            Place(e, e.slotPosition, e.slotRotation);
        }

        // Neck 이 (position, rotation) 에 오도록 놓고, 그곳을 제자리로 기억한다
        private static void Place(Entry e, Vector3 position, Quaternion rotation)
        {
            if (e.hammer == null) return;
            if (e.home != null)
            {
                e.home.SetHome(position, rotation);
                e.home.GoHome();
                return;
            }
            var t = e.hammer.transform;
            var pivot = e.neck != null ? e.neck : t;
            Quaternion local = Quaternion.Inverse(t.rotation) * pivot.rotation;
            t.rotation = rotation * Quaternion.Inverse(local);
            t.position += position - pivot.position;
            if (e.body != null) e.body.isKinematic = true;
        }

        // ───────────────────────── 강조 ─────────────────────────

        // 잡을 수 있는 대상이 된(호버 중이고 아직 잡지 않은) 망치 중 가장 최근에 호버된 하나만 외곽선을 켠다
        private void UpdateHighlight()
        {
            Entry best = null;
            float bestTime = float.NegativeInfinity;
            foreach (var e in hammers)
            {
                if (e.grab == null || e.visual == null) continue;
                bool candidate = e.grab.enabled && e.grab.isHovered && e.grab.isSelected == false;
                if (candidate == false) { hoverStart.Remove(e); continue; }
                if (hoverStart.TryGetValue(e, out float since) == false)
                {
                    since = Time.time;
                    hoverStart[e] = since;
                }
                if (since >= bestTime) { best = e; bestTime = since; }
            }
            foreach (var e in hammers)
                if (e.visual != null) e.visual.SetOutline(e == best);
        }

        // ───────────────────────── 코인 ─────────────────────────

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
            if (Application.isPlaying == false) return;
            Selected = 0;
            for (int i = 0; i < hammers.Count; ++i)
            {
                if (i == 0) PutOnStand(i); else PutOnSlot(i);
                ApplyLock(i);
            }
            RefreshCanHit();
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
