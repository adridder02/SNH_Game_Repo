// =============================================================
// LightSensor.cs
// -------------------------------------------------------------
// Attach this to each Plant GameObject.
//
// HOW IT WORKS:
//   - Finds the scene's Directional Light (Unity's sun).
//   - Reads its intensity and normalises it to a 0–1 range.
//   - Optionally ray-casts toward the light to detect whether the
//     plant is occluded (in shadow behind a wall/hill/etc.).
//   - Exposes NormalisedIntensity, which PlantState.cs uses to
//     calculate the plant's light score contribution.
//
// LIGHT SCORE (calculated in PlantState.cs, based on this value):
//   NormalisedIntensity >= lightThresholdHigh → +2 points (bright)
//   NormalisedIntensity >= lightThresholdLow  → +1 point  (dim)
//   NormalisedIntensity <  lightThresholdLow  → +0 points (dark)
//
// SETUP:
//   1. Attach to each plant.
//   2. Leave directionalLight empty to auto-find, or drag it in.
//   3. If using shadow occlusion, put environment objects on a layer
//      and assign that layer to obstacleLayerMask.
// =============================================================

using UnityEngine;

public class LightSensor : MonoBehaviour
{
    // ---------------------------------------------------------------
    // INSPECTOR — Light source
    //!GOING TO USE AREA LIGHT INSTEAD TO SEE IF THIS WORKS
    // ---------------------------------------------------------------
    [Header("Directional Light")]
    [Tooltip("Drag your scene's Directional Light here, or leave empty to auto-find.")]
    public Light directionalLight;

    [Tooltip("The maximum expected light intensity (used to normalise to 0–1). " +
             "Unity's default sun is 1.0; HDR setups can go higher.")]
    public float maxLightIntensity = 3f;

    // ---------------------------------------------------------------
    // INSPECTOR — Shadow / occlusion check
    // ---------------------------------------------------------------
    [Header("Shadow Occlusion (optional)")]
    [Tooltip("Cast a ray toward the sun to check if the plant is in shadow.")]
    public bool checkForObstacles = true;

    [Tooltip("Layers that can block sunlight (e.g. terrain, walls). " +
             "Default (~0) means everything blocks and 0 means nothing blocks.")]
    public LayerMask obstacleLayerMask;

    [Tooltip("Ray origin height offset above the plant pivot, to avoid self-collision.")]
    public float rayOriginOffset = 0.5f;

    // ---------------------------------------------------------------
    // Runtime — visible in Inspector for debugging.
    // ---------------------------------------------------------------
    [Header("Runtime Info (read-only)")]
    [SerializeField] private float rawIntensity;        // Direct value from Light.intensity
    [SerializeField] private bool  isInShadow;           // True if an obstacle blocks the sun
    [SerializeField] private float normalisedIntensity;  // Final 0–1 value for plant logic

    // ---------------------------------------------------------------
    // Public read-only access consumed by PlantState.cs.
    // ---------------------------------------------------------------
    public float NormalisedIntensity => normalisedIntensity;

    // ---------------------------------------------------------------
    // Awake — auto-find the Directional Light if not manually assigned.
    // ---------------------------------------------------------------
    private void Awake()
    {
        if (directionalLight != null) return; // Already set — skip search.

        // Scan all lights in the scene for the first Directional type.
        // FindObjectsByType replaces the deprecated FindObjectsOfType.
        // FindObjectsSortMode.None skips sorting for better performance
        // since we only need any directional light, not a specific one.
        Light[] allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light l in allLights)
        {
            if (l.type == LightType.Rectangle)
            {
                directionalLight = l;
                Debug.Log($"[LightSensor] Auto-found directional light: '{l.name}'.");
                return;
            }
        }

        Debug.LogWarning("[LightSensor] No Directional Light found in the scene. " +
                         "Light score will always be 0.");
    }

    // ---------------------------------------------------------------
    // Update — sample and process the light every frame.
    // ---------------------------------------------------------------
    private void Update()
    {
        if (directionalLight == null)
        {
            normalisedIntensity = 0f;
            return;
        }

        // Read the raw intensity from the Unity Light component.
        rawIntensity = directionalLight.intensity;
//        Debug.Log("Raw Light Intensity felt " + rawIntensity);
        // Optionally check whether the plant is occluded by geometry.
        isInShadow = checkForObstacles && IsPlantInShadow();

        // If in shadow, report zero light regardless of sun intensity.
        // Otherwise, normalise the intensity to a 0–1 range.
        normalisedIntensity = isInShadow
            ? 0f
            : Mathf.Clamp01(rawIntensity / maxLightIntensity);
//             Debug.Log("Light Intensity felt " + Mathf.Clamp01(rawIntensity / maxLightIntensity));

    }

    // ---------------------------------------------------------------
    // IsPlantInShadow — fires a ray from the plant toward the sun.
    // Returns true if any obstacle on obstacleLayerMask is hit.
    // ---------------------------------------------------------------
    private bool IsPlantInShadow()
    {
        // The directional light's forward vector points INTO the scene,
        // so we negate it to get the direction FROM the plant TO the sun.
        Vector3 directionToLight = -directionalLight.transform.forward;

        // Offset the ray origin upward to avoid the plant's own collider.
        Vector3 rayOrigin = transform.position + Vector3.up * rayOriginOffset;

        // If the ray hits anything, the plant is behind an obstacle.
        return Physics.Raycast(
            rayOrigin,
            directionToLight,
            Mathf.Infinity,     // No distance cap — the sun is very far away
            obstacleLayerMask
        );
    }

    // ---------------------------------------------------------------
    // OnDrawGizmosSelected — visualises the sun ray in the Scene view.
    // Yellow = clear line-of-sight; Red = in shadow.
    // ---------------------------------------------------------------
    private void OnDrawGizmosSelected()
    {
        if (directionalLight == null) return;

        Vector3 origin    = transform.position + Vector3.up * rayOriginOffset;
        Vector3 direction = -directionalLight.transform.forward;

        Gizmos.color = isInShadow ? Color.red : Color.yellow;
        Gizmos.DrawRay(origin, direction * 6f);
    }
}
