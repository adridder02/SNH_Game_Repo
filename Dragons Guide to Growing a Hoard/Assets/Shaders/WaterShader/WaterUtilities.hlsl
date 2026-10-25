// WaterUtilities.hlsl
// Custom Function nodes (Type = File): "HSVLerp" and "DistortUV". All float precision.

#ifndef STYLIZED_WATER_UTILITIES_INCLUDED
#define STYLIZED_WATER_UTILITIES_INCLUDED

// Prefixed to avoid clashing with URP's own RgbToHsv / HsvToRgb helpers.
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

// Lerp in HSV space, taking the shortest path around the hue circle.
void HSVLerp_float(float4 A, float4 B, float T, out float4 Out)
{
    float3 hsvA = WaterRGBToHSV(A.rgb);
    float3 hsvB = WaterRGBToHSV(B.rgb);

    float dh = hsvB.x - hsvA.x;
    if (dh > 0.5) dh -= 1.0;
    else if (dh < -0.5) dh += 1.0;

    float hue = frac(hsvA.x + dh * T + 1.0);
    float3 hsv = float3(hue, lerp(hsvA.y, hsvB.y, T), lerp(hsvA.z, hsvB.z, T));

    Out = float4(WaterHSVToRGB(hsv), lerp(A.a, B.a, T));
}

// Sine-based UV wobble for the surface foam.
void DistortUV_float(float2 UV, float Amount, out float2 Out)
{
    float time = _Time.y;

    UV.y += Amount * 0.01 * (sin(UV.x * 3.5 + time * 0.35) + sin(UV.x * 4.8 + time * 1.05) + sin(UV.x * 7.3 + time * 0.45)) / 3.0;
    UV.x += Amount * 0.12 * (sin(UV.y * 4.0 + time * 0.50) + sin(UV.y * 6.8 + time * 0.75) + sin(UV.y * 11.3 + time * 0.2)) / 3.0;
    UV.y += Amount * 0.12 * (sin(UV.x * 4.2 + time * 0.64) + sin(UV.x * 6.3 + time * 1.65) + sin(UV.x * 8.2 + time * 0.45)) / 3.0;

    Out = UV;
}

#endif
