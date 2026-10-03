using UnityEngine;

// =============================================================
// PollenPuffProducer.cs
// -------------------------------------------------------------
// Attach alongside PlantState on the Pollen Puff prefab. This is
// DELIBERATELY separate from PlantHarvestYield/PotContents.HarvestPlant
// — the puffballs are a repeating side-production while the plant
// stays alive and growing, not a one-time "harvest destroys the
// plant" payout like the other 15 abilities. Pollen Puff can still
// ALSO have a PlantHarvestYield if you want harvesting the whole
// plant to give a final bonus puffball or two — the two systems
// don't conflict.
//
// ASSUMPTIONS (flagged — there's no day/night cycle system in the
// codebase to hook into, so this uses a plain real-seconds timer
// instead):
//   • "Per day cycle" = cycleDurationSeconds of real time, exposed in
//     the Inspector so it can be tuned to match whatever day-length
//     you land on later (or driven externally — see SetCycleDuration).
//   • Production only accrues while the plant is Revived (PlantState.
//     CurrentState) — a dead/struggling Pollen Puff doesn't produce.
//     Change RequiresRevivedState below if that's wrong.
//   • Puffballs accrue smoothly (progress toward the next one ticks
//     up continuously) rather than all 3 appearing at once at the
//     start of each cycle — feels better moment-to-moment and this
//     was the part of the spec most open to interpretation, so this
//     is the most game-feel-friendly reading rather than the only
//     possible one.
//   • Uncollected puffballs just wait once ReadyCount hits maxPuffballs
//     (production pauses, nothing is lost) rather than overflowing.
// =============================================================
[RequireComponent(typeof(PlantState))]
public class PollenPuffProducer : MonoBehaviour
{
    [Header("Item")]
    [Tooltip("The ability item granted per puffball collected. Should be Kind=Consumable, effectId=PollenCloud.")]
    public AbilityItemData puffballItem;

    [Header("Production")]
    public int maxPuffballs = 3;

    [Tooltip("Real seconds for one full 'day cycle' that produces maxPuffballs worth of puffballs.")]
    public float cycleDurationSeconds = 600f;

    [Tooltip("If true, production pauses unless PlantState.CurrentState == Revived.")]
    public bool requiresRevivedState = true;

    private PlantState plantState;
    private float progress; // 0..maxPuffballs, fractional

    /// <summary>Whole puffballs currently waiting to be collected.</summary>
    public int ReadyCount => Mathf.FloorToInt(progress);

    private void Awake()
    {
        plantState = GetComponent<PlantState>();
    }

    private void Update()
    {
        if (requiresRevivedState && plantState != null && plantState.CurrentState != PlantStateEnum.Revived)
            return;

        if (progress >= maxPuffballs) return; // full — wait for a collection to free up room

        float ratePerSecond = maxPuffballs / Mathf.Max(1f, cycleDurationSeconds);
        progress = Mathf.Min(maxPuffballs, progress + ratePerSecond * Time.deltaTime);
    }

    /// <summary>Collects every ready puffball into the given inventory. Returns how many were collected
    /// (0 if none were ready or puffballItem/inventory is missing).</summary>
    public int Collect(PlayerAbilityInventory inventory)
    {
        int ready = ReadyCount;
        if (ready <= 0 || inventory == null || puffballItem == null) return 0;

        inventory.Add(puffballItem, ready);
        progress -= ready; // keep the fractional remainder so partial progress toward the next one isn't lost

        Debug.Log($"[PollenPuffProducer] Collected {ready}x {puffballItem.displayName} from '{gameObject.name}'.");
        return ready;
    }

    /// <summary>Lets an external day/night system drive this instead of the Inspector-set duration,
    /// once one exists — pass the real-seconds length of a full in-game day.</summary>
    public void SetCycleDuration(float seconds) => cycleDurationSeconds = Mathf.Max(1f, seconds);

    private void OnDrawGizmos()
    {
        int ready = ReadyCount;
        for (int i = 0; i < maxPuffballs; i++)
        {
            Vector3 pos = transform.position + Vector3.up * (1.0f + i * 0.2f);
            Gizmos.color = i < ready ? new Color(0.95f, 0.85f, 0.4f) : new Color(0.6f, 0.6f, 0.6f, 0.3f);
            Gizmos.DrawSphere(pos, 0.08f);
        }
    }
}
