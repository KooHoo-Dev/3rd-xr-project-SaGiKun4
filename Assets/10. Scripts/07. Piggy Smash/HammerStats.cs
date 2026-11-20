using UnityEngine;

namespace StudyXR.PiggySmash
{
    /// <summary>
    /// 망치 한 자루의 공격력. 망치 루트(Rigidbody 가 있는 오브젝트)에 붙인다.
    /// OreVein 은 맞힌 Head Hitter 의 부모에서 이 컴포넌트를 찾아, 한 번 맞을 때 damage 만큼 타수를 깎는다.
    /// 공격 범위는 각 망치의 Head Hitter 콜라이더 크기로 정한다.
    /// </summary>
    public class HammerStats : MonoBehaviour
    {
        [Tooltip("한 번 맞힐 때 깎는 타수")]
        [Min(1)] [SerializeField] private int damage = 1;

        public int Damage => damage;

        public void SetDamage(int value) => damage = Mathf.Max(1, value);
    }
}
