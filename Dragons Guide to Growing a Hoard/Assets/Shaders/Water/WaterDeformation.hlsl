
#ifndef WATER_DEFORMATION_INCLUDED
#define WATER_DEFORMATION_INCLUDED

#define WATER_RIPPLE_COUNT 8

float4 _WaterRippleData[WATER_RIPPLE_COUNT];
float4 _WaterRippleSettings;

void WaterDeformation_float(
    float3 PositionWS,
    float TimeNow,
    out float HeightOffset)
{
    HeightOffset = 0.0;

    float radius = max(_WaterRippleSettings.x, 0.01);
    float speed = _WaterRippleSettings.y;
    float amplitude = _WaterRippleSettings.z;
    float lifetime = max(_WaterRippleSettings.w, 0.01);

    for (int i = 0; i < WATER_RIPPLE_COUNT; i++)
    {
        float4 ripple = _WaterRippleData[i];

        float age = TimeNow - ripple.w;

        if (age < 0.0 || age > lifetime)
            continue;

        float2 delta = PositionWS.xz - ripple.xy;
        float d = length(delta);

        // Distance travelled by the expanding wave.
        float waveDistance = d - age * speed;

        // A narrow ring rather than a broad, rolling hill.
        float ring = exp(
            -waveDistance * waveDistance * 3.0
        );

        // A smaller wave behind the main ring.
        float secondaryRing = exp(
            -(waveDistance - 0.35) *
            (waveDistance - 0.35) * 5.0
        );

        float wave = ring - secondaryRing * 0.45;

        // Fade smoothly toward the outer edge.
        float spatialFade =
            1.0 - smoothstep(radius * 0.75, radius, d);

        float timeFade =
            1.0 - smoothstep(0.0, lifetime, age);

        // Shallow indentation at the contact point.
        float centerDip =
            -exp(-d * d * 4.0);

        float displacement =
            wave * spatialFade * timeFade * amplitude;

        displacement +=
            centerDip * spatialFade * timeFade *
            amplitude * 0.2;

        HeightOffset += displacement;
    }

    // Keep overlapping ripples under control.
    HeightOffset = clamp(
        HeightOffset,
        -amplitude,
        amplitude
    );
}

#endif
