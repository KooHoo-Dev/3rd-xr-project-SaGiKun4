using UnityEngine;

namespace StudyXR.Blacksmith
{
    /// <summary>
    /// 같은 오브젝트의 AudioSource 출력에 배율을 곱한다. AudioSource 볼륨(최대 1)보다 크게 키울 때 쓴다 (예: BGM 1.5배).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioGain : MonoBehaviour
    {
        [Tooltip("출력 배율. 1 이면 그대로, 1.5 면 1.5배")]
        [Range(0f, 4f)] [SerializeField] private float gain = 1f;

        private volatile float current = 1f;

        public float Gain
        {
            get => gain;
            set { gain = Mathf.Max(0f, value); current = gain; }
        }

        private void Awake() => current = gain;
        private void OnValidate() => current = gain;

        // 오디오 스레드에서 호출된다
        private void OnAudioFilterRead(float[] data, int channels)
        {
            float g = current;
            if (Mathf.Approximately(g, 1f)) return;
            for (int i = 0; i < data.Length; ++i) data[i] *= g;
        }
    }
}
