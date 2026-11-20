using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudyXR.PiggySmash.EditorTools
{
    public static partial class PiggySmashSetup
    {
        /// <summary>게임 루트에 GrabDistanceLimiter 를 붙인다 (씬의 모든 손에 적용).</summary>
        [MenuItem("Tools/Piggy Smash/Add Grab Distance Limiter (Open Scene)")]
        public static void AddGrabDistanceLimiter()
        {
            var scene = SceneManager.GetActiveScene();
            var game = Object.FindAnyObjectByType<PiggySmashGame>(FindObjectsInactive.Include);
            if (game == null)
            {
                Fail("열린 씬에 PiggySmashGame 이 없습니다.");
                return;
            }

            var limiter = game.GetComponent<GrabDistanceLimiter>();
            if (limiter == null) limiter = Undo.AddComponent<GrabDistanceLimiter>(game.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = game.gameObject;
            EditorGUIUtility.PingObject(limiter);
            Debug.Log("[Blacksmith] GrabDistanceLimiter 추가. 인스펙터에서 최소·최대 거리를 조절하세요.", limiter);
        }
    }
}
