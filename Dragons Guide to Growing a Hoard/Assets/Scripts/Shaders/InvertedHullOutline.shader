// =============================================================
// InvertedHullOutline.shader
// -------------------------------------------------------------
// Classic "inverted hull" outline, written for URP.
//
// HOW IT WORKS:
//   Add this as a SECOND material on a renderer (alongside its
//   normal material). It pushes vertices outward along their
//   normals, flips culling so only the back faces draw, and
//   fills them with a flat color — the result is a silhouette
//   rim around the original mesh.
//
// WHY THIS APPROACH (vs a URP Renderer Feature):
//   No Renderer Data asset, no extra layers, no global render
//   pass setup. It's just a material you add/remove per-object
//   at runtime, which is what you want for "highlight whichever
//   plant is currently being interacted with."
// =============================================================

Shader "Custom/InvertedHullOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.85, 0.2, 1)
        _OutlineWidth ("Outline Width", Range(0.0, 0.1)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "Outline"

            // Only render the back faces of the expanded hull —
            // this is what produces the rim instead of a blob.
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // Push the vertex outward along its object-space normal.
                float3 expandedPosOS = IN.positionOS.xyz + normalize(IN.normalOS) * _OutlineWidth;

                OUT.positionCS = TransformObjectToHClip(expandedPosOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
