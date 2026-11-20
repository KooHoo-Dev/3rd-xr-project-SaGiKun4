// 광맥 표면에 금 간 무늬를 덧그리는 오버레이 셰이더 (URP).
// 메시에 UV 와 상관없이 오브젝트 공간 트라이플래너로 무늬를 입힌다.
// _CrackTex : R = 이 값 이상으로 _Crack 이 올라가면 드러나는 금 (0~1), G = 금 선의 진하기
// _Crack    : 0 = 금 없음, 1 = 전부 드러남. OreVein 이 타격 수 / 필요 타격 수로 설정한다.
Shader "StudyXR/OreCrack"
{
    Properties
    {
        [NoScaleOffset] _CrackTex ("Crack Mask (R reveal, G line)", 2D) = "black" {}
        _CrackColor ("Crack Color", Color) = (0.05, 0.035, 0.025, 1)
        _Crack ("Crack Amount", Range(0, 1)) = 0
        _Tiling ("Tiling (per object unit)", Float) = 1.2
        _Softness ("Reveal Softness", Range(0.001, 0.2)) = 0.03
        _Opacity ("Opacity", Range(0, 1)) = 0.95
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "OreCrack"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1   // 같은 메시 위에 그리므로 Z-fighting 방지

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CrackTex);
            SAMPLER(sampler_CrackTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _CrackColor;
                float _Crack;
                float _Tiling;
                float _Softness;
                float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalOS   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.positionOS = v.positionOS.xyz;
                o.normalOS = v.normalOS;
                return o;
            }

            half CrackAlpha(float2 uv)
            {
                half2 m = SAMPLE_TEXTURE2D(_CrackTex, sampler_CrackTex, uv).rg;
                half reveal = saturate((_Crack - m.r) / _Softness + 1.0);
                return m.g * reveal;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 w = abs(normalize(i.normalOS));
                w = w * w * w * w;
                w /= max(1e-4, w.x + w.y + w.z);

                float3 p = i.positionOS * _Tiling;
                half a = CrackAlpha(p.yz) * w.x
                       + CrackAlpha(p.xz) * w.y
                       + CrackAlpha(p.xy) * w.z;

                a *= _Opacity * step(0.001, _Crack);
                return half4(_CrackColor.rgb, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
