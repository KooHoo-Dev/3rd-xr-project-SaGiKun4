using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StudyXR.PiggySmash
{
    /// <summary>
    /// 망치 모델의 표시 상태.
    /// - 잠금: 모든 머티리얼을 검정 머티리얼로 바꿔 실루엣만 보이게 한다.
    /// - 외곽선: 껍데기 외곽선 머티리얼(HammerHullOutline)을 두 번째 머티리얼로 덧붙이고 _OutlineAmount 로 짧게 켜고 끈다.
    /// HammerRack 이 망치마다 붙여서 쓴다.
    /// </summary>
    public class HammerVisual : MonoBehaviour
    {
        private static readonly int OutlineAmountId = Shader.PropertyToID("_OutlineAmount");

        [SerializeField] private float fadeSeconds = 0.12f;

        private Renderer[] renderers;
        private Material[][] originals;
        private Material lockedMaterial;
        private Material outlineMaterial;
        private MaterialPropertyBlock block;
        private bool locked;
        private bool outlined;
        private float amount;
        private Coroutine fade;

        public bool IsOutlined => outlined;

        public void Init(Material locked, Material outline)
        {
            lockedMaterial = locked;
            outlineMaterial = outline;
            if (renderers != null) return;

            // Head Hitter 등 콜라이더 오브젝트에는 렌더러가 없으므로 모델 렌더러만 잡힌다
            var list = new List<Renderer>();
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true)) list.Add(r);
            renderers = list.ToArray();
            originals = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; ++i) originals[i] = renderers[i].sharedMaterials;
            block = new MaterialPropertyBlock();
        }

        public void SetLocked(bool value)
        {
            locked = value;
            if (locked) outlined = false;
            Rebuild();
        }

        public void SetOutline(bool value)
        {
            if (outlined == value || (locked && value)) return;
            outlined = value;
            if (value) Rebuild(); // 외곽선 머티리얼을 붙인 뒤 키운다
            if (fade != null) StopCoroutine(fade);
            if (isActiveAndEnabled && fadeSeconds > 0f) fade = StartCoroutine(Fade(value ? 1f : 0f));
            else { Apply(value ? 1f : 0f); if (value == false) Rebuild(); }
        }

        // 머티리얼 목록 다시 구성: 원래(또는 검정) + (외곽선이 켜졌거나 줄어드는 중이면) 외곽선
        private void Rebuild()
        {
            if (renderers == null) return;
            bool withOutline = locked == false && outlineMaterial != null && (outlined || amount > 0f);
            for (int i = 0; i < renderers.Length; ++i)
            {
                var src = originals[i];
                var mats = new Material[src.Length + (withOutline ? 1 : 0)];
                for (int m = 0; m < src.Length; ++m)
                    mats[m] = locked && lockedMaterial != null ? lockedMaterial : src[m];
                if (withOutline) mats[src.Length] = outlineMaterial;
                renderers[i].sharedMaterials = mats;
            }
            Apply(amount);
        }

        private IEnumerator Fade(float target)
        {
            float start = amount;
            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                Apply(Mathf.Lerp(start, target, t / fadeSeconds));
                yield return null;
            }
            Apply(target);
            if (target <= 0f) Rebuild(); // 다 꺼지면 외곽선 머티리얼을 떼어 그리는 비용을 없앤다
            fade = null;
        }

        private void Apply(float value)
        {
            amount = value;
            if (renderers == null) return;
            foreach (var r in renderers)
            {
                r.GetPropertyBlock(block);
                block.SetFloat(OutlineAmountId, value);
                r.SetPropertyBlock(block);
            }
        }

        private void OnDisable()
        {
            if (fade != null) StopCoroutine(fade);
            fade = null;
            amount = outlined ? 1f : 0f;
            Rebuild();
        }
    }
}
