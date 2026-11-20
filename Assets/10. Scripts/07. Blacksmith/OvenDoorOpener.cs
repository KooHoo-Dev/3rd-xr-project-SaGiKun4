using System.Collections;
using UnityEngine;

namespace StudyXR.Blacksmith
{
    /// <summary>
    /// 라운드가 시작되면 오븐 문(OvenDoor)을 로컬 +Y 축으로 openAngle 만큼 열고,
    /// 라운드가 끝나면 (옵션) 다시 닫는다.
    /// </summary>
    public class OvenDoorOpener : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private BlacksmithGame game;
        [Tooltip("열 문. 비우면 아래 이름으로 씬에서 찾는다")]
        [SerializeField] private Transform door;
        [SerializeField] private string doorName = "OvenDoor";

        [Header("열기")]
        [Tooltip("로컬 Y 축 기준 회전 각도 (도). 반대로 열리면 음수로")]
        [SerializeField] private float openAngle = 35f;
        [SerializeField] private float openSeconds = 0.6f;
        [SerializeField] private bool closeOnRoundEnd = true;

        [Header("소리")]
        [Tooltip("문이 열리는 동안 재생할 소리 (Door)")]
        [SerializeField] private AudioClip openClip;
        [Tooltip("켜면 닫힐 때도 같은 소리를 재생")]
        [SerializeField] private bool playOnClose;
        [Range(0f, 1f)] [SerializeField] private float volume = 1f;

        private Quaternion closedRotation;
        private Coroutine anim;
        private AudioSource audioSource;

        private void Awake()
        {
            if (door == null && string.IsNullOrEmpty(doorName) == false)
            {
                var found = GameObject.Find(doorName);
                if (found != null) door = found.transform;
            }
            if (door == null) Debug.LogWarning($"[OvenDoorOpener] '{doorName}' 를 찾지 못했습니다.", this);
            else
            {
                closedRotation = door.localRotation;
                // 문 위치에서 소리가 나도록 문에 3D AudioSource 를 붙인다
                audioSource = door.GetComponent<AudioSource>();
                if (audioSource == null) audioSource = door.gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.spatialBlend = 1f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.minDistance = 1f;
                audioSource.maxDistance = 15f;
            }
        }

        private void OnEnable()
        {
            if (game == null) return;
            game.RoundStarted += Open;
            game.RoundEnded += OnRoundEnded;
        }

        private void OnDisable()
        {
            if (game == null) return;
            game.RoundStarted -= Open;
            game.RoundEnded -= OnRoundEnded;
        }

        private void Start()
        {
            // 게임 매니저 없이 쓰거나, 이미 라운드가 시작된 뒤 켜졌을 때
            if (game == null || game.IsRunning) Open();
        }

        public void Open()
        {
            if (RotateTo(closedRotation * Quaternion.Euler(0f, openAngle, 0f))) PlaySound();
        }

        public void Close()
        {
            if (RotateTo(closedRotation) && playOnClose) PlaySound();
        }

        private void PlaySound()
        {
            if (audioSource == null || openClip == null) return;
            audioSource.Stop();
            audioSource.clip = openClip;
            audioSource.volume = volume;
            audioSource.Play();
        }

        private void OnRoundEnded()
        {
            if (closeOnRoundEnd) Close();
        }

        // 실제로 움직일 때만 true (이미 그 각도면 소리를 내지 않는다)
        private bool RotateTo(Quaternion target)
        {
            if (door == null) return false;
            if (anim != null) StopCoroutine(anim);
            anim = null;
            if (Quaternion.Angle(door.localRotation, target) < 0.5f)
            {
                door.localRotation = target;
                return false;
            }
            anim = StartCoroutine(Animate(target));
            return true;
        }

        private IEnumerator Animate(Quaternion target)
        {
            Quaternion from = door.localRotation;
            for (float t = 0f; t < openSeconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / openSeconds);
                door.localRotation = Quaternion.Slerp(from, target, k);
                yield return null;
            }
            door.localRotation = target;
            anim = null;
        }
    }
}
