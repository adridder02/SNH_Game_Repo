using System.Collections.Generic;
using UnityEngine;

// =============================================================
// WindmillAsterWard.cs
// -------------------------------------------------------------
// Attach alongside PlantState (and LightSensor) on the Windmill
// Aster prefab. Only active while ITS OWN LightSensor reads direct
// sunlight (>= sunlightThreshold) — "This effect can only be active
// if put in direct sunlight." While active, every plant within
// wardRadius is registered as miasma-immune via PlantState's
// reference-counted immunity sources; the moment sunlight drops
// below threshold (shade, night, moved indoors) every warded
// neighbour is released again.
// =============================================================
[RequireComponent(typeof(PlantState))]
public class WindmillAsterWard : MonoBehaviour
{
    [Range(0f, 1f)]
    [Tooltip("Own LightSensor.NormalisedIntensity must be at or above this to count as 'direct sunlight'.")]
    public float sunlightThreshold = 0.6f;

    public float wardRadius = 4f;
    public float rescanInterval = 1f;
    public LayerMask plantLayerMask = ~0;

    private LightSensor lightSensor;
    private readonly HashSet<PlantState> currentlyWarded = new HashSet<PlantState>();
    private float rescanTimer;

    private void Awake()
    {
        lightSensor = GetComponent<LightSensor>() ?? GetComponentInChildren<LightSensor>();
    }

    private void Update()
    {
        bool inDirectSunlight = lightSensor != null && lightSensor.NormalisedIntensity >= sunlightThreshold;

        if (!inDirectSunlight)
        {
            ReleaseAll();
            return;
        }

        rescanTimer += Time.deltaTime;
        if (rescanTimer < rescanInterval) return;
        rescanTimer = 0f;

        var found = new HashSet<PlantState>();
        foreach (Collider col in Physics.OverlapSphere(transform.position, wardRadius, plantLayerMask))
        {
            PlantState plant = col.GetComponent<PlantState>() ?? col.GetComponentInParent<PlantState>();
            if (plant != null) found.Add(plant);
        }

        foreach (PlantState plant in currentlyWarded)
            if (plant != null && !found.Contains(plant))
                plant.RemoveMiasmaImmunitySource(this);

        foreach (PlantState plant in found)
            plant.AddMiasmaImmunitySource(this);

        currentlyWarded.Clear();
        currentlyWarded.UnionWith(found);
    }

    private void ReleaseAll()
    {
        if (currentlyWarded.Count == 0) return;
        foreach (PlantState plant in currentlyWarded)
            if (plant != null) plant.RemoveMiasmaImmunitySource(this);
        currentlyWarded.Clear();
    }

    private void OnDisable() => ReleaseAll();
    private void OnDestroy() => ReleaseAll();

    private void OnDrawGizmosSelected()
    {
        bool active = lightSensor != null && lightSensor.NormalisedIntensity >= sunlightThreshold;
        Gizmos.color = active ? new Color(0.6f, 0.9f, 1f, 0.5f) : new Color(0.5f, 0.5f, 0.5f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, wardRadius);
    }
}
