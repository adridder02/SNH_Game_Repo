using UnityEngine;

/// <summary>
/// CPU mirror of GerstnerWaves.hlsl. Parameters must match the shader exactly.
/// </summary>
public static class GerstnerWaveDisplacement
{
    // Shader uses _Time.y, which counts from level load, so use the matching CPU clock.
    private static float Now => Time.timeSinceLevelLoad;

    private static Vector3 GerstnerWave(Vector3 position, float steepness, float wavelength, float speed, float direction)
    {
        direction = direction * 2 - 1;
        Vector2 d = new Vector2(Mathf.Cos(Mathf.PI * direction), Mathf.Sin(Mathf.PI * direction)).normalized;
        float k = 2 * Mathf.PI / wavelength;
        float a = steepness / k;
        float f = k * (Vector2.Dot(d, new Vector2(position.x, position.z)) - speed * Now);

        return new Vector3(d.x * a * Mathf.Cos(f), a * Mathf.Sin(f), d.y * a * Mathf.Cos(f));
    }

    public static Vector3 GetWaveDisplacement(Vector3 position, float steepness, float wavelength, float speed, float[] directions)
    {
        Vector3 offset = Vector3.zero;
        for (int i = 0; i < 4; i++)
            offset += GerstnerWave(position, steepness, wavelength, speed, directions[i]);
        return offset;
    }

    /// <summary>
    /// Wave height at a world XZ position. Gerstner waves also move vertices sideways, so the vertex
    /// that ends up above 'position' started somewhere else. A few fixed-point iterations find it.
    /// </summary>
    public static float GetWaveHeight(Vector3 position, float steepness, float wavelength, float speed,
                                      float[] directions, int iterations = 2)
    {
        Vector3 p = position;
        for (int i = 0; i < iterations; i++)
        {
            Vector3 o = GetWaveDisplacement(p, steepness, wavelength, speed, directions);
            p.x = position.x - o.x;
            p.z = position.z - o.z;
        }
        return GetWaveDisplacement(p, steepness, wavelength, speed, directions).y;
    }
}
