using UnityEngine;

// =============================================================
// SparkmintLeafFence.cs
// -------------------------------------------------------------
// One tamed Sparkmint leaf, placed via AbilityPlacementSystem. Uses
// its AbilityItemData.amountA as its lifespan in seconds ("Leaves
// have a duration and when they run out they act as if they're not
// there at all functionally, in other words it breaks the circuit
// until you replace it") — note it deliberately stays ON the grid
// (still occupying its cell, still visible/removable) once expired;
// it just stops counting toward SparkmintCircuitManager's circuit
// check, exactly like the brief describes.
//
// SETUP: AbilityItemData for "Sparkmint Leaf" should be Kind=Placeable,
// effectId=SparkmintLeafFence, footprint (1,1), amountA = leaf lifespan
// in seconds, harvestGrantAmount = 10 (10 leaves per harvest).
// =============================================================
public class SparkmintLeafFence : AbilityPlaceable
{
    private float expireAtTime;
    private bool hasExpired;

    public bool IsActive => !hasExpired;

    protected override void OnPlaced()
    {
        float lifespan = Data != null && Data.amountA > 0f ? Data.amountA : 120f;
        expireAtTime = Time.time + lifespan;
        hasExpired = false;

        SparkmintCircuitManager.Recalculate(Surface, GridData);
    }

    protected override void OnRemoved()
    {
        SparkmintCircuitManager.Recalculate(Surface, GridData);
    }

    private void Update()
    {
        if (hasExpired) return;

        if (Time.time >= expireAtTime)
        {
            hasExpired = true;
            Debug.Log($"[SparkmintLeafFence] Leaf at {GridOrigin} expired — circuit broken until replaced.");
            SparkmintCircuitManager.Recalculate(Surface, GridData);
        }
    }

    // ---------------------------------------------------------------
    // Placeholder visual — green while warding, brown/withered once expired.
    // ---------------------------------------------------------------
    private void OnDrawGizmos()
    {
        DrawFootprintGizmo(hasExpired ? new Color(0.5f, 0.35f, 0.15f) : new Color(0.2f, 0.9f, 0.3f));

        if (!hasExpired)
        {
            float remaining = Mathf.Max(0f, expireAtTime - Time.time);
            float lifespan = Data != null && Data.amountA > 0f ? Data.amountA : 120f;
            float t = lifespan > 0f ? remaining / lifespan : 0f;

            Gizmos.color = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(0.2f, 0.9f, 0.3f), t);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.3f, 0.12f);
        }
    }
}
