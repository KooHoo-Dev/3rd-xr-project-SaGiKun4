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
        [Tooltip("맞은 뒤 흔들리며 작아져 사라지는 시간 (초)")]
        [SerializeField] private float breakSeconds = 0.3f;
        [SerializeField] private GameObject breakEffect;
        [SerializeField] private AudioClip breakClip;

        private bool mined;

        public string OreName => oreName;
        public int Points => points;
        public bool IsMined => mined;

        /// <summary>채굴됐을 때 (광맥, 때린 오브젝트)</summary>
        public event Action<OreVein, GameObject> Mined;

        public void Setup(string name, int value, GameObject effect, AudioClip clip)
        {
            oreName = name;
            points = value;
            breakEffect = effect;
            breakClip = clip;
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
            Mine(collision.gameObject);
        }

        public void Mine(GameObject breaker)
        {
            if (mined) return;
            mined = true;

            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;

            if (breakEffect != null) Destroy(Instantiate(breakEffect, transform.position, Quaternion.identity), 3f);
            if (breakClip != null) AudioSource.PlayClipAtPoint(breakClip, transform.position);

            Mined?.Invoke(this, breaker);
            StartCoroutine(BreakAway());
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
