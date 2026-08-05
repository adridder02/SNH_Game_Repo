using UnityEngine;

// =============================================================
// TimedMiasmaWard.cs
// -------------------------------------------------------------
// Spawned by Verdant Algae's SoilMiasmaWard effect. Registers itself
// as a PlantState miasma-immunity source for 'duration' seconds, then
// unregisters and removes itself — reference-counted immunity (see
// PlantState.AddMiasmaImmunitySource) means this can coexist cleanly
// with Windmill Aster or a Sparkmint circuit warding the same plant.
// =============================================================
public class TimedMiasmaWard : MonoBehaviour
{
    private PlantState plant;
    private float remaining;

    public static TimedMiasmaWard ApplyTo(PlantState plant, float duration)
    {
        if (plant == null) return null;

        GameObject go = new GameObject("VerdantAlgae_MiasmaWard");
        go.transform.SetParent(plant.transform, worldPositionStays: false);

        TimedMiasmaWard ward = go.AddComponent<TimedMiasmaWard>();
        ward.plant = plant;
        ward.remaining = duration;
        plant.AddMiasmaImmunitySource(ward);
        return ward;
    }

    private void Update()
    {
        remaining -= Time.deltaTime;
        if (remaining <= 0f) EndWard();
    }

    private void EndWard()
    {
        if (plant != null) plant.RemoveMiasmaImmunitySource(this);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (plant != null) plant.RemoveMiasmaImmunitySource(this);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.95f, 0.5f, 0.5f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.25f);
    }
}
