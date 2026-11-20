using UnityEngine;

namespace StudyXR.Blacksmith
{
    /// <summary>
    /// 한 번 재생하고 사라지는 3D 효과음.
    /// AudioSource.PlayClipAtPoint 는 볼륨이 1 로 잘리므로, PlayOneShot 의 배율로 1 보다 크게(증폭) 낼 수 있게 한다.
    /// </summary>
    public static class Sfx
    {
        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null || volume <= 0f) return;
            var go = new GameObject("One shot audio");
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.spatialBlend = 1f;
            source.PlayOneShot(clip, volume);
            Object.Destroy(go, clip.length * (Time.timeScale < 0.01f ? 0.01f : Time.timeScale) + 0.1f);
        }
    }
}
