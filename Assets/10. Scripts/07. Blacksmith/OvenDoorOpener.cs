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

        private Quaternion closedRotation;
        private Coroutine anim;

        private void Awake()
        {
            if (door == null && string.IsNullOrEmpty(doorName) == false)
            {
                var found = GameObject.Find(doorName);
                if (found != null) door = found.transform;
            }
            if (door == null) Debug.LogWarning($"[OvenDoorOpener] '{doorName}' 를 찾지 못했습니다.", this);
            else closedRotation = door.localRotation;
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

        public void Open() => RotateTo(closedRotation * Quaternion.Euler(0f, openAngle, 0f));
        public void Close() => RotateTo(closedRotation);

        private void OnRoundEnded()
        {
            if (closeOnRoundEnd) Close();
        }

        private void RotateTo(Quaternion target)
        {
            if (door == null) return;
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(Animate(target));
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
