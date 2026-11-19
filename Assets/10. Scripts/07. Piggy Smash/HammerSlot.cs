using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace StudyXR.PiggySmash
{
    /// <summary>
    /// 선반 위 망치 자리 하나. 이 오브젝트의 위치·회전에 카탈로그의 망치 프리팹을 만든다.
    /// - 해금 전: 망치는 보이지만 잡을 수 없다 (XRGrabInteractable 비활성).
    /// - 해금 후: 잡을 수 있고, 떨어뜨리면 ReturnHome 이 이 자리로 되돌린다.
    /// 망치가 이미 자식으로 놓여 있으면 새로 만들지 않고 그것을 쓴다.
    /// </summary>
    public class HammerSlot : MonoBehaviour
    {
        [SerializeField] private HammerProgress progress;
        [Tooltip("HammerCatalog 의 id (예: HammerShort.002)")]
        [SerializeField] private string hammerId = "";
        [Tooltip("잠긴 망치를 보여줄지. 끄면 해금 전에는 숨긴다")]
        [SerializeField] private bool showWhenLocked = true;

        private GameObject hammer;
        private XRGrabInteractable grab;
        private Rigidbody body;

        public string HammerId => hammerId;
        public GameObject Hammer => hammer;
        public bool IsUnlocked => progress != null && progress.IsUnlocked(hammerId);

        private void Awake()
        {
            if (progress == null) progress = FindAnyObjectByType<HammerProgress>();
            if (progress != null) progress.Changed += Refresh;
        }

        private void OnDestroy()
        {
            if (progress != null) progress.Changed -= Refresh;
        }

        private void Start()
        {
            var entry = progress != null && progress.Catalog != null ? progress.Catalog.Find(hammerId) : null;
            if (entry == null)
            {
                Debug.LogWarning($"[HammerSlot] 카탈로그에 '{hammerId}' 가 없습니다.", this);
                return;
            }

            var existing = GetComponentInChildren<HammerStats>(true);
            if (existing != null) hammer = existing.gameObject;
            else if (entry.prefab != null)
            {
                hammer = Instantiate(entry.prefab, transform.position, transform.rotation);
                hammer.name = entry.prefab.name;
            }
            if (hammer == null) return;

            // 슬롯 자식이면 놓았을 때 부모를 따라 움직이지 않도록 분리
            if (hammer.transform.parent == transform) hammer.transform.SetParent(null, true);

            var stats = hammer.GetComponent<HammerStats>();
            if (stats == null) stats = hammer.AddComponent<HammerStats>();
            stats.Setup(entry.id, entry.damage, entry.splashRadius); // 카탈로그 값이 항상 우선

            grab = hammer.GetComponent<XRGrabInteractable>();
            body = hammer.GetComponent<Rigidbody>();
            Refresh();
        }

        private void Refresh()
        {
            if (hammer == null) return;
            bool unlocked = IsUnlocked;
            if (grab != null) grab.enabled = unlocked;
            // 잠긴 망치는 선반에 고정
            if (body != null) body.isKinematic = unlocked == false;
            hammer.SetActive(unlocked || showWhenLocked);
        }
    }
}
