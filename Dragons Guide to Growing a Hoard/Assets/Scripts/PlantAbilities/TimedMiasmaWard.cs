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

    /// <summary>The Verdant Algae AbilityItemData that applied this — used to show its icon (the
    /// SAME sprite Inventory shows for it) in PotMenuUIController's active-consumable indicator.</summary>
    public AbilityItemData sourceData;

    /// <summary>Fires exactly once, whether the ward ends naturally (duration runs out) or this
    /// GameObject is destroyed some other way — see OnDestroy(), the single place this actually
    /// fires from. Used by AbilityConsumableEffects to revert the pot's soil off its algae material
    /// variant once the ward is gone.</summary>
    public event System.Action OnEnded;

    public static TimedMiasmaWard ApplyTo(PlantState plant, float duration, AbilityItemData data = null, System.Action onEnded = null)
    {
        if (plant == null) return null;

        GameObject go = new GameObject("VerdantAlgae_MiasmaWard");
        go.transform.SetParent(plant.transform, worldPositionStays: false);

        TimedMiasmaWard ward = go.AddComponent<TimedMiasmaWard>();
        ward.plant = plant;
        ward.remaining = duration;
        ward.sourceData = data;
        if (onEnded != null) ward.OnEnded += onEnded;
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
        // The single place OnEnded actually fires — covers BOTH natural expiry (EndWard() calls
        // Destroy(), which triggers this) and any other path that destroys this GameObject, in one
        // place, exactly once, rather than firing it separately from EndWard() too and risking a
        // double-invoke.
        if (plant != null) plant.RemoveMiasmaImmunitySource(this);
        OnEnded?.Invoke();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.95f, 0.5f, 0.5f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.25f);
    }
}