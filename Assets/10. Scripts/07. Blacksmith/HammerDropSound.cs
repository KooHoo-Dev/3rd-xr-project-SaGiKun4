using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace StudyXR.Blacksmith
{
    /// <summary>
    /// 선택된(스탠드에 올렸던) 망치가 손을 떠난 상태로 어딘가에 부딪히면 Drop 소리를 낸다.
    /// 망치 루트(Rigidbody 가 있는 오브젝트)에 붙는다. HammerRack 이 망치마다 붙이고 클립을 넘겨준다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class HammerDropSound : MonoBehaviour
    {
        [Tooltip("부딪힐 때 랜덤으로 하나 재생 (Drop1~4)")]
        [SerializeField] private AudioClip[] clips;
        [Tooltip("1 보다 크면 증폭")]
        [Range(0f, 3f)] [SerializeField] private float volume = 2f;
        [Tooltip("이 속도(m/s) 이상으로 부딪혀야 소리가 난다 (바닥에서 굴러가는 잔충돌 무시)")]
        [SerializeField] private float minImpactSpeed = 0.6f;
        [Tooltip("이 속도(m/s)에서 최대 볼륨")]
        [SerializeField] private float fullVolumeSpeed = 4f;
        [Tooltip("연속 충돌 시 최소 간격 (초)")]
        [SerializeField] private float cooldown = 0.15f;

        private Rigidbody body;
        private XRGrabInteractable grab;
        private HammerStats stats;
        private float lastTime = float.NegativeInfinity;

        public void Init(AudioClip[] dropClips, float dropVolume)
        {
            clips = dropClips;
            volume = dropVolume;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
            stats = GetComponent<HammerStats>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (clips == null || clips.Length == 0) return;
            if (stats == null) stats = GetComponent<HammerStats>();
            if (stats != null && stats.CanHit == false) return;      // 선택된 망치만
            if (grab != null && grab.isSelected) return;             // 손에 든 동안은 소리 없음
            if (body != null && body.isKinematic) return;            // 선반·스탠드에 걸린 상태
            if (Time.time - lastTime < cooldown) return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < minImpactSpeed) return;
            lastTime = Time.time;

            var clip = clips[Random.Range(0, clips.Length)];
            float k = Mathf.InverseLerp(minImpactSpeed, Mathf.Max(minImpactSpeed + 0.01f, fullVolumeSpeed), speed);
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Sfx.PlayAt(clip, point, volume * Mathf.Lerp(0.4f, 1f, k));
        }
    }
}
