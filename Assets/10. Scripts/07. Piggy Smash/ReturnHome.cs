using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace StudyXR.PiggySmash
{
    /// <summary>
    /// 망치를 바닥에 떨어뜨리거나 놓친 채로 일정 시간이 지나면 제자리로 되돌린다.
    /// pivot(예: Neck)을 지정하면 '제자리'는 pivot 의 위치·방향이고, 복귀할 때 pivot 이 그 자리에 오도록 망치 전체를 옮긴다.
    /// 제자리에 놓인 동안은 Rigidbody 를 고정(kinematic)해 거치대에 걸려 있게 하고, 손에서 놓으면 다시 물리로 떨어진다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ReturnHome : MonoBehaviour
    {
        [Tooltip("위치 기준점 (비우면 이 오브젝트). 망치는 Neck")]
        [SerializeField] private Transform pivot;
        [Tooltip("이 높이 아래로 떨어지면 즉시 복귀 (월드 Y)")]
        [SerializeField] private float fallY = 0.3f;
        [Tooltip("손에서 놓은 뒤 이 시간이 지나면 복귀. 0 이하면 사용 안 함")]
        [SerializeField] private float idleReturnSeconds = 8f;
        [Tooltip("기준점이 제자리에서 이 거리 안이면 '제자리'로 본다")]
        [SerializeField] private float homeRadius = 0.3f;
        [Tooltip("제자리에 있는 동안 고정해 둔다 (거치대에 걸어 두기)")]
        [SerializeField] private bool holdAtHome = true;

        private Rigidbody body;
        private XRGrabInteractable grab;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private float releasedTime = -1f;

        private Transform Pivot => pivot != null ? pivot : transform;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
            homePosition = Pivot.position;
            homeRotation = Pivot.rotation;
        }

        private void OnEnable()
        {
            if (grab != null) grab.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            if (grab != null) grab.selectExited.RemoveListener(OnReleased);
        }

        // XRI 가 놓을 때 잡기 전 상태(고정)로 되돌리므로, 놓은 뒤에는 물리로 떨어지게 한다
        private void OnReleased(SelectExitEventArgs _)
        {
            if (holdAtHome && body != null) body.isKinematic = false;
            releasedTime = -1f;
        }

        private void Update()
        {
            bool held = grab != null && grab.isSelected;
            if (held)
            {
                releasedTime = -1f;
                return;
            }

            if (transform.position.y < fallY)
            {
                GoHome();
                return;
            }

            if (idleReturnSeconds <= 0f) return;
            bool atHome = (Pivot.position - homePosition).sqrMagnitude < homeRadius * homeRadius;
            if (atHome)
            {
                releasedTime = -1f;
                return;
            }

            if (releasedTime < 0f) releasedTime = Time.time;
            if (Time.time - releasedTime >= idleReturnSeconds) GoHome();
        }

        /// <summary>기준점(pivot)을 바꾼다. 망치는 Neck.</summary>
        public void SetPivot(Transform newPivot) => pivot = newPivot;

        /// <summary>기준점이 놓일 제자리(위치·방향)를 바꾼다.</summary>
        public void SetHome(Vector3 position, Quaternion rotation)
        {
            homePosition = position;
            homeRotation = rotation;
            releasedTime = -1f;
        }

        /// <summary>기준점이 제자리에 오도록 망치 전체를 옮긴다.</summary>
        public void GoHome()
        {
            releasedTime = -1f;
            if (body.isKinematic == false)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            var p = Pivot;
            Quaternion pivotLocal = Quaternion.Inverse(transform.rotation) * p.rotation;
            transform.rotation = homeRotation * Quaternion.Inverse(pivotLocal);
            transform.position += homePosition - p.position;

            body.position = transform.position;
            body.rotation = transform.rotation;
            if (holdAtHome) body.isKinematic = true;
        }
    }
}
