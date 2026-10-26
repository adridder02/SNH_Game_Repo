using UnityEngine;

// =============================================================
// MelodybloomTreeMimic.cs
// -------------------------------------------------------------
// Attach alongside PlantState on the Melodybloom prefab. "Thrives in
// the worst possible conditions and mimics the effects of the tree on
// a minor scale, increasing how quickly plants need things like water
// refilled." Reuses PlantState's EXISTING neighbour-debuff pipeline
// (DebuffSpec + ApplyDebuffFromNeighbour, already built for the
// per-prefab neighbour debuff system) instead of inventing a second
// one — just applied continuously in a radius rather than only to
// 4-way grid neighbours.
// =============================================================
[RequireComponent(typeof(PlantState))]
public class MelodybloomTreeMimic : MonoBehaviour
{
    public float radius = 5f;
    public float tickInterval = 3f;

    [Tooltip("How much water each affected plant's pot loses per application — kept small since this is " +
             "meant to be a MINOR version of the tree's effect.")]
    public float drainAmountPerTick = 0.4f;

    public LayerMask plantLayerMask = ~0;

    private float timer;

    private void Update()
    {
        timer += GameClock.DeltaTime;
        if (timer < tickInterval) return;
        timer = 0f;

        PlantState.DebuffSpec spec = new PlantState.DebuffSpec
        {
            enabled = true,
            mode = PlantState.DebuffSpec.Mode.DrainWater,
            amountPerTick = drainAmountPerTick,
            tickInterval = tickInterval,
            duration = tickInterval, // one-shot pulse, re-applied every tickInterval rather than stacking
            transferStolenWaterToSelf = false
        };

        var affectedPots = new System.Collections.Generic.HashSet<PotContents>();

        foreach (Collider col in Physics.OverlapSphere(transform.position, radius, plantLayerMask))
        {
            PlantState plant = col.GetComponent<PlantState>() ?? col.GetComponentInParent<PlantState>();
            if (plant == null || plant.gameObject == gameObject) continue;

            plant.ApplyDebuffFromNeighbour(spec, GetComponent<PlantState>());

            PotContents pot = plant.GetComponentInParent<PotContents>();
            if (pot != null) affectedPots.Add(pot);
        }

        UpdateTints(affectedPots);
    }

    // Purple grid cells under every plant this Melodybloom is currently draining.
    private int lastTintSignature = int.MinValue;

    private void UpdateTints(System.Collections.Generic.HashSet<PotContents> pots)
    {
        int sig = 17;
        foreach (PotContents pot in pots) sig += pot.GetInstanceID() * 31;
        if (sig == lastTintSignature) return;
        lastTintSignature = sig;

        GridEffectTints.Clear(this);
        foreach (PotContents pot in pots)
            GridEffectTints.TintPot(this, pot, GridEffectTints.Melodybloom);
    }

    private void OnDisable() { GridEffectTints.Clear(this); lastTintSignature = int.MinValue; }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.1f, 0.5f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
