// WaterLighting.hlsl
// Stylized specular lighting for URP in Unity 6 (works with Forward and Forward+).
//
// Shader Graph setup:
//  * Custom Function node, Type = File, Source = this file.
//  * Node names: "MainLighting" and "AdditionalLighting" (Shader Graph appends _float).
//  * Add these Boolean keywords on the Blackboard (Multi Compile, Global, NOT exposed):
//      _CLUSTER_LIGHT_LOOP (Unity 6.1+) or _FORWARD_PLUS (Unity 6.0), _MAIN_LIGHT_SHADOWS, _MAIN_LIGHT_SHADOWS_CASCADE,
//      _MAIN_LIGHT_SHADOWS_SCREEN, _ADDITIONAL_LIGHTS, _ADDITIONAL_LIGHT_SHADOWS, _SHADOWS_SOFT
//  * AdditionalLighting needs a Screen Position (Default) node wired into screenUV.

#ifndef STYLIZED_WATER_LIGHTING_INCLUDED
#define STYLIZED_WATER_LIGHTING_INCLUDED

// Blinn-Phong style specular term. Safe in the Shader Graph preview (no URP types used).
float LightingSpecular(float3 L, float3 N, float3 V, float smoothness)
{
    float3 H = SafeNormalize(L + V);
    float NdotH = saturate(dot(N, H));
    return pow(NdotH, smoothness);
}

// Everything that touches the URP "Light" struct must stay out of the preview build.
#ifndef SHADERGRAPH_PREVIEW

float3 LightSpecular(Light light, float3 N, float3 V, float smoothness, float hardness)
{
    float3 attenuatedLight = light.color * light.distanceAttenuation * light.shadowAttenuation;

    float specularSoft = LightingSpecular(light.direction, N, V, smoothness);
    float specularHard = smoothstep(0.005, 0.01, specularSoft);
    float specularTerm = lerp(specularSoft, specularHard, hardness);

    return specularTerm * attenuatedLight;
}

#endif

void MainLighting_float(float3 normalWS, float3 positionWS, float3 viewWS, float smoothness, out float specular)
{
    specular = 0.0;

#ifndef SHADERGRAPH_PREVIEW
    smoothness = exp2(10 * smoothness + 1);

    normalWS = normalize(normalWS);
    viewWS = SafeNormalize(viewWS);

    Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
    specular = LightingSpecular(mainLight.direction, normalWS, viewWS, smoothness);
    // Optional: multiply by mainLight.shadowAttenuation to hide highlights in shadow.
#endif
}

void AdditionalLighting_float(float3 normalWS, float3 positionWS, float3 viewWS, float2 screenUV,
                              float smoothness, float hardness, out float3 specular)
{
    specular = 0;

#ifndef SHADERGRAPH_PREVIEW
    smoothness = exp2(10 * smoothness + 1);

    normalWS = normalize(normalWS);
    viewWS = SafeNormalize(viewWS);

    // Forward+ needs an InputData in scope for LIGHT_LOOP_BEGIN.
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = viewWS;
    inputData.normalizedScreenSpaceUV = screenUV;

    #if USE_CLUSTER_LIGHT_LOOP || USE_FORWARD_PLUS
    // In Forward+, extra directional lights are not part of the clustered loop.
    for (uint d = 0; d < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); d++)
    {
        Light dirLight = GetAdditionalLight(d, positionWS, half4(1, 1, 1, 1));
        specular += LightSpecular(dirLight, normalWS, viewWS, smoothness, hardness);
    }
    #endif

    // Note: GetAdditionalLightsCount() returns 0 in Forward+; LIGHT_LOOP_BEGIN handles that path.
    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1));
        specular += LightSpecular(light, normalWS, viewWS, smoothness, hardness);
    LIGHT_LOOP_END
#endif
}

#endif
