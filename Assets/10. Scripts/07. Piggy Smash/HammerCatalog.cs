using System;
using System.Collections.Generic;
using UnityEngine;

namespace StudyXR.PiggySmash
{
    /// <summary>
    /// 해금할 수 있는 망치 목록. 순서 = 상점에 표시되는 순서. 첫 번째(가격 0)가 기본 망치.
    /// Tools > Piggy Smash > Build Hammer Prefabs 가 만들고, 이후 가격·성능은 인스펙터에서 조정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Piggy Smash/Hammer Catalog", fileName = "HammerCatalog")]
    public class HammerCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string id = "";
            public string displayName = "";
            public GameObject prefab;
            [Min(0)] public int cost;
            [Tooltip("한 번 맞힐 때 깎는 타수")]
            [Min(1)] public int damage = 1;
            [Tooltip("주변 광맥까지 함께 맞히는 반경 (m)")]
            [Min(0f)] public float splashRadius;
        }

        [SerializeField] private List<Entry> hammers = new List<Entry>();

        public IReadOnlyList<Entry> Hammers => hammers;
        public int Count => hammers.Count;

        public Entry Get(int index) => index >= 0 && index < hammers.Count ? hammers[index] : null;

        public int IndexOf(string id)
        {
            for (int i = 0; i < hammers.Count; ++i)
                if (hammers[i].id == id) return i;
            return -1;
        }

        public Entry Find(string id)
        {
            int i = IndexOf(id);
            return i >= 0 ? hammers[i] : null;
        }

#if UNITY_EDITOR
        public List<Entry> EditableList => hammers;
#endif
    }
}
