using UnityEngine;

namespace StudyXR.Blacksmith
{
    /// <summary>
    /// 저장 데이터(PlayerPrefs) 버전. 게임이 시작될 때(씬 로드 전) 저장된 버전이 Current 보다 낮으면
    /// 코인·해금·광물·의뢰·강화 등 모든 저장 데이터를 지우고 처음 하는 상태로 만든다.
    /// 모든 PC/기기의 진행 상황을 한 번 초기화하고 싶으면 Current 를 1 올린다.
    /// </summary>
    public static class SaveDataVersion
    {
        public const int Current = 1;
        private const string Key = "Blacksmith.SaveVersion";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CheckOnStart()
        {
            if (PlayerPrefs.GetInt(Key, 0) >= Current) return;
            ResetAll();
        }

        /// <summary>모든 저장 데이터를 지우고 현재 버전을 기록한다.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.SetInt(Key, Current);
            PlayerPrefs.Save();
            Debug.Log($"[SaveDataVersion] 저장 데이터를 초기화했습니다 (버전 {Current}).");
        }
    }
}
