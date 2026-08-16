using UnityEngine;

// =============================================================
// SundialCactusIndicator.cs
// -------------------------------------------------------------
// Attach alongside PlantState (and LightSensor) on the Sundial
// Cactus prefab. Reads the plant's own LightSensor and converts
// NormalisedIntensity into a 0-4 "stem count": 0% dark = 0 stems,
// 25% = 1, 50% = 2, 75% = 3, 100% (full sunlight) = 4.
//
// stemObjects is optional — if you build a model with 4 stem meshes
// you can wire them here and they'll be toggled on/off to match
// StemCount. Until then, OnDrawGizmos draws the same count as
// vertical placeholder lines so the behaviour is visible/testable
// in the Scene view immediately.
// =============================================================
[RequireComponent(typeof(PlantState))]
public class SundialCactusIndicator : MonoBehaviour
{
    [Tooltip("Optional: 4 GameObjects (one per stem) toggled on/off to match StemCount. Leave empty to " +
             "rely on the gizmo placeholder only.")]
    public GameObject[] stemObjects = new GameObject[4];

    private LightSensor lightSensor;

    public int StemCount { get; private set; }

    private void Awake()
    {
        lightSensor = GetComponent<LightSensor>() ?? GetComponentInChildren<LightSensor>();
        if (lightSensor == null)
            Debug.LogWarning("[SundialCactusIndicator] No LightSensor found — stem count will always read 0.", this);
        
        for (int i = 0; i < stemObjects.Length; i++)
            stemObjects[i].SetActive(false);    
    }

    private void Update()
    {
        float intensity = lightSensor != null ? lightSensor.NormalisedIntensity : 0f;

        StemCount =
            intensity >= 0.999f ? 4 :
            intensity >= 0.75f  ? 3 :
            intensity >= 0.50f  ? 2 :
            intensity >= 0.25f  ? 1 : 0;

        for (int i = 0; i < stemObjects.Length; i++)
            stemObjects[i]?.SetActive(i < StemCount);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.95f, 0.85f, 0.2f);
        for (int i = 0; i < StemCount; i++)
        {
            Vector3 baseP = transform.position + new Vector3((i - 1.5f) * 0.15f, 0f, 0f);
            Gizmos.DrawLine(baseP, baseP + Vector3.up * (0.2f + i * 0.05f));
        }
    }
}
