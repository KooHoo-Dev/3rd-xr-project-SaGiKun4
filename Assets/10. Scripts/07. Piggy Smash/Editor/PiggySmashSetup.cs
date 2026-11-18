using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Content.Interaction;

namespace StudyXR.PiggySmash.EditorTools
{
    /// <summary>
    /// 저금통 부수기 게임 배치 도구.
    /// - Convert Lab Scene: 랩 씬 복사본(Piggy Smash.unity)에서 PiggyBankExample 만 남기고 나머지를 지운 뒤 게임 로직을 붙인다.
    /// - Build Game In Open Scene: 열린 씬의 선택한 테이블(없으면 새 큐브) 위에 게임을 배치한다.
    /// </summary>
    public static class PiggySmashSetup
    {
        private const string PiggyPath = "Assets/XRI_Examples/Props/Prefabs/PiggyBank_Breakable.prefab";
        private const string MalletPath = "Assets/XRI_Examples/GrabInteractables/Prefabs/Mallet.prefab";
        private const string GameScenePath = "Assets/Scenes/Piggy Smash.unity";
        private const string DestroyerTag = "Destroyer";

        // 랩 씬에서 남길 루트 오브젝트
        private static readonly string[] KeepRoots =
        {
            "Directional Light", "XR", "Complete XR Origin Set Up Variant", "Post-process Volume",
        };

        // ───────────────────────── 랩 씬 변환 ─────────────────────────

        [MenuItem("Tools/Piggy Smash/Convert Lab Scene (Piggy Smash.unity)")]
        public static void ConvertLabScene()
        {
            EnsureTag(DestroyerTag);
            var piggyPrefab = LoadPiggyPrefab();
            if (piggyPrefab == null) return;

            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            Transform example = FindInScene(scene, "PiggyBankExample");
            if (example == null)
            {
                Fail("씬에서 PiggyBankExample 을 찾지 못했습니다.");
                return;
            }

            // Zone 2 의 바닥(Station). 이미 변환된 씬이면 루트에 있다.
            Transform zone = example.parent != null ? example.parent.parent : null;
            Transform station = zone != null ? zone.Find("Station") : null;
            if (station == null)
                station = scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == "Station");

            if (example.parent != null) example.SetParent(null, true);
            if (station != null && station.parent != null) station.SetParent(null, true);

            // 1. 필요 없는 루트 삭제
            var keep = new HashSet<GameObject>(KeepRootsIn(scene)) { example.gameObject };
            if (station != null) keep.Add(station.gameObject);
            foreach (var go in scene.GetRootGameObjects())
                if (keep.Contains(go) == false) Object.DestroyImmediate(go);

            // 2. 빠진 스크립트(StudyXR.Common.Respawnable, ScreenFade 등) 제거
            int removed = 0;
            foreach (var go in scene.GetRootGameObjects())
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

            // 3. 스폰 위치: 원래 놓여 있던 저금통 자리 (재실행이면 이전 스폰 지점)
            Pose spawnPose = new Pose(example.position + Vector3.up * 1f, example.rotation);
            Transform placedPiggy = example.Cast<Transform>().FirstOrDefault(c => c.GetComponent<Breakable>() != null);
            Transform oldGame = example.Find("Piggy Smash Game");
            Transform oldPoint = oldGame != null ? FindChild(oldGame, "Spawn Point") : null;
            if (placedPiggy != null) spawnPose = new Pose(placedPiggy.position + Vector3.up * 0.02f, placedPiggy.rotation);
            else if (oldPoint != null) spawnPose = new Pose(oldPoint.position, oldPoint.rotation);
            if (placedPiggy != null) Object.DestroyImmediate(placedPiggy.gameObject);
            if (oldGame != null) Object.DestroyImmediate(oldGame.gameObject);

            // 4. 망치: 태그 확인 + 떨어뜨리면 거치대로 복귀
            var mallet = example.Find("Mallet");
            if (mallet != null)
            {
                if (mallet.GetComponent<ReturnHome>() == null) mallet.gameObject.AddComponent<ReturnHome>();
                SetupHeadHitter(mallet);
            }

            // 5. 테이블 범위 (망치·거치대 제외 — 점수판 높이 기준)
            var table = example.Find("Table");
            Bounds area;
            if (table == null || TryGetBounds(table.gameObject, out area) == false)
                TryGetBounds(example.gameObject, out area);

            // 6. 플레이어 위치: 테이블에서 Station 중앙 쪽으로 떨어진 곳에서 테이블을 바라보게
            Vector3 toPlayer = station != null ? Flat(station.position - area.center) : Vector3.back;
            var origin = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Complete XR Origin Set Up Variant");
            Vector3 viewer = area.center + toPlayer * 2f;
            if (origin != null)
            {
                // 카메라 아래의 Screen Fade (머티리얼이 없는 페이드 판) 제거
                var fade = FindChild(origin.transform, "Screen Fade");
                if (fade != null) TryDestroy(fade.gameObject);

                float halfDepth = Mathf.Abs(Vector3.Dot(area.extents, new Vector3(Mathf.Abs(toPlayer.x), 0f, Mathf.Abs(toPlayer.z))));
                Vector3 pos = area.center + toPlayer * (halfDepth + 0.6f);
                pos.y = example.position.y; // 테이블이 놓인 바닥 높이

                // 루트가 아니라 실제 리그(XROrigin 컴포넌트)를 옮긴다.
                // 랩 씬에서 리그 자식이 루트 기준으로 따로 이동·회전돼 있어서 루트만 옮기면 엉뚱한 곳에 선다.
                var rig = origin.GetComponentsInChildren<Component>(true)
                    .FirstOrDefault(c => c != null && c.GetType().Name == "XROrigin");
                Transform rigTransform = rig != null ? rig.transform : origin.transform;
                rigTransform.SetPositionAndRotation(pos, Quaternion.LookRotation(-toPlayer));
                PrefabUtility.RecordPrefabInstancePropertyModifications(rigTransform);
                viewer = pos;
            }

            // 7. 게임 로직 배치
            var gameRoot = CreateGame(example, area, spawnPose, viewer, piggyPrefab);

            // 8. 스카이박스: 원래 Synty 스카이박스는 이 프로젝트에 없으므로 기본값으로
            if (RenderSettings.skybox == null)
                RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddToBuildSettingsFirst(GameScenePath);

            Debug.Log($"[Piggy Smash] 변환 완료. 빠진 스크립트 {removed}개 제거. " +
                      $"Rig={viewer:F2}, " +
                      $"Spawn={spawnPose.position:F2}, Table={area.center:F2}/{area.size:F2}, Game={gameRoot.transform.position:F2}");
        }

        // ───────────────────────── 망치 헤드 타격 판정 ─────────────────────────

        [MenuItem("Tools/Piggy Smash/Setup Mallet Head Hitter (Open Scene)")]
        public static void SetupMalletsInOpenScene()
        {
            EnsureTag(DestroyerTag);
            var scene = SceneManager.GetActiveScene();
            int count = 0;
            foreach (var go in scene.GetRootGameObjects())
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Mallet" && t.GetComponent<Rigidbody>() != null && SetupHeadHitter(t)) count++;

            if (count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[Piggy Smash] 망치 {count}개에 Head Hitter 를 설정했습니다.");
        }

        /// <summary>
        /// 망치 헤드의 Box Collider 를 복제해 'Head Hitter' 자식으로 분리한다.
        /// Head Hitter: Kinematic Rigidbody + Box Collider + MalletHitter + Destroyer 태그 (운동량이 클 때만 켜짐)
        /// 망치 본체: Destroyer 태그 제거 → 그냥 닿기만 해서는 부서지지 않음.
        /// 원래 헤드 Box 는 물리용(테이블 위에 놓이고 저금통을 밀기)으로 그대로 둔다.
        /// </summary>
        private static bool SetupHeadHitter(Transform mallet)
        {
            var collision = FindChild(mallet, "Collision");
            var headBox = collision != null ? collision.GetComponent<BoxCollider>() : mallet.GetComponentInChildren<BoxCollider>(true);
            if (headBox == null)
            {
                Debug.LogWarning($"[Piggy Smash] {mallet.name} 에서 헤드 Box Collider 를 찾지 못했습니다.", mallet);
                return false;
            }

            // 본체는 더 이상 '부수는 물체'가 아님
            mallet.gameObject.tag = "Untagged";
            PrefabUtility.RecordPrefabInstancePropertyModifications(mallet.gameObject);

            // 기존 Head Hitter 가 있으면 다시 만든다
            var old = FindChild(mallet, "Head Hitter");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var hitter = new GameObject("Head Hitter");
            hitter.tag = DestroyerTag;
            hitter.layer = headBox.gameObject.layer;
            hitter.transform.SetParent(headBox.transform, false);

            var box = hitter.AddComponent<BoxCollider>();
            box.center = headBox.center;
            box.size = headBox.size * 1.1f; // 물리용 헤드보다 살짝 크게 → 먼저 닿도록
            box.enabled = false;

            var rb = hitter.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var mh = hitter.AddComponent<MalletHitter>();
            var so = new SerializedObject(mh);
            so.FindProperty("hitCollider").objectReferenceValue = box;
            so.FindProperty("ownerBody").objectReferenceValue = mallet.GetComponent<Rigidbody>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (var t in parent.GetComponentsInChildren<Transform>(true))
                if (t != parent && t.name == name) return t;
            return null;
        }

        // 배치 모드 실행용: Unity.exe -batchmode -executeMethod StudyXR.PiggySmash.EditorTools.PiggySmashSetup.ConvertLabSceneBatch
        public static void ConvertLabSceneBatch()
        {
            ConvertLabScene();
            AssetDatabase.SaveAssets();
        }

        // ───────────────────────── 아무 씬에 배치 ─────────────────────────

        [MenuItem("Tools/Piggy Smash/Build Game In Open Scene")]
        public static void BuildInOpenScene()
        {
            EnsureTag(DestroyerTag);
            var piggyPrefab = LoadPiggyPrefab();
            var malletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MalletPath);
            if (piggyPrefab == null || malletPrefab == null)
            {
                Fail("PiggyBank_Breakable 또는 Mallet 프리팹을 찾지 못했습니다.");
                return;
            }

            Undo.SetCurrentGroupName("Build Piggy Smash");
            int group = Undo.GetCurrentGroup();

            GameObject table = Selection.activeGameObject;
            if (table == null || TryGetBounds(table, out _) == false)
            {
                table = GameObject.CreatePrimitive(PrimitiveType.Cube);
                table.name = "Piggy Table";
                table.transform.position = new Vector3(0f, 0.4f, 1.0f);
                table.transform.localScale = new Vector3(1.2f, 0.8f, 0.7f);
                Undo.RegisterCreatedObjectUndo(table, "Create Table");
            }
            TryGetBounds(table, out Bounds tb);
            Vector3 top = new Vector3(tb.center.x, tb.max.y, tb.center.z);

            var malletGo = (GameObject)PrefabUtility.InstantiatePrefab(malletPrefab);
            Undo.RegisterCreatedObjectUndo(malletGo, "Create Mallet");
            malletGo.transform.position = top + new Vector3(Mathf.Min(0.4f, tb.extents.x * 0.7f), 0.1f, -tb.extents.z * 0.4f);
            malletGo.tag = DestroyerTag;
            if (malletGo.GetComponent<ReturnHome>() == null) malletGo.AddComponent<ReturnHome>();

            var cam = Camera.main;
            Vector3 viewer = cam != null ? cam.transform.position : top - Vector3.forward * 2f;
            var spawnPose = new Pose(top + Vector3.up * 0.05f, Quaternion.LookRotation(Flat(viewer - top)));
            var root = CreateGame(null, tb, spawnPose, viewer, piggyPrefab);
            Undo.RegisterCreatedObjectUndo(root, "Create Game");

            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = root;
            Debug.Log("[Piggy Smash] 배치 완료. 씬에 XR Origin 이 있는지 확인하세요.", root);
        }

        // ───────────────────────── 공통 ─────────────────────────

        /// <summary>게임 매니저 + 점수판 + 스포너를 만들고 서로 연결한다.</summary>
        private static GameObject CreateGame(Transform parent, Bounds area, Pose spawnPose, Vector3 viewer, Breakable piggyPrefab)
        {
            var root = new GameObject("Piggy Smash Game");
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(area.center.x, area.min.y, area.center.z);
            var game = root.AddComponent<PiggySmashGame>();

            // 점수판: 플레이어 반대편, 테이블 위쪽에서 플레이어를 바라보게
            Vector3 away = Flat(area.center - viewer);
            var boardGo = new GameObject("Score Board");
            boardGo.transform.SetParent(root.transform, false);
            float back = Mathf.Abs(Vector3.Dot(area.extents, new Vector3(Mathf.Abs(away.x), 0f, Mathf.Abs(away.z))));
            boardGo.transform.position = new Vector3(area.center.x, area.max.y + 0.6f, area.center.z) + away * (back + 0.1f);
            boardGo.transform.rotation = Quaternion.LookRotation(away); // TMP 는 +Z 방향으로 바라볼 때 읽힘
            var board = boardGo.AddComponent<TextMeshPro>();
            board.rectTransform.sizeDelta = new Vector2(1.4f, 0.7f);
            board.fontSize = 2.2f;
            board.alignment = TextAlignmentOptions.Center;
            board.text = "PIGGY SMASH";

            var spawnerGo = new GameObject("Piggy Spawner");
            spawnerGo.transform.SetParent(root.transform, false);
            var point = new GameObject("Spawn Point").transform;
            point.SetParent(spawnerGo.transform, false);
            point.SetPositionAndRotation(spawnPose.position, spawnPose.rotation);
            var spawner = spawnerGo.AddComponent<OreSpawner>();

            var so = new SerializedObject(game);
            so.FindProperty("boardText").objectReferenceValue = board;
            so.ApplyModifiedPropertiesWithoutUndo();

            so = new SerializedObject(spawner);
            so.FindProperty("game").objectReferenceValue = game;
            var points = so.FindProperty("spawnPoints");
            points.arraySize = 1;
            points.GetArrayElementAtIndex(0).objectReferenceValue = point;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static Breakable LoadPiggyPrefab()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PiggyPath);
            var breakable = go != null ? go.GetComponent<Breakable>() : null;
            if (breakable == null) Fail("PiggyBank_Breakable 프리팹을 찾지 못했습니다: " + PiggyPath);
            return breakable;
        }

        private static IEnumerable<GameObject> KeepRootsIn(Scene scene)
        {
            return scene.GetRootGameObjects().Where(g => KeepRoots.Contains(g.name));
        }

        private static Transform FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            return null;
        }

        private static void TryDestroy(GameObject go)
        {
            try { Object.DestroyImmediate(go); }
            catch (System.Exception e)
            {
                // 프리팹에 포함된 오브젝트라 지울 수 없으면 비활성화
                Debug.LogWarning($"[Piggy Smash] {go.name} 삭제 실패, 비활성화합니다: {e.Message}");
                go.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(go);
            }
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized;
        }

        private static bool TryGetBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var c in go.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger) continue;
                if (found == false) { bounds = c.bounds; found = true; }
                else bounds.Encapsulate(c.bounds);
            }
            if (found) return true;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (found == false) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return found;
        }

        private static void AddToBuildSettingsFirst(string path)
        {
            var list = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        private static void EnsureTag(string tag)
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset == null || asset.Length == 0) return;
            var tagManager = new SerializedObject(asset[0]);
            var tags = tagManager.FindProperty("tags");
            for (int i = 0; i < tags.arraySize; ++i)
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;

            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedProperties();
            Debug.Log($"[Piggy Smash] 태그 '{tag}' 를 추가했습니다.");
        }

        private static void Fail(string message)
        {
            Debug.LogError("[Piggy Smash] " + message);
            if (Application.isBatchMode == false) EditorUtility.DisplayDialog("Piggy Smash", message, "확인");
        }
    }
}
