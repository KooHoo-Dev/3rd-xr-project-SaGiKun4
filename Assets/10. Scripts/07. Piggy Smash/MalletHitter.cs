using UnityEngine;

namespace StudyXR.PiggySmash
{
    /// <summary>
    /// 망치 헤드의 '타격 판정' 콜라이더. 스윙 거리로 가산 데미지를 준다.
    ///
    /// 1. 출발점: 헤드가 느릴 때(swingStartSpeed 미만)는 출발점을 계속 현재 위치로 갱신한다.
    ///    속도가 오르기 시작하면 그 자리가 출발점으로 고정된다.
    ///    휘두르던 방향이 크게 꺾이면(되돌아오면) 그 지점에서 출발점을 다시 잡는다.
    ///    → 눈앞에서 짧게 휘적이면 스윙 거리가 짧다.
    /// 2. 판정: 헤드 속도가 minHitSpeed 이상이면 콜라이더를 켠다 (짧게 휘적여도 맞기는 한다).
    /// 3. 데미지: 기본 공격력 + 스윙 거리 가산점
    ///    (bonusStartDistance 부터 bonusDistancePerPoint 마다 +1, 최대 maxBonus).
    ///    → 짧게 휘적이면 기본 데미지만, 크게 내려치면 가산점으로 쉽게 캔다.
    ///    한 번 맞히면 출발점을 다시 잡으므로, 다음 타격은 다시 휘둘러야 한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MalletHitter : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("켜고 끌 타격 콜라이더 (비우면 이 오브젝트의 Collider)")]
        [SerializeField] private Collider hitCollider;
        [Tooltip("망치 본체 Rigidbody (비우면 부모에서 찾음)")]
        [SerializeField] private Rigidbody ownerBody;

        [Header("스윙 판정")]
        [Tooltip("이 속도(m/s) 아래에서는 출발점을 현재 위치로 계속 갱신한다")]
        [SerializeField] private float swingStartSpeed = 1.0f;
        [Tooltip("충돌 순간 헤드 속도가 이 값(m/s) 이상이어야 타격으로 인정")]
        [SerializeField] private float minHitSpeed = 1.5f;
        [Tooltip("휘두르는 방향이 출발점→현재 방향과 이 각도(도) 이상 벌어지면 출발점을 다시 잡는다")]
        [Range(30f, 180f)] [SerializeField] private float reverseAngle = 100f;
        [Tooltip("조건을 벗어난 뒤에도 이 시간 동안은 켜 둔다 (프레임 누락 방지)")]
        [SerializeField] private float holdSeconds = 0.05f;

        [Header("스윙 거리 가산 데미지")]
        [Tooltip("출발점에서 이 거리(m) 이상 휘두르면 가산점이 붙기 시작한다 (+1)")]
        [SerializeField] private float bonusStartDistance = 0.2f;
        [Tooltip("bonusStartDistance 이후 이 거리(m)마다 +1")]
        [Min(0.01f)] [SerializeField] private float bonusDistancePerPoint = 0.1f;
        [Tooltip("가산점 최대치")]
        [Min(0)] [SerializeField] private int maxBonus = 9;

        [Header("디버그")]
        [SerializeField] private bool logHits;
        [SerializeField] private bool drawSwingGizmo = true;

        private Rigidbody body;
        private Collider[] ownColliders;
        private Vector3 lastPosition;
        private Vector3 swingStart;
        private float lastValidTime = float.NegativeInfinity;
        private float consumedAt = float.NegativeInfinity;
        private int consumedDamage;

        /// <summary>현재 헤드 속도 (m/s)</summary>
        public float CurrentSpeed { get; private set; }
        /// <summary>출발점에서 현재 위치까지 거리 (m)</summary>
        public float SwingDistance => (transform.position - swingStart).magnitude;
        public bool IsArmed => hitCollider != null && hitCollider.enabled;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            if (hitCollider == null) hitCollider = GetComponent<Collider>();
            if (ownerBody == null && transform.parent != null) ownerBody = transform.parent.GetComponentInParent<Rigidbody>();

            // 망치 자신의 콜라이더(손잡이 등)와는 부딪히지 않게
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
            swingStart = lastPosition;
            lastValidTime = float.NegativeInfinity;
        }

        private void OnDisable()
        {
            SetArmed(false);
        }

        private void FixedUpdate()
        {
            Vector3 position = transform.position;
            Vector3 delta = position - lastPosition;

            // 헤드 속도: 위치 변화량, 또는 본체가 물리로 움직일 때 헤드 지점 속도 중 큰 값
            float deltaSpeed = delta.magnitude / Time.fixedDeltaTime;
            float bodySpeed = 0f;
            if (ownerBody != null && ownerBody.isKinematic == false)
                bodySpeed = ownerBody.GetPointVelocity(position).magnitude;
            CurrentSpeed = Mathf.Max(deltaSpeed, bodySpeed);

            // 1. 출발점 갱신
            if (CurrentSpeed < swingStartSpeed)
            {
                swingStart = position; // 느리면 여기서부터 다시
            }
            else
            {
                Vector3 swing = position - swingStart;
                if (swing.sqrMagnitude > 0.05f * 0.05f && delta.sqrMagnitude > 1e-8f &&
                    Vector3.Angle(delta, swing) > reverseAngle)
                {
                    swingStart = lastPosition; // 되돌아오는 지점에서 새 스윙
                }
            }
            lastPosition = position;

            // 2. 판정
            bool valid = CurrentSpeed >= minHitSpeed;
            if (valid) lastValidTime = Time.time;
            SetArmed(Time.time - lastValidTime <= holdSeconds);
        }

        /// <summary>
        /// 광맥이 맞았을 때 호출한다. 기본 공격력 + 속도 가산점을 돌려주고, 스윙을 새로 시작시킨다.
        /// 같은 물리 프레임에 여러 광맥을 맞히면 모두 같은 데미지를 받는다.
        /// </summary>
        public int ConsumeHit(int baseDamage)
        {
            if (Mathf.Approximately(consumedAt, Time.fixedTime)) return consumedDamage;

            float distance = SwingDistance;
            int bonus = distance >= bonusStartDistance
                ? Mathf.Min(maxBonus, 1 + Mathf.FloorToInt((distance - bonusStartDistance) / bonusDistancePerPoint))
                : 0;
            consumedDamage = Mathf.Max(1, baseDamage) + bonus;
            consumedAt = Time.fixedTime;

            if (logHits)
                Debug.Log($"[MalletHitter] 타격 — 스윙 거리 {distance:F2} m, 속도 {CurrentSpeed:F2} m/s, 데미지 {baseDamage} + {bonus}", this);

            // 한 번 맞혔으면 다시 휘둘러야 한다
            swingStart = transform.position;
            lastValidTime = float.NegativeInfinity;
            return consumedDamage;
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

        private void OnDrawGizmos()
        {
            if (drawSwingGizmo == false || Application.isPlaying == false) return;
            Gizmos.color = IsArmed ? Color.red : (SwingDistance >= bonusStartDistance ? Color.yellow : Color.gray);
            Gizmos.DrawLine(swingStart, transform.position);
            Gizmos.DrawWireSphere(swingStart, 0.02f);
        }
    }
}
