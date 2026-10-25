using UnityEngine;

/// <summary>
/// The wave parameters the CPU needs to match the water shader. Shared by BuoyantObject and WaterInteractor.
/// Assign the water material and press "Read From Material" (or just enter play mode) so the values can't drift.
/// </summary>
[System.Serializable]
public class WaterWaveSettings
{
    [Tooltip("The water material. If it uses the StylizedWater shader, wave values are read from it automatically.")]
    public Material material;

    [Tooltip("The water object. Its world Y is the water height.")]
    public Transform surface;

    public float steepness = 0.15f;
    public float wavelength = 10f;
    public float speed = 1f;
    public float[] directions = { 0.10f, 0.35f, 0.60f, 0.85f };

    static readonly int SteepnessId = Shader.PropertyToID("_Steepness");
    static readonly int WavelengthId = Shader.PropertyToID("_Wavelength");
    static readonly int SpeedId = Shader.PropertyToID("_WaveSpeed");
    static readonly int DirectionsId = Shader.PropertyToID("_Directions");

    // If you use the Shader Graph version, set each property's Reference to these same names:
    // _Steepness, _Wavelength, _WaveSpeed, _Directions.
    public void ReadFromMaterial()
    {
        if (material == null) return;
        if (!material.HasProperty(SteepnessId) || !material.HasProperty(DirectionsId)) return;

        steepness = material.GetFloat(SteepnessId);
        wavelength = material.GetFloat(WavelengthId);
        speed = material.GetFloat(SpeedId);

        Vector4 d = material.GetVector(DirectionsId);
        if (directions == null || directions.Length != 4) directions = new float[4];
        directions[0] = d.x; directions[1] = d.y; directions[2] = d.z; directions[3] = d.w;
    }

    /// <summary>World-space Y of the water surface at a world position.</summary>
    public float HeightAt(Vector3 worldPosition)
    {
        float baseY = surface != null ? surface.position.y : 0f;
        return baseY + GerstnerWaveDisplacement.GetWaveHeight(worldPosition, steepness, wavelength, speed, directions);
    }
}
