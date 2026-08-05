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
        timer += Time.deltaTime;
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

        foreach (Collider col in Physics.OverlapSphere(transform.position, radius, plantLayerMask))
        {
            PlantState plant = col.GetComponent<PlantState>() ?? col.GetComponentInParent<PlantState>();
            if (plant == null || plant.gameObject == gameObject) continue;

            plant.ApplyDebuffFromNeighbour(spec, GetComponent<PlantState>());
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.1f, 0.5f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
