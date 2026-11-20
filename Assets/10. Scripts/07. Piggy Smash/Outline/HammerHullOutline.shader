// 껍데기 외곽선 (Inverted Hull) — 외곽선 전용 머티리얼.
// 원본: KooHoo-Dev/edu-lecture-shader-gallery 의 inverted-hull-outline (MIT).
// 원본은 본체 + 외곽선 두 패스를 한 셰이더에 담지만, 망치는 기존 텍스처 머티리얼을 그대로 써야 하므로
// 외곽선 패스만 떼어 '두 번째 머티리얼'로 덧붙여 쓴다 (HammerVisual 이 켜고 끈다).
// 본체보다 조금 키운 껍데기의 뒷면만(Cull Front) 그려, 윤곽 바깥으로 삐져나온 띠가 외곽선이 된다.
Shader "Blacksmith/HammerHullOutline"
{
    Properties
    {
        _OutlineColor ("외곽선 색", Color) = (1, 0.788, 0.302, 1)
        _OutlineWidth ("외곽선 두께 (m)", Range(0, 0.05)) = 0.008
        _OutlineAmount ("외곽선 켜기", Range(0, 1)) = 1
        [Enum(Normal, 0, ScaleFromCenter, 1)] _PushDir ("미는 방식", Float) = 0
        _Center ("확대 기준점 (오브젝트 공간)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Outline"
            // 본체(Lit)와 다른 LightMode 라야 함께 그려진다. URP 17 은 SRPDefaultUnlit 을 Forward/Deferred 모두에서 그린다.
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineAmount;
                float _PushDir;
                float4 _Center;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // 켜기 값이 0 이면 살짝 안쪽으로 넣어 본체 안에 완전히 숨긴다.
                float width = lerp(-0.002, _OutlineWidth, saturate(_OutlineAmount));

                // 법선 방향으로 월드 공간에서 민다 → 두께가 물체 스케일과 무관하게 일정하다.
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(v.normalOS));
                float3 pushed = positionWS + normalWS * width;

                // 각진 메시에서 모서리가 벌어지면 ScaleFromCenter: 기준점에서 바깥으로 민다.
                float3 centerWS = TransformObjectToWorld(_Center.xyz);
                float3 fromCenter = positionWS - centerWS;
                float3 scaled = positionWS + normalize(fromCenter + 1e-5) * width;

                o.positionCS = TransformWorldToHClip(lerp(pushed, scaled, step(0.5, _PushDir)));
                return o;
            }

            half4 frag(Varyings v) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
                // 조명과 상관없는 단색이라 그늘에서도 또렷하다.
                return half4(_OutlineColor.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
