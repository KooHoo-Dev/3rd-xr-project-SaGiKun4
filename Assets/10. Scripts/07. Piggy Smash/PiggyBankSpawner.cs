using System.Collections;
using UnityEngine;
using UnityEngine.XR.Content.Interaction;

namespace StudyXR.PiggySmash
{
    /// <summary>
    /// 테이블 위 스폰 지점에 PiggyBank_Breakable 을 생성하고,
    /// Breakable.onBreak 를 받아 점수를 올린 뒤 일정 시간 후 다시 생성한다.
    /// </summary>
    public class PiggyBankSpawner : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private PiggySmashGame game;
        [Tooltip("Breakable 컴포넌트가 붙은 저금통 프리팹 (PiggyBank_Breakable)")]
        [SerializeField] private Breakable piggyPrefab;
        [Tooltip("테이블 위 생성 위치. 여러 개면 매번 랜덤으로 고른다.")]
        [SerializeField] private Transform[] spawnPoints;

        [Header("규칙")]
        [SerializeField] private int pointsPerBreak = 1;
        [SerializeField] private float respawnDelay = 1.0f;
        [Tooltip("깨진 저금통(PiggyBank_Shattered)이 생성된 뒤 사라지기까지의 시간 (초)")]
        [SerializeField] private float shardLifetime = 2.0f;
        [Tooltip("사라지기 직전 조각들이 작아지는 시간 (shardLifetime 에 포함)")]
        [SerializeField] private float shardShrinkSeconds = 0.4f;

        [Header("생성 연출")]
        [SerializeField] private float popInSeconds = 0.25f;
        [SerializeField] private AudioClip spawnClip;

        [Header("떨어진 저금통 되돌리기 (점수 없음)")]
        [Tooltip("스폰 지점에서 이 거리 이상 멀어지면 다시 생성")]
        [SerializeField] private float maxDistanceFromSpawn = 2.0f;

        private Breakable current;
        private Transform currentPoint;
        private Coroutine respawnRoutine;

        private void OnEnable()
        {
            if (game == null) return;
            game.RoundStarted += OnRoundStarted;
            game.RoundEnded += OnRoundEnded;
        }

        private void OnDisable()
        {
            if (game == null) return;
            game.RoundStarted -= OnRoundStarted;
            game.RoundEnded -= OnRoundEnded;
        }

        private void Start()
        {
            // 게임 매니저 없이 단독으로 써도 동작하도록
            if (game == null) Spawn();
        }

        private void Update()
        {
            if (current == null || currentPoint == null) return;

            // 손으로 집어 테이블 밖으로 떨어뜨리거나 너무 멀리 던졌으면 회수 후 재생성
            if ((current.transform.position - currentPoint.position).sqrMagnitude > maxDistanceFromSpawn * maxDistanceFromSpawn)
            {
                ClearCurrent();
                Spawn();
            }
        }

        private void OnRoundStarted()
        {
            if (respawnRoutine != null) StopCoroutine(respawnRoutine);
            respawnRoutine = null;
            ClearCurrent();
            Spawn();
        }

        private void OnRoundEnded()
        {
            if (respawnRoutine != null) StopCoroutine(respawnRoutine);
            respawnRoutine = null;
            ClearCurrent();
        }

        public void Spawn()
        {
            if (piggyPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogWarning("[PiggyBankSpawner] 프리팹 또는 스폰 지점이 비어 있습니다.", this);
                return;
            }

            currentPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            current = Instantiate(piggyPrefab, currentPoint.position, currentPoint.rotation);
            current.onBreak.AddListener(OnPiggyBroken);

            if (spawnClip != null) AudioSource.PlayClipAtPoint(spawnClip, currentPoint.position);
            if (popInSeconds > 0f) StartCoroutine(PopIn(current.transform, popInSeconds));
        }

        // Breakable.onBreak(때린 오브젝트, 부서진 버전)
        private void OnPiggyBroken(GameObject breaker, GameObject brokenVersion)
        {
            Vector3 position = current != null ? current.transform.position : brokenVersion.transform.position;
            current = null;

            if (brokenVersion != null)
            {
                // PiggyBank_Shattered 에 붙은 Unbreakable 은 1초 뒤 조각을 되감아 새 저금통을 만든다.
                // 스포너가 새 저금통을 따로 생성하므로, 되감기를 막고 조각은 그대로 사라지게 한다.
                var restore = brokenVersion.GetComponent<Unbreakable>();
                if (restore != null)
                {
                    restore.enabled = false; // 즉시 멈춤 (Destroy 는 프레임 끝에 처리됨)
                    Destroy(restore);
                }

                StartCoroutine(RemoveShards(brokenVersion));
                Destroy(brokenVersion, shardLifetime + 0.1f); // 스포너가 꺼져 코루틴이 멈춰도 확실히 정리
            }
            if (game != null) game.AddPoints(pointsPerBreak, position + Vector3.up * 0.25f);

            bool keepGoing = (game == null) || game.IsRunning;
            if (keepGoing) respawnRoutine = StartCoroutine(RespawnAfter(respawnDelay));
        }

        private IEnumerator RespawnAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            respawnRoutine = null;
            if (game == null || game.IsRunning) Spawn();
        }

        // 깨진 버전을 shardLifetime 뒤에 제거. 마지막 shardShrinkSeconds 동안 조각들을 작게 줄인다.
        private IEnumerator RemoveShards(GameObject broken)
        {
            float shrink = Mathf.Clamp(shardShrinkSeconds, 0f, shardLifetime);
            yield return new WaitForSeconds(shardLifetime - shrink);
            if (broken == null) yield break;

            if (shrink > 0f)
            {
                var pieces = broken.GetComponentsInChildren<Transform>();
                var scales = new Vector3[pieces.Length];
                for (int i = 0; i < pieces.Length; ++i) scales[i] = pieces[i].localScale;

                for (float t = 0f; t < shrink && broken != null; t += Time.deltaTime)
                {
                    float k = 1f - t / shrink;
                    for (int i = 0; i < pieces.Length; ++i)
                        if (pieces[i] != null && pieces[i] != broken.transform) pieces[i].localScale = scales[i] * k;
                    yield return null;
                }
            }

            if (broken != null) Destroy(broken);
        }

        private void ClearCurrent()
        {
            if (current == null) return;
            current.onBreak.RemoveListener(OnPiggyBroken);
            Destroy(current.gameObject);
            current = null;
        }

        private static IEnumerator PopIn(Transform target, float seconds)
        {
            Vector3 full = target.localScale;
            float t = 0f;
            while (target != null && t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / seconds);
                // 살짝 튀어오르는 느낌 (overshoot)
                float s = 1f + 2.7f * Mathf.Pow(k - 1f, 3f) + 1.7f * Mathf.Pow(k - 1f, 2f);
                target.localScale = full * Mathf.Max(0.01f, s);
                yield return null;
            }
            if (target != null) target.localScale = full;
        }
    }
}
