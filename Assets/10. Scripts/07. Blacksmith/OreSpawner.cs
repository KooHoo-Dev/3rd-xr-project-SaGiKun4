using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace StudyXR.Blacksmith
{
    /// <summary>
    /// 광맥 스포너.
    /// 3~5초(랜덤) 간격으로, 기존 광맥이 남아 있든 말든 1~3개의 광맥을 스폰 영역에 랜덤 생성한다.
    /// 광맥 종류는 가중치로 고르고, 종류마다 점수가 다르다 (Bronze 1 / Silver 5 / Gold 10).
    /// </summary>
    public class OreSpawner : MonoBehaviour
    {
        [Serializable]
        public class OreType
        {
            public string name = "Bronze";
            public GameObject prefab;
            [Min(0)] public int points = 1;
            [Tooltip("캐기까지 때려야 하는 횟수")]
            [Min(1)] public int hitsToMine = 1;
            [Tooltip("뽑힐 확률 가중치. 클수록 자주 나온다")]
            [Min(0f)] public float weight = 1f;
            public Color popupColor = Color.white;
        }

        [Header("연결")]
        [SerializeField] private BlacksmithGame game;

        [Header("광맥 종류")]
        [SerializeField] private List<OreType> ores = new List<OreType>();

        [Header("스폰 위치")]
        [Tooltip("스폰 지점 목록 (spawnArea 가 없을 때 사용)")]
        [SerializeField] private Transform[] spawnPoints;
        [Tooltip("이 콜라이더의 윗면(+Y) 범위 중 랜덤 위치에 생성. BoxCollider 면 회전까지 고려한다.")]
        [SerializeField] private Collider spawnArea;
        [Tooltip("윗면 가장자리에서 안쪽으로 띄울 거리 (m)")]
        [SerializeField] private float areaEdgeMargin = 0.15f;
        [Tooltip("윗면에서 위로 띄울 높이 (m)")]
        [SerializeField] private float areaHeightOffset = 0.01f;
        [Tooltip("켜면 Y축 회전을 랜덤으로")]
        [SerializeField] private bool randomYaw = true;
        [Tooltip("광맥끼리 최소 간격 (m). 자리가 없으면 그 광맥은 건너뛴다")]
        [SerializeField] private float minSpacing = 0.25f;

        [Header("스폰 규칙")]
        [SerializeField] private float minInterval = 3f;
        [SerializeField] private float maxInterval = 5f;
        [SerializeField] private int minPerWave = 1;
        [SerializeField] private int maxPerWave = 3;
        [Tooltip("동시에 존재할 수 있는 최대 개수. 0 이면 제한 없음")]
        [SerializeField] private int maxAlive = 0;

        [Header("크기")]
        [Tooltip("광맥의 가장 긴 변을 이 길이(m)로 맞춘다. 0 이면 프리팹 크기 그대로")]
        [SerializeField] private float fitSize = 0.35f;

        [Header("채굴 후 빨려들어갈 곳")]
        [Tooltip("채굴된 광맥이 날아갈 대상. 비우면 아래 이름으로 씬에서 찾는다")]
        [SerializeField] private Transform sinkTarget;
        [SerializeField] private string sinkTargetName = "Fireplace_Logs";
        [Tooltip("대상까지 날아가는 시간 (초)")]
        [SerializeField] private float flySeconds = 0.9f;
        [Tooltip("날아가는 궤적의 최고 높이 (m)")]
        [SerializeField] private float arcHeight = 0.6f;

        [Header("금 가는 표현")]
        [Tooltip("OreCrack 머티리얼 (Assets/10. Scripts/07. Blacksmith/Crack/OreCrack.mat)")]
        [SerializeField] private Material crackMaterial;
        [Tooltip("캐지지 않은 타격마다 나는 소리")]
        [SerializeField] private AudioClip hitClip;

        [Header("연출")]
        [SerializeField] private float popInSeconds = 0.25f;
        [SerializeField] private AudioClip spawnClip;
        [SerializeField] private GameObject breakEffect;
        [SerializeField] private AudioClip breakClip;

        private readonly List<OreVein> alive = new List<OreVein>();
        private Coroutine loop;

        public IReadOnlyList<OreVein> Alive => alive;

        private void OnEnable()
        {
            if (game == null) return;
            game.RoundStarted += OnRoundStarted;
            game.RoundEnded += OnRoundEnded;
        }

        private void OnDisable()
        {
            if (game != null)
            {
                game.RoundStarted -= OnRoundStarted;
                game.RoundEnded -= OnRoundEnded;
            }
            StopLoop();
        }

        private void Awake()
        {
            if (sinkTarget == null && string.IsNullOrEmpty(sinkTargetName) == false)
            {
                var found = GameObject.Find(sinkTargetName);
                if (found != null) sinkTarget = found.transform;
                else Debug.LogWarning($"[OreSpawner] '{sinkTargetName}' 를 찾지 못해 광맥이 제자리에서 사라집니다.", this);
            }
        }

        private void Start()
        {
            // 게임 매니저 없이 단독으로 써도 동작하도록
            if (game == null) StartLoop();
        }

        private void OnRoundStarted()
        {
            ClearAll();
            StartLoop();
        }

        private void OnRoundEnded()
        {
            StopLoop();
            ClearAll();
        }

        private void StartLoop()
        {
            StopLoop();
            loop = StartCoroutine(SpawnLoop());
        }

        private void StopLoop()
        {
            if (loop != null) StopCoroutine(loop);
            loop = null;
        }

        private IEnumerator SpawnLoop()
        {
            while (true)
            {
                SpawnWave();
                float wait = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
                yield return new WaitForSeconds(wait);
                if (game != null && game.IsRunning == false) yield break;
            }
        }

        /// <summary>1~3개(minPerWave~maxPerWave)를 한 번에 생성</summary>
        public void SpawnWave()
        {
            int count = Random.Range(Mathf.Min(minPerWave, maxPerWave), Mathf.Max(minPerWave, maxPerWave) + 1);
            for (int i = 0; i < count; ++i)
            {
                if (maxAlive > 0 && alive.Count >= maxAlive) return;
                SpawnOne();
            }
        }

        public OreVein SpawnOne()
        {
            var type = PickType();
            if (type == null || type.prefab == null)
            {
                Debug.LogWarning("[OreSpawner] 광맥 프리팹이 비어 있습니다.", this);
                return null;
            }
            if (TryPickPosition(out Vector3 position, out Quaternion rotation) == false) return null;

            var go = Instantiate(type.prefab, position, rotation);
            go.name = type.name;
            FitAndPlace(go.transform, position);

            var vein = go.GetComponent<OreVein>();
            if (vein == null) vein = go.AddComponent<OreVein>();
            vein.Setup(type.name, type.points, breakEffect, breakClip);
            vein.SetupSink(sinkTarget, flySeconds, arcHeight);
            vein.SetupHits(type.hitsToMine, crackMaterial, hitClip);
            vein.Mined += (v, breaker) => OnMined(v, type);
            alive.Add(vein);

            if (spawnClip != null) AudioSource.PlayClipAtPoint(spawnClip, position);
            if (popInSeconds > 0f) StartCoroutine(PopIn(go.transform, popInSeconds));
            return vein;
        }

        private void OnMined(OreVein vein, OreType type)
        {
            alive.Remove(vein);
            if (game != null) game.AddPoints(type.points, vein.transform.position + Vector3.up * 0.25f, type.popupColor);
        }

        private OreType PickType()
        {
            float total = 0f;
            foreach (var o in ores) if (o != null && o.prefab != null) total += Mathf.Max(0f, o.weight);
            if (total <= 0f) return null;

            float r = Random.value * total;
            foreach (var o in ores)
            {
                if (o == null || o.prefab == null) continue;
                r -= Mathf.Max(0f, o.weight);
                if (r <= 0f) return o;
            }
            return ores[ores.Count - 1];
        }

        private bool TryPickPosition(out Vector3 position, out Quaternion rotation)
        {
            bool hasPoints = spawnPoints != null && spawnPoints.Length > 0;
            position = default;
            rotation = Quaternion.identity;
            if (spawnArea == null && hasPoints == false)
            {
                Debug.LogWarning("[OreSpawner] 스폰 지점/영역이 비어 있습니다.", this);
                return false;
            }

            alive.RemoveAll(v => v == null);
            for (int attempt = 0; attempt < 12; ++attempt)
            {
                if (spawnArea != null)
                {
                    position = RandomPointOnTop(spawnArea);
                    rotation = Quaternion.Euler(0f, spawnArea.transform.eulerAngles.y, 0f);
                }
                else
                {
                    var point = spawnPoints[Random.Range(0, spawnPoints.Length)];
                    position = point.position;
                    rotation = point.rotation;
                }
                if (randomYaw) rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                if (IsFree(position)) return true;
            }
            return false; // 자리가 없으면 이번 광맥은 건너뜀
        }

        private bool IsFree(Vector3 position)
        {
            float min2 = minSpacing * minSpacing;
            foreach (var v in alive)
            {
                if (v == null || v.IsMined) continue;
                Vector3 d = v.transform.position - position;
                d.y = 0f;
                if (d.sqrMagnitude < min2) return false;
            }
            return true;
        }

        // 크기를 fitSize 로 맞추고, 메시 바닥이 스폰 면에 닿도록 높이를 보정
        private void FitAndPlace(Transform t, Vector3 surfacePoint)
        {
            if (TryGetRendererBounds(t, out Bounds b) == false) return;

            if (fitSize > 0f)
            {
                float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (longest > 1e-4f) t.localScale *= fitSize / longest;
                TryGetRendererBounds(t, out b);
            }
            t.position += Vector3.up * (surfacePoint.y - b.min.y);
        }

        private static bool TryGetRendererBounds(Transform t, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                if (found == false) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return found;
        }

        // 콜라이더 윗면(+Y)의 랜덤 위치 (월드 좌표)
        private Vector3 RandomPointOnTop(Collider area)
        {
            if (area is BoxCollider box)
            {
                Vector3 scale = box.transform.lossyScale;
                float hx = Mathf.Max(0f, box.size.x * 0.5f - areaEdgeMargin / Mathf.Max(1e-4f, Mathf.Abs(scale.x)));
                float hz = Mathf.Max(0f, box.size.z * 0.5f - areaEdgeMargin / Mathf.Max(1e-4f, Mathf.Abs(scale.z)));
                Vector3 local = box.center + new Vector3(Random.Range(-hx, hx), box.size.y * 0.5f, Random.Range(-hz, hz));
                return box.transform.TransformPoint(local) + Vector3.up * areaHeightOffset;
            }

            Bounds b = area.bounds;
            float ex = Mathf.Max(0f, b.extents.x - areaEdgeMargin);
            float ez = Mathf.Max(0f, b.extents.z - areaEdgeMargin);
            return new Vector3(b.center.x + Random.Range(-ex, ex), b.max.y + areaHeightOffset, b.center.z + Random.Range(-ez, ez));
        }

        public void ClearAll()
        {
            foreach (var v in alive)
                if (v != null) Destroy(v.gameObject);
            alive.Clear();
        }

        private static IEnumerator PopIn(Transform target, float seconds)
        {
            Vector3 full = target.localScale;
            float t = 0f;
            while (target != null && t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / seconds);
                float s = 1f + 2.7f * Mathf.Pow(k - 1f, 3f) + 1.7f * Mathf.Pow(k - 1f, 2f); // 살짝 튀어오르는 느낌
                target.localScale = full * Mathf.Max(0.01f, s);
                yield return null;
            }
            if (target != null) target.localScale = full;
        }

        private void OnDrawGizmosSelected()
        {
            if (spawnArea is BoxCollider box)
            {
                Gizmos.color = new Color(1f, 0.75f, 0.3f, 0.9f);
                Gizmos.matrix = box.transform.localToWorldMatrix;
                Vector3 scale = box.transform.lossyScale;
                float sx = Mathf.Max(0f, box.size.x - 2f * areaEdgeMargin / Mathf.Max(1e-4f, Mathf.Abs(scale.x)));
                float sz = Mathf.Max(0f, box.size.z - 2f * areaEdgeMargin / Mathf.Max(1e-4f, Mathf.Abs(scale.z)));
                Gizmos.DrawWireCube(box.center + Vector3.up * box.size.y * 0.5f, new Vector3(sx, 0f, sz));
            }
        }

#if UNITY_EDITOR
        // 광맥 목록이 비어 있으면 Metal Ore 의 Bronze / Silver / Gold 를 기본값으로 채운다.
        private void Reset() => FillDefaultOres();

        private void OnValidate()
        {
            if (ores == null || ores.Count == 0) FillDefaultOres();
        }

        [ContextMenu("기본 광맥 (Bronze 1 / Silver 5 / Gold 10) 으로 채우기")]
        private void FillDefaultOres()
        {
            const string dir = "Assets/Metal Ore/Prefabs/";
            ores = new List<OreType>
            {
                new OreType { name = "Bronze", points = 1,  hitsToMine = 1, weight = 6f, popupColor = new Color(0.85f, 0.55f, 0.3f),
                              prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(dir + "Bronze.prefab") },
                new OreType { name = "Silver", points = 5,  hitsToMine = 3, weight = 3f, popupColor = new Color(0.85f, 0.9f, 1f),
                              prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(dir + "Silver.prefab") },
                new OreType { name = "Gold",   points = 10, hitsToMine = 5, weight = 1f, popupColor = new Color(1f, 0.84f, 0.2f),
                              prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(dir + "Gold.prefab") },
            };
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
