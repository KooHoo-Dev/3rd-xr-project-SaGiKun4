using UnityEngine;

namespace StudyXR.PiggySmash
{
    /// <summary>
    /// 망치 한 자루의 성능. 망치 루트(Rigidbody 가 있는 오브젝트)에 붙인다.
    /// OreVein 은 맞힌 Head Hitter 의 부모에서 이 컴포넌트를 찾아 공격력·범위를 적용한다.
    /// </summary>
    public class HammerStats : MonoBehaviour
    {
        [Tooltip("HammerCatalog 의 id 와 같아야 한다 (보통 망치 모델 이름)")]
        [SerializeField] private string hammerId = "";
        [Tooltip("한 번 맞힐 때 깎는 타수")]
        [Min(1)] [SerializeField] private int damage = 1;
        [Tooltip("맞힌 지점 주변 이 반경(m) 안의 다른 광맥도 함께 맞는다. 0 이면 단일 타격")]
        [Min(0f)] [SerializeField] private float splashRadius = 0f;

        public string HammerId => hammerId;
        public int Damage => damage;
        public float SplashRadius => splashRadius;

        public void Setup(string id, int newDamage, float newSplashRadius)
        {
            hammerId = id;
            damage = Mathf.Max(1, newDamage);
            splashRadius = Mathf.Max(0f, newSplashRadius);
        }
    }
}
