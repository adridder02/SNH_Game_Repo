// GerstnerWaves.hlsl
// 4 summed Gerstner waves. Custom Function node: Type = File, Name = "GerstnerWaves".
// Inputs : position (World), steepness, wavelength, speed, directions (Vector4, each 0..1)
// Outputs: Offset (add to world position), normal
//
// PI is the full-precision value so the CPU buoyancy code (Mathf.PI) stays in sync.

#ifndef STYLIZED_WATER_GERSTNER_INCLUDED
#define STYLIZED_WATER_GERSTNER_INCLUDED

#define WATER_PI 3.14159265

float3 GerstnerWave(float3 position, float steepness, float wavelength, float speed, float direction,
                    inout float3 tangent, inout float3 binormal)
{
    direction = direction * 2 - 1;
    float2 d = normalize(float2(cos(WATER_PI * direction), sin(WATER_PI * direction)));
    float k = 2 * WATER_PI / wavelength;
    float f = k * (dot(d, position.xz) - speed * _Time.y);
    float a = steepness / k;

    tangent += float3(
        -d.x * d.x * (steepness * sin(f)),
         d.x * (steepness * cos(f)),
        -d.x * d.y * (steepness * sin(f)));

    binormal += float3(
        -d.x * d.y * (steepness * sin(f)),
         d.y * (steepness * cos(f)),
        -d.y * d.y * (steepness * sin(f)));

    return float3(
        d.x * (a * cos(f)),
        a * sin(f),
        d.y * (a * cos(f)));
}

void GerstnerWaves_float(float3 position, float steepness, float wavelength, float speed, float4 directions,
                         out float3 Offset, out float3 normal)
{
    Offset = 0;
    float3 tangent = float3(1, 0, 0);
    float3 binormal = float3(0, 0, 1);

    Offset += GerstnerWave(position, steepness, wavelength, speed, directions.x, tangent, binormal);
    Offset += GerstnerWave(position, steepness, wavelength, speed, directions.y, tangent, binormal);
    Offset += GerstnerWave(position, steepness, wavelength, speed, directions.z, tangent, binormal);
    Offset += GerstnerWave(position, steepness, wavelength, speed, directions.w, tangent, binormal);

    normal = normalize(cross(binormal, tangent));
}

#endif
