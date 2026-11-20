using UnityEngine;

namespace StudyXR.Blacksmith
{
    /// <summary>
    /// 망치가 스탠드 영역(트리거 콜라이더)에 닿아 있는지 알려준다. HammerRack 이 망치마다 붙인다.
    /// 망치 Rigidbody 에 붙어야 자식 콜라이더들의 트리거 이벤트를 받는다.
    /// </summary>
    public class HammerContact : MonoBehaviour
    {
        private HammerRack rack;
        private int index;
        private Collider standArea;
        private int touching;

        /// <summary>지금 스탠드 영역에 닿아 있는지</summary>
        public bool TouchingStand => touching > 0;

        public void Init(HammerRack owner, int hammerIndex, Collider stand)
        {
            rack = owner;
            index = hammerIndex;
            standArea = stand;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (standArea == null || other != standArea) return;
            touching++;
            if (rack != null) rack.OnHammerTouchedStand(index);
        }

        private void OnTriggerExit(Collider other)
        {
            if (standArea == null || other != standArea) return;
            touching = Mathf.Max(0, touching - 1);
        }

        private void OnDisable()
        {
            touching = 0;
        }
    }
}
