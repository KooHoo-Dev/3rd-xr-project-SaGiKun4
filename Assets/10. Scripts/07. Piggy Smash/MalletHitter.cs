using UnityEngine;

namespace StudyXR.PiggySmash
{
    /// <summary>
    /// 망치 헤드의 '타격 판정' 콜라이더.
    /// Kinematic Rigidbody 를 가진 별도 오브젝트에 붙이고, 헤드의 운동량(질량 × 속도)이
    /// 기준 이상일 때만 콜라이더를 켠다. 이 오브젝트에 Destroyer 태그를 달면
    /// Breakable 은 세게 휘두른 헤드에만 부서진다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MalletHitter : MonoBehaviour
    {
        public enum Measure { Momentum, Speed }

        [Header("연결")]
        [Tooltip("켜고 끌 타격 콜라이더 (비우면 이 오브젝트의 Collider)")]
        [SerializeField] private Collider hitCollider;
        [Tooltip("망치 본체 Rigidbody (비우면 부모에서 찾음). 질량과 속도 계산에 사용")]
        [SerializeField] private Rigidbody ownerBody;

        [Header("판정 기준")]
        [SerializeField] private Measure measure = Measure.Momentum;
        [Tooltip("운동량 기준 (kg·m/s). 망치 질량 0.9kg 기준 1.8 ≈ 헤드 속도 2m/s")]
        [SerializeField] private float minMomentum = 1.8f;
        [Tooltip("속도 기준 (m/s)")]
        [SerializeField] private float minSpeed = 2.0f;
        [Tooltip("기준 아래로 떨어진 뒤에도 이 시간 동안은 켜 둔다 (깜빡임·프레임 누락 방지)")]
        [SerializeField] private float holdSeconds = 0.08f;

        [Header("디버그")]
        [SerializeField] private bool logHits;

        private Rigidbody body;
        private Collider[] ownColliders;
        private Vector3 lastPosition;
        private float lastFastTime = float.NegativeInfinity;

        /// <summary>현재 헤드 속도 (m/s)</summary>
        public float CurrentSpeed { get; private set; }
        /// <summary>현재 운동량 (kg·m/s)</summary>
        public float CurrentMomentum => CurrentSpeed * (ownerBody != null ? ownerBody.mass : 1f);
        public bool IsArmed => hitCollider != null && hitCollider.enabled;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            if (hitCollider == null) hitCollider = GetComponent<Collider>();
            if (ownerBody == null && transform.parent != null) ownerBody = transform.parent.GetComponentInParent<Rigidbody>();

            // 망치 자신의 콜라이더(손잡이·헤드)와는 부딪히지 않게
            var list = new System.Collections.Generic.List<Collider>();
            if (ownerBody != null)
                foreach (var c in ownerBody.GetComponentsInChildren<Collider>(true))
                    if (c != hitCollider) list.Add(c);
            ownColliders = list.ToArray();

            SetArmed(false);
        }

        private void OnEnable()
        {
            lastPosition = transform.position;
            lastFastTime = float.NegativeInfinity;
        }

        private void OnDisable()
        {
            SetArmed(false);
        }

        private void FixedUpdate()
        {
            // 1) 위치 변화량으로 구한 속도 (손으로 직접 움직이는 경우에도 동작)
            Vector3 position = transform.position;
            float deltaSpeed = (position - lastPosition).magnitude / Time.fixedDeltaTime;
            lastPosition = position;

            // 2) 본체 Rigidbody 가 물리로 움직일 때 헤드 지점의 속도
            float bodySpeed = 0f;
            if (ownerBody != null && ownerBody.isKinematic == false)
                bodySpeed = ownerBody.GetPointVelocity(position).magnitude;

            CurrentSpeed = Mathf.Max(deltaSpeed, bodySpeed);

            bool fast = measure == Measure.Momentum
                ? CurrentMomentum >= minMomentum
                : CurrentSpeed >= minSpeed;
            if (fast) lastFastTime = Time.time;

            SetArmed(Time.time - lastFastTime <= holdSeconds);
        }

        private void SetArmed(bool on)
        {
            if (hitCollider == null || hitCollider.enabled == on) return;
            hitCollider.enabled = on;

            // 콜라이더를 껐다 켜면 IgnoreCollision 상태가 풀릴 수 있어 켤 때마다 다시 적용
            if (on && ownColliders != null)
                foreach (var c in ownColliders)
                    if (c != null) Physics.IgnoreCollision(hitCollider, c, true);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (logHits)
                Debug.Log($"[MalletHitter] {collision.gameObject.name} 타격 — 속도 {CurrentSpeed:F2} m/s, 운동량 {CurrentMomentum:F2}", this);
        }
    }
}
