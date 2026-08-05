using System.Collections.Generic;
using UnityEngine;

// =============================================================
// WaterbellSprinkler.cs
// -------------------------------------------------------------
// Placed via AbilityPlacementSystem. Finds up to 3 adjacent (4-way)
// pots and keeps topping each one up toward targetWaterLevel using
// PotContents.AddWater — since AddWater already clamps at
// plantWaterMax, this naturally "holds" the level rather than
// overfilling once it's reached, matching "set the plant's water
// level to whatever the waterbell's menu says it should be and lock
// it there until something changes."
//
// targetWaterLevel is a plain public field for now, standing in for
// the real per-plant menu described in the brief ("choose the water
// level... for up to three plants") — wire a proper UI to it later;
// for now it's editable in the Inspector per-instance (or via
// SetTargetLevel at runtime from a placeholder debug menu) and the
// SAME level applies to all connected pots.
// =============================================================
public class WaterbellSprinkler : AbilityPlaceable
{
    [Range(0f, 10f)]
    public float targetWaterLevel = 6f;

    [Tooltip("How often (seconds) this re-tops-up its connected pots.")]
    public float tickInterval = 2f;

    [Tooltip("How much water is added per tick when a connected pot is below target — mirrors the amount " +
             "a player's watering press would add (PotInteraction.waterPerPress).")]
    public float amountPerTick = 2f;

    private readonly List<PotContents> connectedPots = new List<PotContents>();
    private float tickTimer;

    private static readonly Vector3Int[] Neighbours4 =
    {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
    };

    protected override void OnPlaced()
    {
        if (Data != null && Data.amountA > 0f) targetWaterLevel = Data.amountA;
        RefreshConnectedPots();
    }

    private void Update()
    {
        tickTimer += Time.deltaTime;
        if (tickTimer < tickInterval) return;
        tickTimer = 0f;

        // Re-scan every tick rather than caching forever — a pot might get moved/removed/added
        // next to the waterbell after placement, and this is cheap (at most 4 neighbour cells).
        RefreshConnectedPots();

        foreach (PotContents pot in connectedPots)
        {
            if (pot == null || !pot.HasPlant) continue;
            if (pot.WaterLevel < targetWaterLevel - 0.01f)
                pot.AddWater(Mathf.Min(amountPerTick, targetWaterLevel - pot.WaterLevel));
        }
    }

    private void RefreshConnectedPots()
    {
        connectedPots.Clear();
        if (GridData == null) return;

        foreach (Vector3Int offset in Neighbours4)
        {
            if (connectedPots.Count >= 3) break;

            Vector3Int neighbourCell = GridOrigin + offset;
            PlacementData data = GridData.GetPlacement(neighbourCell);
            PotContents pot = data?.PlacedObject != null ? data.PlacedObject.GetComponent<PotContents>() : null;
            if (pot != null && !connectedPots.Contains(pot))
                connectedPots.Add(pot);
        }
    }

    public void SetTargetLevel(float level) => targetWaterLevel = Mathf.Clamp(level, 0f, 10f);

    // ---------------------------------------------------------------
    private void OnDrawGizmos()
    {
        DrawFootprintGizmo(new Color(0.2f, 0.6f, 0.95f));

        Gizmos.color = new Color(0.2f, 0.6f, 0.95f, 0.8f);
        foreach (PotContents pot in connectedPots)
        {
            if (pot == null) continue;
            Gizmos.DrawLine(transform.position + Vector3.up * 0.2f, pot.transform.position + Vector3.up * 0.2f);
            Gizmos.DrawWireSphere(pot.transform.position + Vector3.up * 0.2f, 0.1f);
        }
    }
}
