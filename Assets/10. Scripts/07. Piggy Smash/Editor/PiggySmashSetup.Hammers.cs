using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudyXR.PiggySmash.EditorTools
{
    /// <summary>
    /// 망치 해금(강화)용 프리팹 생성.
    /// 씬의 현재 망치(Mallet: 잡기·물리·Head Hitter 가 설정된 것)를 템플릿으로,
    /// Stylized Fantasy Armory 의 망치 모델마다 같은 구성의 프리팹을 만들고 콜라이더를 모델 크기에 맞춘다.
    /// </summary>
    public static partial class PiggySmashSetup
    {
        private const string HammerModelFolder = "Assets/Daniel Mistage/Stylized Fantasy Armory/Prefabs/Weapons and Shields/Hammers";
        private const string HammerOutFolder = "Assets/10. Scripts/07. Piggy Smash/Hammers";
        private const string CatalogPath = HammerOutFolder + "/HammerCatalog.asset";

        // 만들 망치 4종 (id = 모델 프리팹 이름, 표시 이름, 가격, 공격력, 범위 m). 첫 줄이 기본 망치(무료).
        // 카탈로그에 이미 있는 항목의 가격·성능은 덮어쓰지 않는다 → 인스펙터에서 조정한 값 유지.
        private static readonly (string id, string name, int cost, int damage, float radius)[] DefaultStats =
        {
            ("HammerShort.004", "Iron Hammer",  0,   1, 0f),
            ("HammerShort.002", "War Hammer",   150, 2, 0f),
            ("HammerLong.001",  "Great Hammer", 400, 2, 0.3f),
            ("HammerLong.004",  "Titan Hammer", 900, 4, 0.4f),
        };

        /// <summary>
        /// 망치 프리팹 4종 + HammerCatalog 를 만든다. 씬은 건드리지 않는다.
        /// 씬에 붙일 때: HammerProgress(카탈로그 연결) 하나 + 선반 자리마다 HammerSlot(hammerId 지정).
        /// </summary>
        [MenuItem("Tools/Piggy Smash/Build Hammer Prefabs (Open Scene)")]
        public static void BuildHammerPrefabs()
        {
            var scene = SceneManager.GetActiveScene();
            var mallet = FindSceneHammer(scene);
            if (mallet == null)
            {
                Fail("열린 씬에서 템플릿 망치(ReturnHome + Head Hitter)를 찾지 못했습니다. Piggy Smash 씬을 여세요.");
                return;
            }

            var templateVisual = FindHammerVisual(mallet.transform);
            if (templateVisual == null)
            {
                Fail("현재 망치에서 Hammer 모델(이름이 Hammer 로 시작하는 MeshRenderer)을 찾지 못했습니다.");
                return;
            }
            string templateId = StripCloneName(templateVisual.name);

            EnsureFolder(HammerOutFolder);
            var catalog = LoadOrCreateCatalog();
            var list = catalog.EditableList;

            int built = 0;
            foreach (var d in DefaultStats)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{HammerModelFolder}/{d.id}.prefab");
                if (model == null)
                {
                    Debug.LogWarning($"[Piggy Smash] 망치 모델을 찾지 못했습니다: {d.id}");
                    continue;
                }
                // 템플릿과 같은 모델은 직접 맞춰 둔 콜라이더를 그대로 쓴다
                var prefab = BuildHammerPrefab(mallet, templateVisual, model, refit: d.id != templateId);
                if (prefab == null) continue;
                built++;

                var entry = list.FirstOrDefault(e => e.id == d.id);
                if (entry == null)
                {
                    entry = new HammerCatalog.Entry { id = d.id, displayName = d.name, cost = d.cost, damage = d.damage, splashRadius = d.radius };
                    list.Add(entry);
                }
                entry.prefab = prefab;
            }

            // 표 순서대로 정렬 (표에 없는 항목은 뒤로)
            var order = DefaultStats.Select(s => s.id).ToList();
            var sorted = list.OrderBy(e => order.IndexOf(e.id) < 0 ? 999 : order.IndexOf(e.id)).ToList();
            list.Clear();
            list.AddRange(sorted);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Selection.activeObject = catalog;
            Debug.Log($"[Piggy Smash] 망치 {built}개 프리팹 생성 → {HammerOutFolder}. 가격·성능은 HammerCatalog 에서 조정하세요.", catalog);
        }

        // ───────────────────────── 프리팹 생성 ─────────────────────────

        private static GameObject BuildHammerPrefab(GameObject mallet, Transform templateVisual, GameObject model, bool refit)
        {
            string id = model.name;
            // 씬 망치를 복제 (Object.Instantiate → 프리팹 연결이 끊긴 일반 오브젝트)
            var temp = Object.Instantiate(mallet);
            temp.name = "Hammer_" + id;
            try
            {
                temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var oldVisual = FindHammerVisual(temp.transform);
                var oldRenderer = oldVisual.GetComponent<Renderer>();
                var oldFilter = oldVisual.GetComponent<MeshFilter>();

                // 모델 교체: 같은 부모·같은 로컬 자세
                var visual = Object.Instantiate(model, oldVisual.parent, false);
                visual.name = id;
                visual.transform.localPosition = oldVisual.localPosition;
                visual.transform.localRotation = oldVisual.localRotation;
                visual.transform.localScale = oldVisual.localScale;
                visual.transform.SetSiblingIndex(oldVisual.GetSiblingIndex());
                foreach (var c in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c); // 모델 전체를 감싼 박스는 사용 안 함

                var newRenderer = visual.GetComponentInChildren<Renderer>();
                var newFilter = visual.GetComponentInChildren<MeshFilter>();
                var map = new Dictionary<Object, Object>
                {
                    { oldVisual.gameObject, visual },
                    { oldVisual, visual.transform },
                };
                if (oldRenderer != null) map[oldRenderer] = newRenderer;
                if (oldFilter != null) map[oldFilter] = newFilter;
                RemapReferences(temp, map);
                Object.DestroyImmediate(oldVisual.gameObject);

                if (refit) FitColliders(temp, newFilter);

                var stats = temp.GetComponent<HammerStats>();
                if (stats == null) stats = temp.AddComponent<HammerStats>();
                stats.Setup(id, 1, 0f); // 실제 값은 런타임에 카탈로그에서 적용

                string path = $"{HammerOutFolder}/Hammer_{id}.prefab";
                return PrefabUtility.SaveAsPrefabAsset(temp, path);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Piggy Smash] {id} 프리팹 생성 실패: {e}");
                return null;
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        /// <summary>
        /// 모델 메시를 망치 길이 방향으로 잘라 보고, 넓은 쪽 끝을 헤드로 판단한다.
        /// - 손잡이: CapsuleCollider (Collision 오브젝트)
        /// - 헤드: Head Hitter 의 BoxCollider (타격 판정, 물리 헤드보다 10% 크게)
        /// </summary>
        private static void FitColliders(GameObject hammer, MeshFilter filter)
        {
            if (filter == null || filter.sharedMesh == null) return;
            var root = hammer.transform;

            // 루트 기준 정점
            var mesh = filter.sharedMesh;
            var verts = mesh.vertices;
            var points = new List<Vector3>(verts.Length);
            if (verts.Length > 0)
                foreach (var v in verts) points.Add(root.InverseTransformPoint(filter.transform.TransformPoint(v)));
            else
            {
                // 정점을 읽을 수 없으면 바운드 꼭짓점으로 대신
                var b = mesh.bounds;
                for (int i = 0; i < 8; ++i)
                {
                    var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    points.Add(root.InverseTransformPoint(filter.transform.TransformPoint(c)));
                }
            }

            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) bounds.Encapsulate(p);
            int axis = bounds.size.x >= bounds.size.y && bounds.size.x >= bounds.size.z ? 0 : (bounds.size.y >= bounds.size.z ? 1 : 2);
            float min = bounds.min[axis], max = bounds.max[axis], length = max - min;

            // 길이 방향 단면 폭
            const int bins = 40;
            var width = new float[bins];
            var lo = new Vector2[bins];
            var hi = new Vector2[bins];
            for (int i = 0; i < bins; ++i) { lo[i] = Vector2.positiveInfinity; hi[i] = Vector2.negativeInfinity; }
            foreach (var p in points)
            {
                int i = Mathf.Clamp((int)((p[axis] - min) / length * bins), 0, bins - 1);
                var q = Cross(p, axis);
                lo[i] = Vector2.Min(lo[i], q);
                hi[i] = Vector2.Max(hi[i], q);
            }
            for (int i = 0; i < bins; ++i) width[i] = hi[i].x >= lo[i].x ? Mathf.Max(hi[i].x - lo[i].x, hi[i].y - lo[i].y) : 0f;

            // 헤드 방향: 양 끝 20% 중 더 넓은 쪽
            float startW = width.Take(bins / 5).Max(), endW = width.Skip(bins - bins / 5).Max();
            bool headAtMax = endW >= startW;
            float maxW = width.Max();
            int headBins = 0;
            for (int k = 0; k < bins; ++k)
            {
                int i = headAtMax ? bins - 1 - k : k;
                if (width[i] >= maxW * 0.5f || k < 2) headBins++;
                else if (headBins > 0) break;
            }
            float headStart = headAtMax ? max - length * headBins / bins : min + length * headBins / bins;

            var headPts = points.Where(p => headAtMax ? p[axis] >= headStart : p[axis] <= headStart).ToList();
            var head = new Bounds(headPts[0], Vector3.zero);
            foreach (var p in headPts) head.Encapsulate(p);

            // 손잡이 반경: 헤드를 뺀 단면 폭의 중간값
            var handleW = Enumerable.Range(0, bins)
                .Where(i => headAtMax ? i < bins - headBins : i >= headBins)
                .Select(i => width[i]).Where(w => w > 0f).OrderBy(w => w).ToList();
            float radius = Mathf.Clamp(handleW.Count > 0 ? handleW[handleW.Count / 2] * 0.5f : 0.02f, 0.015f, 0.04f);
            float handleFrom = headAtMax ? min : headStart;
            float handleTo = headAtMax ? headStart : max;

            // 손잡이 캡슐
            var capsule = hammer.GetComponentsInChildren<CapsuleCollider>(true).FirstOrDefault();
            if (capsule != null)
            {
                var a = capsule.transform.InverseTransformPoint(root.TransformPoint(AxisPoint(bounds.center, axis, handleFrom)));
                var b = capsule.transform.InverseTransformPoint(root.TransformPoint(AxisPoint(bounds.center, axis, handleTo)));
                var d = b - a;
                int dir = Mathf.Abs(d.x) >= Mathf.Abs(d.y) && Mathf.Abs(d.x) >= Mathf.Abs(d.z) ? 0 : (Mathf.Abs(d.y) >= Mathf.Abs(d.z) ? 1 : 2);
                capsule.direction = dir;
                capsule.center = (a + b) * 0.5f;
                capsule.radius = radius;
                capsule.height = Mathf.Abs(d[dir]) + radius * 2f;
            }

            // 헤드 타격 판정
            var hitter = hammer.GetComponentsInChildren<MalletHitter>(true).FirstOrDefault();
            var box = hitter != null ? hitter.GetComponent<BoxCollider>() : null;
            if (box != null)
            {
                hitter.transform.position = root.TransformPoint(head.center);
                // 헤드 바운드를 Head Hitter 로컬 축으로 옮긴 크기
                var size = Vector3.zero;
                for (int i = 0; i < 3; ++i)
                {
                    var axisWorld = root.TransformVector(Vector3.zero.With(i, head.size[i]));
                    var local = hitter.transform.InverseTransformVector(axisWorld);
                    size += new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
                }
                box.center = Vector3.zero;
                box.size = size * 1.1f;
                box.enabled = false;
            }
        }

        private static Vector3 With(this Vector3 v, int axis, float value)
        {
            v[axis] = value;
            return v;
        }

        private static Vector2 Cross(Vector3 p, int axis)
        {
            return axis == 0 ? new Vector2(p.y, p.z) : (axis == 1 ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y));
        }

        private static Vector3 AxisPoint(Vector3 center, int axis, float value)
        {
            center[axis] = value;
            return center;
        }

        // 복제된 컴포넌트들이 예전 모델(렌더러 등)을 가리키던 참조를 새 모델로 바꾼다 (XRI 하이라이트 등)
        private static void RemapReferences(GameObject root, Dictionary<Object, Object> map)
        {
            foreach (var comp in root.GetComponentsInChildren<Component>(true))
            {
                if (comp == null || comp is Transform) continue;
                var so = new SerializedObject(comp);
                var it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    if (map.TryGetValue(it.objectReferenceValue, out var to) && to != null)
                    {
                        it.objectReferenceValue = to;
                        changed = true;
                    }
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ───────────────────────── 찾기 / 에셋 ─────────────────────────

        private static GameObject FindSceneHammer(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
                foreach (var home in go.GetComponentsInChildren<ReturnHome>(true))
                    if (home.GetComponentInChildren<MalletHitter>(true) != null) return home.gameObject;
            return null;
        }

        private static Transform FindHammerVisual(Transform hammer)
        {
            foreach (var r in hammer.GetComponentsInChildren<MeshRenderer>(true))
                for (var t = r.transform; t != null && t != hammer; t = t.parent)
                    if (t.name.StartsWith("Hammer") && t.GetComponent<MeshFilter>() != null) return t;
            return null;
        }

        private static string StripCloneName(string name)
        {
            return name.Replace("(Clone)", "").Trim();
        }

        private static HammerCatalog LoadOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HammerCatalog>(CatalogPath);
            if (catalog != null) return catalog;
            catalog = ScriptableObject.CreateInstance<HammerCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
