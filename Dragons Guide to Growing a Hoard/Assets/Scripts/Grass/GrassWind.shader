// =============================================================
// GrassWind.shader
// -------------------------------------------------------------
// One shader, used for both garden dressing and big fields — only
// the instance count and placement differ between the two; the
// shading and wind motion are identical.
//
// Wind technique: vertex colour's RED channel is a height mask
// baked into the blade mesh (0 at the base, 1 at the tip). The
// vertex shader displaces world position sideways, scaled by that
// mask, so the base stays planted and the tip sways. Two sine
// waves (slow sway + faster flutter) keep it from looking robotic.
//
// GPU Instancing: required for Graphics.DrawMeshInstanced to work.
// Tick "Enable GPU Instancing" on the material in the Inspector.
// =============================================================

Shader "Custom/GrassWind"
{
    Properties
    {
        _MainTex ("Blade Texture (alpha cutout)", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0.15, 0.35, 0.1, 1)
        _TipColor ("Tip Color", Color) = (0.55, 0.75, 0.25, 1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.3

        _WindSpeed ("Wind Speed", Float) = 1.5
        _WindStrength ("Wind Strength", Float) = 0.25
        _WindScale ("Wind Noise Scale", Float) = 0.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "AlphaTest" }
        Cull Off // blades are thin geometry — render both sides

        Pass
        {
            Name "GrassForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float _Cutoff;
                float _WindSpeed;
                float _WindStrength;
                float _WindScale;
                float4 _MainTex_ST;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR; // r = height mask, 0 base -> 1 tip
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float height      : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float heightMask = IN.color.r;

                // Phase keyed off world position so neighbouring blades
                // don't all sway perfectly in sync.
                float phase = (posWS.x + posWS.z) * _WindScale;
                float t = _Time.y * _WindSpeed;

                float sway = sin(t + phase) * 0.7 + sin(t * 2.3 + phase * 1.7) * 0.3;
                posWS.x += sway * _WindStrength * heightMask;
                posWS.z += cos(t * 0.8 + phase) * _WindStrength * 0.5 * heightMask;

                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.height = heightMask;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(tex.a - _Cutoff);

                half4 col = lerp(_BaseColor, _TipColor, IN.height);
                return col * tex;
            }
            ENDHLSL
        }
    }
}
