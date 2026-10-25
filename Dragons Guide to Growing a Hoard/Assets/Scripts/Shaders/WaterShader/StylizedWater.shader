// StylizedWater.shader
// Text version of the Shader Graph water: world-space depth fade, refraction, HSV colours,
// horizon fresnel, surface + intersection foam, blended normals, stylized specular, Gerstner waves, fog.
// Target: Unity 6 (6000.x), URP, Forward or Forward+. Requires Depth Texture and Opaque Texture
// enabled on the URP asset, and a perspective camera.

Shader "Custom/StylizedWater"
{
    Properties
    {
        [Header(Depth and Color)]
        _DepthRange ("Depth Range", Float) = 3
        _ShallowColor ("Shallow Color", Color) = (0.30, 0.85, 0.80, 0.50)
        _DeepColor ("Deep Color", Color) = (0.05, 0.25, 0.50, 0.95)
        _HorizonColor ("Horizon Color", Color) = (0.70, 0.90, 1.00, 1.00)
        _HorizonDistance ("Horizon Distance", Float) = 3
        _PosterizeSteps ("Posterize Steps (0 = off)", Float) = 4

        [Header(Refraction)]
        _RefractionStrength ("Refraction Strength", Float) = 0.015
        _RefractionScale ("Refraction Scale", Float) = 10
        _RefractionSpeed ("Refraction Speed", Float) = 0.2

        [Header(Surface Foam)]
        [NoScaleOffset] _FoamTex ("Foam Texture", 2D) = "white" {}
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamTiling ("Foam Tiling (xy)", Vector) = (0.2, 0.2, 0, 0)
        _FoamDirection ("Foam Direction (0-1)", Range(0, 1)) = 0.25
        _FoamSpeed ("Foam Speed", Float) = 0.05
        _FoamCutoff ("Foam Cutoff", Range(0, 1)) = 0.6
        _FoamDistortion ("Foam Distortion", Float) = 1

        [Header(Intersection Foam)]
        [NoScaleOffset] _IntersectionFoamTex ("Intersection Foam Texture", 2D) = "white" {}
        _IntersectionFoamColor ("Intersection Foam Color", Color) = (1, 1, 1, 1)
        _IntersectionFoamTiling ("Intersection Foam Tiling (xy)", Vector) = (0.3, 0.3, 0, 0)
        _IntersectionFoamDirection ("Intersection Foam Direction (0-1)", Range(0, 1)) = 0.6
        _IntersectionFoamSpeed ("Intersection Foam Speed", Float) = 0.05
        _IntersectionFoamFade ("Intersection Foam Fade (range)", Float) = 0.6
        _IntersectionFoamCutoff ("Intersection Foam Cutoff", Range(0, 1)) = 1

        [Header(Normals and Lighting)]
        [NoScaleOffset] _NormalsTex ("Normals Texture", 2D) = "bump" {}
        _NormalsScale ("Normals Scale", Float) = 0.15
        _NormalsSpeed ("Normals Speed", Float) = 0.05
        _NormalsDirection ("Normals Direction (0-1)", Range(0, 1)) = 0.3
        _NormalsStrength ("Normals Strength", Float) = 0.6
        _Smoothness ("Smoothness", Range(0, 1)) = 0.9
        _Hardness ("Additional Lights Hardness", Range(0, 1)) = 0.5
        _MainLightCutoff ("Main Light Cutoff", Range(0, 1)) = 0.5
        _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)

        [Header(Cartoon)]
        _FoamStepFPS ("Foam Frame Rate (0 = smooth)", Float) = 0
        _CrestFoamColor ("Crest Foam Color", Color) = (1, 1, 1, 1)
        _CrestFoamThreshold ("Crest Foam Threshold", Range(0, 1)) = 0.65

        [Header(Player and Object Ripples)]
        _RippleFrequency ("Ripple Frequency", Float) = 6
        _RippleSpeed ("Ripple Speed", Float) = 3
        _RippleHeight ("Ripple Height", Float) = 0.12

        [Header(Waves)]
        _Steepness ("Steepness", Float) = 0.15
        _Wavelength ("Wavelength", Float) = 10
        _WaveSpeed ("Wave Speed", Float) = 1
        _Directions ("Wave Directions (0-1 each)", Vector) = (0.10, 0.35, 0.60, 0.85)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            // Forward+: _CLUSTER_LIGHT_LOOP on Unity 6.1+, _FORWARD_PLUS on Unity 6.0. Only the matching one is ever used.
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_FoamTex);             SAMPLER(sampler_FoamTex);
            TEXTURE2D(_IntersectionFoamTex); SAMPLER(sampler_IntersectionFoamTex);
            TEXTURE2D(_NormalsTex);          SAMPLER(sampler_NormalsTex);

            CBUFFER_START(UnityPerMaterial)
                float _DepthRange;
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _HorizonColor;
                float _HorizonDistance;
                float _PosterizeSteps;

                float _RefractionStrength;
                float _RefractionScale;
                float _RefractionSpeed;

                float4 _FoamColor;
                float4 _FoamTiling;
                float _FoamDirection;
                float _FoamSpeed;
                float _FoamCutoff;
                float _FoamDistortion;

                float4 _IntersectionFoamColor;
                float4 _IntersectionFoamTiling;
                float _IntersectionFoamDirection;
                float _IntersectionFoamSpeed;
                float _IntersectionFoamFade;
                float _IntersectionFoamCutoff;

                float _NormalsScale;
                float _NormalsSpeed;
                float _NormalsDirection;
                float _NormalsStrength;
                float _Smoothness;
                float _Hardness;
                float _MainLightCutoff;
                float4 _SpecularColor;

                float _Steepness;
                float _Wavelength;
                float _WaveSpeed;
                float4 _Directions;

                float _FoamStepFPS;
                float4 _CrestFoamColor;
                float _CrestFoamThreshold;

                float _RippleFrequency;
                float _RippleSpeed;
                float _RippleHeight;
            CBUFFER_END

            // Set every frame by WaterInteractor.cs (global, not per-material)
            #define MAX_WATER_INTERACTORS 16
            float4 _WaterInteractors[MAX_WATER_INTERACTORS];       // xyz = world position, w = radius
            float4 _WaterInteractorParams[MAX_WATER_INTERACTORS];  // x = strength 0..1
            float _WaterInteractorCount;

            // ------------------------------------------------------------------
            // Waves
            // ------------------------------------------------------------------
            #define WATER_PI 3.14159265

            float3 GerstnerWave(float3 position, float steepness, float wavelength, float speed, float direction)
            {
                direction = direction * 2 - 1;
                float2 d = normalize(float2(cos(WATER_PI * direction), sin(WATER_PI * direction)));
                float k = 2 * WATER_PI / wavelength;
                float f = k * (dot(d, position.xz) - speed * _Time.y);
                float a = steepness / k;
                return float3(d.x * (a * cos(f)), a * sin(f), d.y * (a * cos(f)));
            }

            float3 GerstnerWaves(float3 position)
            {
                float3 offset = 0;
                offset += GerstnerWave(position, _Steepness, _Wavelength, _WaveSpeed, _Directions.x);
                offset += GerstnerWave(position, _Steepness, _Wavelength, _WaveSpeed, _Directions.y);
                offset += GerstnerWave(position, _Steepness, _Wavelength, _WaveSpeed, _Directions.z);
                offset += GerstnerWave(position, _Steepness, _Wavelength, _WaveSpeed, _Directions.w);
                return offset;
            }

            // ------------------------------------------------------------------
            // Helpers
            // ------------------------------------------------------------------
            float2 WaterGradientNoiseDir(float2 p)
            {
                p = p % 289;
                float x = (34 * p.x + 1) * p.x % 289 + p.y;
                x = (34 * x + 1) * x % 289;
                x = frac(x / 41) * 2 - 1;
                return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
            }

            float WaterGradientNoise(float2 uv, float scale)
            {
                float2 p = uv * scale;
                float2 ip = floor(p);
                float2 fp = frac(p);
                float d00 = dot(WaterGradientNoiseDir(ip), fp);
                float d01 = dot(WaterGradientNoiseDir(ip + float2(0, 1)), fp - float2(0, 1));
                float d10 = dot(WaterGradientNoiseDir(ip + float2(1, 0)), fp - float2(1, 0));
                float d11 = dot(WaterGradientNoiseDir(ip + float2(1, 1)), fp - float2(1, 1));
                fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
                return lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x) + 0.5;
            }

            float3 WaterRGBToHSV(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 P = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 Q = lerp(float4(P.xyw, c.r), float4(c.r, P.yzx), step(P.x, c.r));
                float D = Q.x - min(Q.w, Q.y);
                float E = 1e-10;
                return float3(abs(Q.z + (Q.w - Q.y) / (6.0 * D + E)), D / (Q.x + E), Q.x);
            }

            float3 WaterHSVToRGB(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 P = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(P - K.xxx), c.y);
            }

            float4 HSVLerp(float4 A, float4 B, float T)
            {
                float3 hsvA = WaterRGBToHSV(A.rgb);
                float3 hsvB = WaterRGBToHSV(B.rgb);
                float dh = hsvB.x - hsvA.x;
                if (dh > 0.5) dh -= 1.0;
                else if (dh < -0.5) dh += 1.0;
                float hue = frac(hsvA.x + dh * T + 1.0);
                float3 hsv = float3(hue, lerp(hsvA.y, hsvB.y, T), lerp(hsvA.z, hsvB.z, T));
                return float4(WaterHSVToRGB(hsv), lerp(A.a, B.a, T));
            }

            float2 PanningUVT(float2 uv, float direction, float speed, float2 tiling, float2 offset, float time)
            {
                float angle = (direction * 2 - 1) * PI;
                float2 dir = float2(cos(angle), sin(angle));
                return uv * tiling + offset + time * speed * dir;
            }

            float2 PanningUV(float2 uv, float direction, float speed, float2 tiling, float2 offset)
            {
                return PanningUVT(uv, direction, speed, tiling, offset, _Time.y);
            }

            // Time that advances in steps (stop-motion look). fps <= 0 gives smooth time.
            float WaterTime(float fps)
            {
                return fps > 0.5 ? floor(_Time.y * fps) / fps : _Time.y;
            }

            float2 DistortUV(float2 uv, float amount, float time)
            {
                uv.y += amount * 0.01 * (sin(uv.x * 3.5 + time * 0.35) + sin(uv.x * 4.8 + time * 1.05) + sin(uv.x * 7.3 + time * 0.45)) / 3.0;
                uv.x += amount * 0.12 * (sin(uv.y * 4.0 + time * 0.50) + sin(uv.y * 6.8 + time * 0.75) + sin(uv.y * 11.3 + time * 0.2)) / 3.0;
                uv.y += amount * 0.12 * (sin(uv.x * 4.2 + time * 0.64) + sin(uv.x * 6.3 + time * 1.65) + sin(uv.x * 8.2 + time * 0.45)) / 3.0;
                return uv;
            }

            float3 BlendOverlay(float3 baseColor, float3 blendColor, float opacity)
            {
                float3 low = 2 * baseColor * blendColor;
                float3 high = 1 - 2 * (1 - baseColor) * (1 - blendColor);
                float3 result = lerp(low, high, step(0.5, baseColor));
                return lerp(baseColor, result, opacity);
            }

            // World position of the ground visible under the water at a given screen UV.
            float3 ScenePositionWS(float2 uv, float3 positionWS)
            {
                float sceneEye = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float surfaceEye = -TransformWorldToView(positionWS).z;
                float3 camPos = _WorldSpaceCameraPos;
                return camPos + (positionWS - camPos) * (sceneEye / surfaceEye);
            }

            // 1 in shallow water / at shorelines, 0 in deep water.
            float WorldDepthFade(float2 uv, float3 positionWS, float range)
            {
                float3 scenePos = ScenePositionWS(uv, positionWS);
                return 1 - saturate((positionWS.y - scenePos.y) / range);
            }

            float LightingSpecular(float3 L, float3 N, float3 V, float smoothness)
            {
                float3 H = SafeNormalize(L + V);
                return pow(saturate(dot(N, H)), smoothness);
            }

            float3 LightSpecular(Light light, float3 N, float3 V, float smoothness, float hardness)
            {
                float3 attenuatedLight = light.color * light.distanceAttenuation * light.shadowAttenuation;
                float soft = LightingSpecular(light.direction, N, V, smoothness);
                float hard = smoothstep(0.005, 0.01, soft);
                return lerp(soft, hard, hardness) * attenuatedLight;
            }

            // The variable must be called "inputData": LIGHT_LOOP_BEGIN needs it in scope for Forward+.
            float3 AdditionalSpecular(InputData inputData, float smoothness, float hardness)
            {
                float3 specular = 0;

                #if USE_CLUSTER_LIGHT_LOOP || USE_FORWARD_PLUS
                // Extra directional lights are handled outside the clustered loop.
                UNITY_LOOP for (uint d = 0; d < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); d++)
                {
                    Light dirLight = GetAdditionalLight(d, inputData.positionWS, half4(1, 1, 1, 1));
                    specular += LightSpecular(dirLight, inputData.normalWS, inputData.viewDirectionWS, smoothness, hardness);
                }
                #endif

                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData.positionWS, half4(1, 1, 1, 1));
                    specular += LightSpecular(light, inputData.normalWS, inputData.viewDirectionWS, smoothness, hardness);
                LIGHT_LOOP_END

                return specular;
            }

            // ------------------------------------------------------------------
            // Vertex / fragment
            // ------------------------------------------------------------------
            // x = ripple height, y = foam amount (0..1) from all active interactors at this XZ position.
            float2 WaterInteraction(float2 posXZ)
            {
                float height = 0;
                float foam = 0;
                int count = (int)_WaterInteractorCount;
                for (int i = 0; i < MAX_WATER_INTERACTORS; i++)
                {
                    if (i >= count) break;
                    float4 data = _WaterInteractors[i];
                    float strength = _WaterInteractorParams[i].x;
                    float d = distance(posXZ, data.xz);
                    float falloff = saturate(1 - d / max(data.w, 0.001));
                    falloff *= falloff;
                    height += sin(d * _RippleFrequency - _Time.y * _RippleSpeed) * falloff * strength * _RippleHeight;
                    foam += falloff * strength;
                }
                return float2(height, saturate(foam));
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 restXZ     : TEXCOORD1; // world XZ before wave displacement, for stable texture UVs
                half   fogFactor  : TEXCOORD2;
                float  waveHeight : TEXCOORD3; // Gerstner height, used for crest foam
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.restXZ = positionWS.xz;
                float3 waveOffset = GerstnerWaves(positionWS);
                positionWS += waveOffset;
                positionWS.y += WaterInteraction(OUT.restXZ).x; // ripples from players / objects
                OUT.waveHeight = waveOffset.y;

                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 positionWS = IN.positionWS;
                float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);

                // ---- Refraction (with the "object sticking out of the water" fix) ----
                float noise = WaterGradientNoise(screenUV + _Time.y * _RefractionSpeed, _RefractionScale);
                float2 refractedUV = screenUV + (noise * 2.0 - 1.0) * _RefractionStrength;
                float3 refractedScenePos = ScenePositionWS(refractedUV, positionWS);
                float2 sceneUV = (refractedScenePos.y > positionWS.y) ? screenUV : refractedUV;

                // ---- Depth, colour, horizon, underwater ----
                float depth = WorldDepthFade(sceneUV, positionWS, _DepthRange);
                if (_PosterizeSteps > 0.5) depth = floor(depth * _PosterizeSteps) / _PosterizeSteps;

                float4 depthColor = HSVLerp(_DeepColor, _ShallowColor, depth);
                float fresnel = pow(1 - saturate(dot(float3(0, 1, 0), viewDirWS)), _HorizonDistance);
                float4 waterColor = lerp(depthColor, _HorizonColor, fresnel);

                float3 underwater = SampleSceneColor(sceneUV);
                float3 color = lerp(underwater, waterColor.rgb, waterColor.a);

                // ---- Surface foam ----
                float foamTime = WaterTime(_FoamStepFPS);
                float2 foamUV = DistortUV(PanningUVT(IN.restXZ, _FoamDirection, _FoamSpeed, _FoamTiling.xy, 0, foamTime), _FoamDistortion, foamTime);
                float foamTex = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, foamUV).r;
                float foamMask = step(_FoamCutoff, foamTex) * _FoamColor.a;
                color = BlendOverlay(color, _FoamColor.rgb, foamMask);

                // ---- Wave crest foam (cartoon) ----
                float maxAmplitude = max(4 * _Steepness * _Wavelength / (2 * WATER_PI), 0.0001);
                float height01 = saturate(IN.waveHeight / maxAmplitude * 0.5 + 0.5);
                float crestMask = step(_CrestFoamThreshold, height01) * step(_FoamCutoff, foamTex) * _CrestFoamColor.a;
                color = BlendOverlay(color, _CrestFoamColor.rgb, crestMask);

                // ---- Intersection foam ----
                float edgeDepth = WorldDepthFade(screenUV, positionWS, _IntersectionFoamFade);
                float2 ifUV = PanningUVT(IN.restXZ, _IntersectionFoamDirection, _IntersectionFoamSpeed, _IntersectionFoamTiling.xy, 0, foamTime);
                float ifTex = SAMPLE_TEXTURE2D(_IntersectionFoamTex, sampler_IntersectionFoamTex, ifUV).r;
                float threshold = _IntersectionFoamCutoff * edgeDepth;
                float ifMask = step(ifTex, threshold) * edgeDepth * _IntersectionFoamColor.a;
                color = BlendOverlay(color, _IntersectionFoamColor.rgb, ifMask);

                // ---- Foam around players / objects touching the water ----
                float interactionFoam = WaterInteraction(IN.restXZ).y;
                float interactionMask = step(ifTex, interactionFoam * 1.2) * step(0.001, interactionFoam) * _IntersectionFoamColor.a;
                color = BlendOverlay(color, _IntersectionFoamColor.rgb, interactionMask);

                // ---- Normals (two scrolling samples, blended) ----
                float2 nUV1 = PanningUV(IN.restXZ, _NormalsDirection, _NormalsSpeed, _NormalsScale.xx, 0);
                float2 nUV2 = PanningUV(IN.restXZ, _NormalsDirection, _NormalsSpeed * -0.5, (_NormalsScale * 0.5).xx, 0);
                float3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalsTex, sampler_NormalsTex, nUV1));
                float3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalsTex, sampler_NormalsTex, nUV2));
                float3 nTS = normalize(float3(n1.rg + n2.rg, n1.b * n2.b));              // Normal Blend
                nTS = normalize(float3(nTS.rg * _NormalsStrength, lerp(1, nTS.b, saturate(_NormalsStrength)))); // Normal Strength
                float3 normalWS = normalize(float3(nTS.x, nTS.z, nTS.y)); // flat, Y-up water: tangent X/Y -> world X/Z

                // ---- Lighting ----
                float smoothness = exp2(10 * _Smoothness + 1);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
                float mainSpec = LightingSpecular(mainLight.direction, normalWS, viewDirWS, smoothness);
                float3 lighting = step(_MainLightCutoff, mainSpec) * _SpecularColor.rgb;

                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirWS;
                inputData.normalizedScreenSpaceUV = screenUV;
                lighting += AdditionalSpecular(inputData, smoothness, _Hardness);

                color += lighting;

                // ---- Fog ----
                color = MixFog(color, IN.fogFactor);

                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
