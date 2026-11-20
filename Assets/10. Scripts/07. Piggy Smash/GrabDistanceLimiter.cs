using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Attachment;

namespace StudyXR.PiggySmash
{
    /// <summary>
    /// 잡은 물건을 조이스틱으로 밀고 당길 때(Near-Far Interactor 의 InteractionAttachController)
    /// 손에서 떨어질 수 있는 최소·최대 거리를 제한한다.
    /// XRI 는 당기는 쪽만 0 에서 멈추고(손으로 들어옴) 미는 쪽은 제한이 없어서, 매 갱신 직후 거리를 잘라낸다.
    /// - 이 컴포넌트가 InteractionAttachController 와 같은 오브젝트에 있으면 그 하나만,
    ///   아니면 씬의 모든 InteractionAttachController 에 적용한다.
    /// </summary>
    public class GrabDistanceLimiter : MonoBehaviour
    {
        [Tooltip("손에서 최소 거리 (m). 0 이면 끝까지 당겨 손에 쥘 수 있다")]
        [Min(0f)] [SerializeField] private float minDistance = 0f;
        [Tooltip("손에서 최대 거리 (m)")]
        [Min(0f)] [SerializeField] private float maxDistance = 1.5f;

        private readonly List<InteractionAttachController> controllers = new List<InteractionAttachController>();
        private readonly Dictionary<InteractionAttachController, System.Action> handlers = new Dictionary<InteractionAttachController, System.Action>();

        public float MinDistance { get => minDistance; set => minDistance = Mathf.Max(0f, value); }
        public float MaxDistance { get => maxDistance; set => maxDistance = Mathf.Max(0f, value); }

        private void OnEnable()
        {
            controllers.Clear();
            var own = GetComponent<InteractionAttachController>();
            if (own != null) controllers.Add(own);
            else controllers.AddRange(FindObjectsByType<InteractionAttachController>(FindObjectsInactive.Include, FindObjectsSortMode.None));

            foreach (var c in controllers)
            {
                var controller = c;
                System.Action handler = () => Clamp(controller);
                handlers[controller] = handler;
                controller.attachUpdated += handler;
            }
        }

        private void OnDisable()
        {
            foreach (var pair in handlers)
                if (pair.Key != null) pair.Key.attachUpdated -= pair.Value;
            handlers.Clear();
        }

        private void Clamp(InteractionAttachController controller)
        {
            IInteractionAttachController attach = controller;
            if (attach.hasOffset == false) return; // 손에 바로 쥔 상태

            // 앵커 자식의 로컬 위치 = 손(앵커 부모)에서 물건까지의 오프셋
            var child = attach.GetOrCreateAnchorTransform();
            Vector3 offset = child.localPosition;
            float distance = offset.magnitude;
            if (distance < 1e-5f) return;

            float max = Mathf.Max(minDistance, maxDistance);
            float clamped = Mathf.Clamp(distance, minDistance, max);
            if (Mathf.Approximately(clamped, distance)) return;
            child.localPosition = offset * (clamped / distance);
        }
    }
}
