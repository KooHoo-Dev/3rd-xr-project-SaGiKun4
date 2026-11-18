using System;
using System.Collections;
using UnityEngine;

namespace StudyXR.PiggySmash
{
    /// <summary>
    /// 광맥 하나. Destroyer 태그(망치 Head Hitter)에 맞으면 채굴되어 점수 이벤트를 보내고 사라진다.
    /// 프리팹에 콜라이더/리지드바디가 없으면 런타임에 자동으로 붙인다.
    /// </summary>
    public class OreVein : MonoBehaviour
    {
        [SerializeField] private string hitterTag = "Destroyer";
        [SerializeField] private string oreName = "Ore";
        [SerializeField] private int points = 1;
        [Tooltip("캐기까지 때려야 하는 횟수 (Bronze 1 / Silver 3 / Gold 5)")]
        [Min(1)] [SerializeField] private int hitsToMine = 1;
        [Tooltip("한 번 휘두를 때 여러 번 맞은 것으로 세지 않도록 타격 사이 최소 간격 (초)")]
        [SerializeField] private float hitCooldown = 0.3f;
        [SerializeField] private AudioClip hitClip;

        [Header("금 가는 표현")]
        [Tooltip("OreCrack 머티리얼. 맞을 때마다 금이 더 많이 드러난다")]
        [SerializeField] private Material crackMaterial;
        [Tooltip("맞은 뒤 흔들리며 작아져 사라지는 시간 (초). 빨려들어갈 대상이 없을 때 사용")]
        [SerializeField] private float breakSeconds = 0.3f;
        [SerializeField] private GameObject breakEffect;
        [SerializeField] private AudioClip breakClip;

        [Header("빨려들어가기")]
        [Tooltip("채굴된 광맥이 날아가 빨려들어갈 대상 (예: 오븐 안 Fireplace_Logs)")]
        [SerializeField] private Transform sinkTarget;
        [Tooltip("맞은 직후 제자리에서 움찔하는 시간 (초)")]
        [SerializeField] private float hitPauseSeconds = 0.12f;
        [Tooltip("대상까지 날아가는 시간 (초)")]
        [SerializeField] private float flySeconds = 0.9f;
        [Tooltip("날아가는 궤적의 최고 높이 (m)")]
        [SerializeField] private float arcHeight = 0.6f;
        [Tooltip("도착할 때의 크기 비율")]
        [SerializeField] private float endScale = 0.1f;
        [Tooltip("날아가며 도는 속도 (도/초)")]
        [SerializeField] private float spinSpeed = 720f;

        private bool mined;
        private int hits;
        private float lastHitTime = float.NegativeInfinity;
        private Renderer[] crackRenderers;
        private MaterialPropertyBlock crackBlock;
        private Coroutine punch;
        private Vector3 punchScale, punchPos;
        private static readonly int CrackId = Shader.PropertyToID("_Crack");

        public string OreName => oreName;
        public int Points => points;
        public bool IsMined => mined;
        public int Hits => hits;
        public int HitsToMine => hitsToMine;

        /// <summary>맞았지만 아직 캐지지 않았을 때 (광맥, 현재 타격 수)</summary>
        public event Action<OreVein, int> Hit;

        /// <summary>채굴됐을 때 (광맥, 때린 오브젝트)</summary>
        public event Action<OreVein, GameObject> Mined;

        public void Setup(string name, int value, GameObject effect, AudioClip clip)
        {
            oreName = name;
            points = value;
            breakEffect = effect;
            breakClip = clip;
        }

        /// <summary>필요 타격 수와 금 가는 머티리얼</summary>
        public void SetupHits(int required, Material crack, AudioClip clip)
        {
            hitsToMine = Mathf.Max(1, required);
            crackMaterial = crack;
            hitClip = clip;
            AttachCrackOverlay();
        }

        /// <summary>채굴 후 빨려들어갈 대상과 비행 설정</summary>
        public void SetupSink(Transform target, float seconds, float height)
        {
            sinkTarget = target;
            flySeconds = seconds;
            arcHeight = height;
        }

        private void Awake()
        {
            EnsurePhysics();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (mined) return;
            // collision.gameObject 는 Rigidbody 가 붙은 오브젝트 = Head Hitter
            if (collision.gameObject.CompareTag(hitterTag) == false) return;
            if (Time.time - lastHitTime < hitCooldown) return;
            lastHitTime = Time.time;
            TakeHit(collision.gameObject);
        }

        public void TakeHit(GameObject breaker)
        {
            if (mined) return;
            hits++;
            if (hits >= hitsToMine)
            {
                Mine(breaker);
                return;
            }

            SetCrack((float)hits / hitsToMine);
            if (hitClip != null) AudioSource.PlayClipAtPoint(hitClip, transform.position);
            Hit?.Invoke(this, hits);
            StopPunch();
            punchScale = transform.localScale;
            punchPos = transform.position;
            punch = StartCoroutine(Punch());
        }

        // 맞았을 때 움찔하는 반응 (크기·위치만 잠깐 바뀌고 원래대로)
        private IEnumerator Punch()
        {
            const float seconds = 0.15f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.Sin(t / seconds * Mathf.PI);
                transform.localScale = punchScale * (1f + 0.08f * k);
                transform.position = punchPos + UnityEngine.Random.insideUnitSphere * (0.01f * k);
                yield return null;
            }
            transform.localScale = punchScale;
            transform.position = punchPos;
            punch = null;
        }

        // 움찔하는 도중이면 멈추고 원래 크기·위치로 되돌린다
        private void StopPunch()
        {
            if (punch == null) return;
            StopCoroutine(punch);
            punch = null;
            transform.localScale = punchScale;
            transform.position = punchPos;
        }

        // 각 메시 렌더러에 금 간 무늬 머티리얼을 하나 더 얹는다 (같은 메시를 한 번 더 그림)
        private void AttachCrackOverlay()
        {
            if (crackMaterial == null || crackRenderers != null) return;
            crackRenderers = GetComponentsInChildren<MeshRenderer>();
            crackBlock = new MaterialPropertyBlock();
            foreach (var r in crackRenderers)
            {
                var mats = r.sharedMaterials;
                var list = new Material[mats.Length + 1];
                mats.CopyTo(list, 0);
                list[mats.Length] = crackMaterial;
                r.sharedMaterials = list;
            }
            SetCrack(0f);
        }

        private void SetCrack(float amount)
        {
            if (crackRenderers == null) return;
            foreach (var r in crackRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(crackBlock);
                crackBlock.SetFloat(CrackId, amount);
                r.SetPropertyBlock(crackBlock);
            }
        }

        public void Mine(GameObject breaker)
        {
            if (mined) return;
            mined = true;
            StopPunch();
            SetCrack(1f);

            DisablePhysics();

            if (breakEffect != null) Destroy(Instantiate(breakEffect, transform.position, Quaternion.identity), 3f);
            if (breakClip != null) AudioSource.PlayClipAtPoint(breakClip, transform.position);

            Mined?.Invoke(this, breaker);
            StartCoroutine(sinkTarget != null ? FlyIntoTarget() : BreakAway());
        }

        // 채굴된 순간부터 어떤 것과도 부딪히지 않게: 콜라이더 끄기 + Rigidbody 를 물리에서 빼기
        private void DisablePhysics()
        {
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var body in GetComponentsInChildren<Rigidbody>(true))
            {
                body.detectCollisions = false;
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.None;
            }
        }

        // 맞은 자리에서 움찔 → 포물선을 그리며 대상 쪽으로 가속 → 돌면서 작아지며 빨려들어감
        private IEnumerator FlyIntoTarget()
        {
            Vector3 startScale = transform.localScale;
            Vector3 startPos = transform.position;

            for (float t = 0f; t < hitPauseSeconds; t += Time.deltaTime)
            {
                float k = t / hitPauseSeconds;
                transform.localScale = startScale * (1f + 0.15f * Mathf.Sin(k * Mathf.PI)); // 맞아서 부풀었다 돌아옴
                transform.position = startPos + UnityEngine.Random.insideUnitSphere * 0.015f;
                yield return null;
            }
            transform.position = startPos;

            Quaternion startRot = transform.rotation;
            Vector3 spinAxis = UnityEngine.Random.onUnitSphere;
            float duration = Mathf.Max(0.05f, flySeconds);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (sinkTarget == null) break;
                float k = t / duration;
                float move = k * k * k; // 처음엔 천천히, 끝으로 갈수록 빨려들어가듯 가속

                Vector3 end = sinkTarget.position;
                Vector3 control = (startPos + end) * 0.5f + Vector3.up * arcHeight;
                transform.position = QuadraticBezier(startPos, control, end, move);
                transform.rotation = Quaternion.AngleAxis(spinSpeed * t, spinAxis) * startRot;
                transform.localScale = startScale * Mathf.Lerp(1f, endScale, k * k);
                yield return null;
            }
            Destroy(gameObject);
        }

        private static Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        private IEnumerator BreakAway()
        {
            Vector3 startScale = transform.localScale;
            Vector3 startPos = transform.position;
            for (float t = 0f; t < breakSeconds; t += Time.deltaTime)
            {
                float k = t / breakSeconds;
                transform.localScale = startScale * (1f - k * k);
                transform.position = startPos + UnityEngine.Random.insideUnitSphere * (0.02f * (1f - k)); // 맞은 느낌의 흔들림
                yield return null;
            }
            Destroy(gameObject);
        }

        // 광물 프리팹은 메시만 있으므로 맞을 수 있게 콜라이더와 고정된 Rigidbody 를 붙인다.
        // Head Hitter 는 Kinematic 이라, 충돌 이벤트를 받으려면 광맥 쪽이 (고정된) 동적 Rigidbody 여야 한다.
        private void EnsurePhysics()
        {
            if (GetComponentInChildren<Collider>() == null) FitBoxCollider();

            var body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = false;
            body.mass = 50f;
            body.constraints = RigidbodyConstraints.FreezeAll;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void FitBoxCollider()
        {
            bool found = false;
            Bounds local = default;
            foreach (var mf in GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Bounds b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; ++i)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = transform.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (found == false) { local = new Bounds(p, Vector3.zero); found = true; }
                    else local.Encapsulate(p);
                }
            }
            if (found == false) return;

            var box = gameObject.AddComponent<BoxCollider>();
            box.center = local.center;
            box.size = local.size;
        }
    }
}
