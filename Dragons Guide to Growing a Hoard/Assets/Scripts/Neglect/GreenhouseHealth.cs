using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Aggregates all ZoneHealth scores into a single greenhouse-level happiness
/// score and evaluates the asymmetric "tree finished" condition.
///
/// THE ASYMMETRIC FINISH CONDITION
/// --------------------------------
/// The greenhouse (tree) is considered "finished" / fully neglected when
/// EITHER of the following is true — both thresholds are adjustable:
///
///   1. UNIFORM NEGLECT:
///      Every zone's happiness is at or below uniformNeglectThreshold.
///      Example default (60 %): all zones at 60 % → finished.
///
///   2. DOMINANT NEGLECT:
///      At least one zone is at or below dominantBadThreshold AND
///      the number of such zones meets or exceeds dominantBadZoneCount.
///      Example defaults (20 %, 2 zones): two zones at ≤20 % → finished,
///      regardless of what the remaining zone(s) score.
///
/// This captures the described scenarios:
///   • "All three zones at 60 %"    → Condition 1 fires.
///   • "One zone 100 %, two at 10 %" → Condition 2 fires (two zones ≤ 20 %).
///
/// All threshold numbers are exposed in the Inspector under
/// "Finish Conditions" so the exact values can be changed without recompiling.
///
/// OVERALL SCORE
/// -------------
/// The overall greenhouse happiness is the simple average of all zone scores.
/// A weighted average option is also provided (weightedAverage toggle) where
/// each zone's contribution is scaled by its zoneWeight field.
///
/// SETUP
/// -----
/// 1. Build your zones (ZoneHealth planes) in the scene.
/// 2. Create an empty GameObject "Greenhouse" and attach this script.
/// 3. Drag all ZoneHealth GameObjects into the zones list in the Inspector.
/// 4. Assign the NegligenceUI script to the ui field.
/// 5. Adjust finish condition thresholds as needed.
/// </summary>
public class GreenhouseHealth : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector fields
    // -------------------------------------------------------------------------

    [Header("Zones")]
    [Tooltip("All ZoneHealth components in this greenhouse. " +
             "Drag zone GameObjects here — order does not matter.")]
    public List<ZoneHealth> zones = new List<ZoneHealth>();

    [Header("Overall score mode")]
    [Tooltip("If false: overall score = simple average of all zone scores.\n" +
             "If true:  overall score = weighted average using each zone's " +
             "zoneWeight field.")]
    public bool weightedAverage = false;

    [Tooltip("Per-zone weights used when weightedAverage is true. " +
             "Must have the same number of entries as zones. " +
             "Values do not need to sum to 1 — they are normalised automatically.")]
    public List<float> zoneWeights = new List<float>();

    [Header("Finish conditions (adjustable)")]
    [Tooltip("CONDITION 1 — Uniform neglect:\n" +
             "The greenhouse is finished if EVERY zone's happiness is at or " +
             "below this value.\n" +
             "Default: 60. Set to 0 to disable this condition.")]
    public float uniformNeglectThreshold = 60f;

    [Tooltip("CONDITION 2 — Dominant neglect:\n" +
             "Happiness threshold a zone must be AT OR BELOW to count as " +
             "'badly neglected' for this condition.\n" +
             "Default: 20.")]
    public float dominantBadThreshold = 20f;

    [Tooltip("CONDITION 2 — Dominant neglect:\n" +
             "Number of zones that must be at or below dominantBadThreshold " +
             "to trigger the finish condition.\n" +
             "Default: 2 (works for a 3-zone greenhouse).")]
    public int dominantBadZoneCount = 2;

    [Header("UI reference")]
    [Tooltip("The NegligenceUI script that renders health bars and icons.")]
    public NegligenceUI ui;

    // -------------------------------------------------------------------------
    // Public read-only state
    // -------------------------------------------------------------------------

    /// <summary>
    /// Overall greenhouse happiness score (0–100), recalculated every time
    /// any zone updates.
    /// </summary>
    public float OverallHappiness { get; private set; } = 100f;

    /// <summary>
    /// True when any finish condition is met. Remains true once triggered
    /// until ResetFinished() is called.
    /// </summary>
    public bool IsFinished { get; private set; } = false;

    /// <summary>
    /// Fired when the greenhouse transitions to the finished state.
    /// Subscribe in a game manager to trigger the end-state sequence.
    /// </summary>
    public event System.Action OnGreenhouseFinished;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Subscribe to each zone's update event so we recalculate the overall
        // score whenever any zone's happiness changes.
        foreach (ZoneHealth zone in zones)
        {
            if (zone != null)
                zone.OnZoneUpdated += OnZoneUpdated;
        }

        // Pad or trim zoneWeights to match the zones list length
        NormaliseWeightsList();
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent stale event callbacks after destruction
        foreach (ZoneHealth zone in zones)
        {
            if (zone != null)
                zone.OnZoneUpdated -= OnZoneUpdated;
        }
    }

    // -------------------------------------------------------------------------
    // Zone update handler
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by any ZoneHealth whenever it finishes an evaluation cycle.
    /// Recalculates the overall greenhouse score and checks finish conditions.
    /// </summary>
    private void OnZoneUpdated(ZoneHealth updatedZone)
    {
        RecalculateOverall();
        CheckFinishConditions();

        // Push fresh data to the UI
        if (ui != null)
            ui.Refresh(this);
    }

    // -------------------------------------------------------------------------
    // Score calculation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Computes the overall greenhouse happiness score from all zone scores.
    /// Uses simple or weighted average based on the weightedAverage toggle.
    /// </summary>
    private void RecalculateOverall()
    {
        if (zones.Count == 0)
        {
            OverallHappiness = 100f;
            return;
        }

        if (!weightedAverage)
        {
            // Simple average — every zone contributes equally
            float total = 0f;
            foreach (ZoneHealth zone in zones)
                total += zone.ZoneHappiness;

            OverallHappiness = total / zones.Count;
        }
        else
        {
            // Weighted average — zones with higher weights contribute more.
            // Formula: sum(score[i] * weight[i]) / sum(weight[i])
            float weightedSum  = 0f;
            float totalWeight  = 0f;

            for (int i = 0; i < zones.Count; i++)
            {
                float w = (i < zoneWeights.Count) ? zoneWeights[i] : 1f;
                weightedSum += zones[i].ZoneHappiness * w;
                totalWeight += w;
            }

            OverallHappiness = totalWeight > 0f
                ? weightedSum / totalWeight
                : 100f;
        }
    }

    // -------------------------------------------------------------------------
    // Finish condition checks
    // -------------------------------------------------------------------------

    /// <summary>
    /// Evaluates both finish conditions against the current zone scores.
    /// Once IsFinished becomes true it stays true — call ResetFinished() to
    /// allow re-triggering (e.g. after a new game starts).
    /// </summary>
    private void CheckFinishConditions()
    {
        if (IsFinished) return; // already finished, no need to re-check

        if (CheckUniformNeglect() || CheckDominantNeglect())
        {
            IsFinished = true;
            Debug.Log("[GreenhouseHealth] Greenhouse has reached a finished/neglected state.");
            OnGreenhouseFinished?.Invoke();
        }
    }

    /// <summary>
    /// CONDITION 1 — Uniform neglect.
    /// Returns true if EVERY zone's happiness is at or below
    /// uniformNeglectThreshold.
    /// </summary>
    private bool CheckUniformNeglect()
    {
        // A threshold of 0 means this condition is disabled
        if (uniformNeglectThreshold <= 0f) return false;

        foreach (ZoneHealth zone in zones)
        {
            // If any zone is ABOVE the threshold, condition is not met
            if (zone.ZoneHappiness > uniformNeglectThreshold)
                return false;
        }

        Debug.Log($"[GreenhouseHealth] Condition 1 met: all zones ≤ " +
                  $"{uniformNeglectThreshold}%.");
        return true;
    }

    /// <summary>
    /// CONDITION 2 — Dominant neglect.
    /// Returns true if at least dominantBadZoneCount zones have happiness
    /// at or below dominantBadThreshold.
    /// </summary>
    private bool CheckDominantNeglect()
    {
        int badZones = 0;

        foreach (ZoneHealth zone in zones)
        {
            if (zone.ZoneHappiness <= dominantBadThreshold)
                badZones++;
        }

        if (badZones >= dominantBadZoneCount)
        {
            Debug.Log($"[GreenhouseHealth] Condition 2 met: {badZones} zones " +
                      $"at or below {dominantBadThreshold}%.");
            return true;
        }

        return false;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Ensures zoneWeights has one entry per zone, padding with 1f or
    /// trimming excess entries.
    /// </summary>
    private void NormaliseWeightsList()
    {
        while (zoneWeights.Count < zones.Count)
            zoneWeights.Add(1f);

        while (zoneWeights.Count > zones.Count)
            zoneWeights.RemoveAt(zoneWeights.Count - 1);
    }

    /// <summary>
    /// Resets the finished state so the greenhouse can be neglected again.
    /// Call this when starting a new game or resetting the scene.
    /// </summary>
    public void ResetFinished()
    {
        IsFinished = false;
    }

    /// <summary>
    /// Forces all zones to evaluate immediately, then recalculates overall.
    /// Useful to call after loading a saved layout so scores are correct
    /// before the first natural evaluation cycle fires.
    /// </summary>
    public void ForceFullEvaluation()
    {
        foreach (ZoneHealth zone in zones)
            zone?.ForceEvaluate();

        RecalculateOverall();
        CheckFinishConditions();

        if (ui != null)
            ui.Refresh(this);
    }
}
