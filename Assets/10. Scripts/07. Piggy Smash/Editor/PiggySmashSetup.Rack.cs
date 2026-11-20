using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudyXR.PiggySmash.EditorTools
{
    /// <summary>
    /// 망치 선반(WeaponRack) 설정.
    /// - Hammer_Level1~4 루트에 기존 Mallet 루트의 컴포넌트(XRGrabInteractable, Rigidbody, ReturnHome 등)를 복사한다.
    /// - 자식 컴포넌트에 남아 있는 Mallet 참조를 각 망치의 같은 오브젝트로 바꾼다.
    /// - Mallet 이 걸려 있던 자리에 'Hammer Anchor' 를 만들고 (선택한 망치의 Neck 이 놓일 곳), Mallet 은 끈다.
    /// - 선반에 HammerRack 을 붙이고 시작 패널을 망치 선택 UI 포함으로 다시 만든다.
    /// </summary>
    public static partial class PiggySmashSetup
    {
        private const string GameTitle = "대장장이";
        private const string AnchorName = "Hammer Anchor";

        private static readonly (string name, int cost, int damage)[] RackDefaults =
        {
            ("급조한 단조 망치",   0,   1),
            ("어설픈 단조 망치",   150, 2),
            ("균형잡힌 단조 망치", 400, 3),
            ("강력한 단조 망치",   900, 4),
        };

        [MenuItem("Tools/Piggy Smash/Setup Hammer Rack (Open Scene)")]
        public static void SetupHammerRack()
        {
            var scene = SceneManager.GetActiveScene();
            var game = Object.FindAnyObjectByType<PiggySmashGame>(FindObjectsInactive.Include);

            // 1. 망치 찾기
            var levels = new List<GameObject>();
            for (int i = 1; i <= 16; ++i)
            {
                var t = FindInScene(scene, "Hammer_Level" + i);
                if (t == null) break;
                levels.Add(t.gameObject);
            }
            var mallet = FindTemplateMallet(scene, levels);
            if (game == null || levels.Count == 0 || mallet == null)
            {
                Fail($"필요한 오브젝트를 찾지 못했습니다. Game={game != null}, Hammer_Level={levels.Count}개, Mallet={mallet != null}");
                return;
            }
            var rackTransform = FindInScene(scene, "WeaponRack.001");
            var rackGo = rackTransform != null ? rackTransform.gameObject : levels[0].transform.parent.gameObject;

            Undo.SetCurrentGroupName("Setup Hammer Rack");
            int group = Undo.GetCurrentGroup();

            // 2. 망치마다 루트 컴포넌트 복사 + Mallet 참조 교체
            int remapped = 0;
            foreach (var hammer in levels)
            {
                CopyRootComponents(mallet, hammer);
                remapped += RemapMalletReferences(mallet, hammer);
            }

            // 3. 거치대 기준점: Mallet 이 걸린 자세 그대로 Level1 을 놓았을 때의 Neck 위치·방향
            var anchor = CreateAnchor(mallet, levels[0]);

            // 4. 선반 컴포넌트
            var rack = rackGo.GetComponent<HammerRack>();
            bool fresh = rack == null;
            if (fresh) rack = Undo.AddComponent<HammerRack>(rackGo);
            var so = new SerializedObject(rack);
            so.FindProperty("game").objectReferenceValue = game;
            so.FindProperty("anchor").objectReferenceValue = anchor;
            var list = so.FindProperty("hammers");
            int oldSize = list.arraySize;
            list.arraySize = levels.Count;
            for (int i = 0; i < levels.Count; ++i)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("hammer").objectReferenceValue = levels[i];
                if (i < oldSize && fresh == false) continue; // 이미 조정한 이름·가격·공격력 유지
                var d = i < RackDefaults.Length ? RackDefaults[i] : ($"단조 망치 {i + 1}", 500 * i, i + 1);
                e.FindPropertyRelative("displayName").stringValue = d.Item1;
                e.FindPropertyRelative("cost").intValue = d.Item2;
                e.FindPropertyRelative("damage").intValue = d.Item3;
            }
            so.ApplyModifiedProperties();

            // 5. 기존 Mallet 은 끈다 (선택한 망치가 그 자리에 들어간다)
            Undo.RecordObject(mallet, "Disable Mallet");
            mallet.SetActive(false);

            // 6. 게임 이름
            so = new SerializedObject(game);
            so.FindProperty("title").stringValue = GameTitle;
            so.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // 7. 시작 패널 (망치 선택 UI 포함)
            CreateStartPanel();

            Selection.activeGameObject = rackGo;
            Debug.Log($"[Blacksmith] 망치 {levels.Count}개 설정 완료. Mallet 참조 {remapped}개 교체. 기준점: {anchor.position:F3}", rackGo);
        }

        private static GameObject FindTemplateMallet(Scene scene, List<GameObject> levels)
        {
            foreach (var go in scene.GetRootGameObjects())
                foreach (var home in go.GetComponentsInChildren<ReturnHome>(true))
                    if (levels.Contains(home.gameObject) == false && home.GetComponentInChildren<MalletHitter>(true) != null)
                        return home.gameObject;
            return null;
        }

        // Mallet 루트의 컴포넌트를 망치 루트로 복사 (Rigidbody 먼저 — XRGrabInteractable 이 요구함)
        private static void CopyRootComponents(GameObject from, GameObject to)
        {
            var comps = from.GetComponents<Component>()
                .Where(c => c != null && (c is Transform) == false)
                .OrderBy(c => c is Rigidbody ? 0 : 1)
                .ToList();
            foreach (var c in comps)
            {
                ComponentUtility.CopyComponent(c);
                var existing = to.GetComponent(c.GetType());
                if (existing != null) ComponentUtility.PasteComponentValues(existing);
                else ComponentUtility.PasteComponentAsNew(to);
            }
            if (to.GetComponent<HammerStats>() == null) Undo.AddComponent<HammerStats>(to);
        }

        /// <summary>
        /// 망치 안의 모든 컴포넌트에서 Mallet 쪽 오브젝트를 가리키는 참조를, 망치 안의 같은 경로 오브젝트로 바꾼다.
        /// Mallet 의 모델(HammerShort.004 등)은 망치의 모델로 대응시킨다.
        /// </summary>
        private static int RemapMalletReferences(GameObject mallet, GameObject hammer)
        {
            var malletModel = FindModel(mallet.transform);
            var hammerModel = FindModel(hammer.transform);
            int count = 0;

            foreach (var comp in hammer.GetComponentsInChildren<Component>(true))
            {
                if (comp == null || comp is Transform) continue;
                var so = new SerializedObject(comp);
                var it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var target = it.objectReferenceValue;
                    if (target == null) continue;
                    var go = target is GameObject g ? g : (target is Component c ? c.gameObject : null);
                    if (go == null || go.scene != hammer.scene || go.transform.IsChildOf(mallet.transform) == false) continue;

                    var mapped = MapObject(target, mallet.transform, hammer.transform, malletModel, hammerModel);
                    if (mapped == null)
                    {
                        Debug.LogWarning($"[Blacksmith] {hammer.name}/{comp.GetType().Name}.{it.propertyPath} 의 Mallet 참조({target.name})에 대응하는 오브젝트가 없어 비웁니다.", comp);
                    }
                    it.objectReferenceValue = mapped;
                    changed = true;
                    count++;
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
            return count;
        }

        private static Object MapObject(Object target, Transform fromRoot, Transform toRoot, Transform fromModel, Transform toModel)
        {
            var go = target is GameObject g ? g : ((Component)target).gameObject;

            Transform dest;
            if (fromModel != null && toModel != null && go.transform.IsChildOf(fromModel))
                dest = go.transform == fromModel ? toModel : toModel.Find(RelativePath(fromModel, go.transform));
            else
                dest = go.transform == fromRoot ? toRoot : toRoot.Find(RelativePath(fromRoot, go.transform));
            if (dest == null) return null;

            if (target is GameObject) return dest.gameObject;
            // 같은 타입 중 같은 순번의 컴포넌트
            var type = target.GetType();
            int index = System.Array.IndexOf(go.GetComponents(type), target);
            var candidates = dest.GetComponents(type);
            if (candidates.Length == 0) candidates = dest.GetComponentsInChildren(type, true); // 모델 구조가 다를 때 (렌더러 등)
            return candidates.Length > 0 ? candidates[Mathf.Clamp(index, 0, candidates.Length - 1)] : null;
        }

        private static string RelativePath(Transform root, Transform t)
        {
            var names = new List<string>();
            for (var c = t; c != null && c != root; c = c.parent) names.Add(c.name);
            names.Reverse();
            return string.Join("/", names);
        }

        // 이름이 Hammer 로 시작하고 렌더러를 가진 모델 오브젝트 (Hammer_LevelN 자신 제외)
        private static Transform FindModel(Transform root)
        {
            foreach (Transform child in root)
                if (child.name.StartsWith("Hammer") && child.GetComponentInChildren<MeshRenderer>(true) != null) return child;
            return null;
        }

        private static Transform CreateAnchor(GameObject mallet, GameObject level1)
        {
            var neck = FindChild(level1.transform, "Neck");
            var levelModel = FindModel(level1.transform);
            var malletModel = FindModel(mallet.transform);
            if (neck == null || levelModel == null || malletModel == null)
                Debug.LogWarning("[Blacksmith] Neck 또는 모델을 찾지 못해 Mallet 위치를 기준점으로 씁니다.");

            // Mallet 모델 자세 × (Level1 모델 → Neck) = Level1 을 Mallet 처럼 걸었을 때의 Neck 자세
            Matrix4x4 m = neck != null && levelModel != null && malletModel != null
                ? malletModel.localToWorldMatrix * levelModel.worldToLocalMatrix * neck.localToWorldMatrix
                : mallet.transform.localToWorldMatrix;

            var stand = FindInScene(mallet.scene, "MalletStand");
            var parent = stand != null ? stand : mallet.transform.parent;
            var old = parent != null ? parent.Find(AnchorName) : null;
            var anchor = old != null ? old : new GameObject(AnchorName).transform;
            if (old == null)
            {
                Undo.RegisterCreatedObjectUndo(anchor.gameObject, "Create Hammer Anchor");
                anchor.SetParent(parent, false);
            }
            anchor.SetPositionAndRotation(m.GetColumn(3), m.rotation);
            return anchor;
        }
    }
}
