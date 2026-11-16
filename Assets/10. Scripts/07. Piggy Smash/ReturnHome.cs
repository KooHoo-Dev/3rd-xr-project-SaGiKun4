using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace StudyXR.PiggySmash
{
    /// <summary>
    /// 망치(Mallet)를 바닥에 떨어뜨리거나 놓친 채로 일정 시간이 지나면 처음 자리로 되돌린다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ReturnHome : MonoBehaviour
    {
        [Tooltip("이 높이 아래로 떨어지면 즉시 복귀 (월드 Y)")]
        [SerializeField] private float fallY = 0.3f;
        [Tooltip("손에서 놓은 뒤 이 시간이 지나면 복귀. 0 이하면 사용 안 함")]
        [SerializeField] private float idleReturnSeconds = 8f;
        [Tooltip("시작 위치에서 이 거리 안이면 '제자리'로 본다")]
        [SerializeField] private float homeRadius = 0.3f;

        private Rigidbody body;
        private XRGrabInteractable grab;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private float releasedTime = -1f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
            homePosition = transform.position;
            homeRotation = transform.rotation;
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
            bool atHome = (transform.position - homePosition).sqrMagnitude < homeRadius * homeRadius;
            if (atHome)
            {
                releasedTime = -1f;
                return;
            }

            if (releasedTime < 0f) releasedTime = Time.time;
            if (Time.time - releasedTime >= idleReturnSeconds) GoHome();
        }

        public void GoHome()
        {
            releasedTime = -1f;
            if (body.isKinematic == false)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.position = homePosition;
            body.rotation = homeRotation;
            transform.SetPositionAndRotation(homePosition, homeRotation);
        }
    }
}
