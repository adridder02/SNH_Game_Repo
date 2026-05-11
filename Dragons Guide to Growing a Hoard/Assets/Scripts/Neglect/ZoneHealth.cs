using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Attached to a zone plane GameObject. Automatically detects all plants
/// within its collider bounds using Physics.OverlapBox, reads each plant's
/// PlantStateEnum from the existing PlantState component, converts it to
/// a happiness score, and exposes a negligence level for GreenhouseHealth.
///
/// IMPORTANT: This script reads PlantState.CurrentState directly.
/// PlantHealth.cs is NOT needed — PlantState is the source of truth.
///
/// State → happiness score mapping (adjustable in Inspector):
///   Revived      → scoreRevived      (default 100)
///   Intermediate → scoreIntermediate (default 25)
///   Dead         → scoreDead         (default 0)
///
/// HOW PLANT DETECTION WORKS
/// --------------------------
/// Physics.OverlapBox samples the zone collider's bounds every
/// evaluationInterval seconds and returns all colliders inside.
/// We walk up from each hit collider to the root GameObject and
/// check for a PlantState component — matching how PlantState sits
/// on plant prefab roots. Plants placed or removed by PlantPlacer
/// are picked up automatically on the next cycle.
///
/// ZONE SETUP
/// ----------
/// 1. Create a Plane (or any flat mesh) as your zone.
/// 2. Add a BoxCollider → set Is Trigger = TRUE.
/// 3. Size the collider to cover the cells in this zone.
///    Give it a Y size of ~2 so plants sitting on the surface
///    are inside the detection volume.
/// 4. Attach this script and give the zone a name.
/// 5. Add this zone to GreenhouseHealth.zones list.
///
/// LAYER FILTERING
/// ---------------
/// Assign your plant GameObjects to a dedicated layer (e.g. "Plants")
/// and set plantLayerMask to that layer — OverlapBox will only test
/// against plant colliders, ignoring grid, UI, and other geometry.
/// </summary>
public class ZoneHealth : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector fields
    // -------------------------------------------------------------------------

    [Header("Zone identity")]
    [Tooltip("Display name shown in UI and debug output.")]
    public string zoneName = "Zone A";

    [Header("Happiness scores (adjustable)")]
    [Tooltip("Score a Revived plant contributes to this zone's average.")]
    public float scoreRevived      = 100f;

    [Tooltip("Score an Intermediate plant contributes.")]
    public float scoreIntermediate = 25f;

    [Tooltip("Score a Dead plant contributes.")]
    public float scoreDead         = 0f;

    [Header("Detection settings")]
    [Tooltip("Layer mask for Physics.OverlapBox. Set to the layer your " +
             "plant GameObjects are on (e.g. 'Plants') for best performance. " +
             "Leave as 'Everything' if you have not set up a dedicated layer.")]
    public LayerMask plantLayerMask = ~0;

    [Tooltip("Seconds between each zone re-scan and happiness recalculation. " +
             "Lower = more responsive. Higher = better performance.")]
    public float evaluationInterval = 1f;

    [Header("Negligence thresholds")]
    [Tooltip("Zone happiness below this is Neglected (yellow warning).")]
    public float neglectedThreshold = 40f;

    [Tooltip("Zone happiness below this is Critical (red warning).")]
    public float criticalThreshold  = 20f;

    // -------------------------------------------------------------------------
    // Public read-only state (read by GreenhouseHealth and NegligenceUI)
    // -------------------------------------------------------------------------

    /// <summary>Average happiness score of all plants in this zone (0–100).</summary>
    public float ZoneHappiness { get; private set; } = 100f;

    /// <summary>Number of plants currently detected in this zone.</summary>
    public int PlantCount { get; private set; } = 0;

    /// <summary>Current negligence level derived from ZoneHappiness.</summary>
    public NegligenceLevel Negligence { get; private set; } = NegligenceLevel.Healthy;

    /// <summary>
    /// Fired every evaluation cycle. GreenhouseHealth subscribes here
    /// to know when to recalculate the overall score.
    /// </summary>
    public event System.Action<ZoneHealth> OnZoneUpdated;

    // -------------------------------------------------------------------------
    // Negligence level
    // -------------------------------------------------------------------------

    public enum NegligenceLevel { Healthy, Neglected, Critical }

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private BoxCollider _boxCollider;
    private float       _evaluationTimer = 0f;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _boxCollider = GetComponent<BoxCollider>();

        if (_boxCollider == null)
        {
            Debug.LogWarning($"[ZoneHealth] '{zoneName}' has no BoxCollider — adding one. " +
                             "Please size it in the Inspector to cover this zone.");
            _boxCollider = gameObject.AddComponent<BoxCollider>();
        }

        // Must be a trigger so plants can physically overlap it without being
        // pushed away by the collider.
        if (!_boxCollider.isTrigger)
        {
            _boxCollider.isTrigger = true;
            Debug.Log($"[ZoneHealth] '{zoneName}': BoxCollider.isTrigger set to true.");
        }
    }

    private void Update()
    {
        _evaluationTimer += Time.deltaTime;
        if (_evaluationTimer >= evaluationInterval)
        {
            _evaluationTimer = 0f;
            Evaluate();
        }
    }

    // -------------------------------------------------------------------------
    // Core evaluation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scans for PlantState components inside this zone's bounds,
    /// reads each plant's CurrentState, converts it to a score,
    /// averages them, and derives the negligence level.
    ///
    /// Called on the interval automatically. Call ForceEvaluate()
    /// to trigger immediately (e.g. right after a plant state changes).
    /// </summary>
    public void Evaluate()
    {
        List<PlantState> plants = GetPlantsInZone();
        PlantCount = plants.Count;

        if (PlantCount == 0)
        {
            // Empty zone → treat as fully happy (nothing to neglect)
            ZoneHappiness = 100f;
        }
        else
        {
            // Sum each plant's score and divide by count for the average
            float total = 0f;
            foreach (PlantState plant in plants)
                total += StateToScore(plant.CurrentState);

            ZoneHappiness = total / PlantCount;
        }

        // Derive negligence level from the score
        if (ZoneHappiness >= neglectedThreshold)
            Negligence = NegligenceLevel.Healthy;
        else if (ZoneHappiness >= criticalThreshold)
            Negligence = NegligenceLevel.Neglected;
        else
            Negligence = NegligenceLevel.Critical;

        // Notify GreenhouseHealth and any other listeners
        OnZoneUpdated?.Invoke(this);
    }

    /// <summary>
    /// Triggers an immediate evaluation outside the normal interval.
    /// Useful after a PlantState changes so the UI updates without
    /// waiting for the next cycle.
    /// </summary>
    public void ForceEvaluate()
    {
        _evaluationTimer = 0f;
        Evaluate();
    }

    // -------------------------------------------------------------------------
    // Plant detection
    // -------------------------------------------------------------------------

    /// <summary>
    /// Uses Physics.OverlapBox to find all colliders inside this zone's
    /// BoxCollider bounds, then filters for any that have a PlantState
    /// component on them or on their root GameObject.
    ///
    /// Walking up to the root with GetComponentInParent handles the case
    /// where the collider is on a child of the plant prefab (e.g. the pot
    /// body) while PlantState sits on the root.
    /// </summary>
    private List<PlantState> GetPlantsInZone()
    {
        // Convert the BoxCollider's local centre to world space
        Vector3 worldCentre = transform.TransformPoint(_boxCollider.center);

        // Scale the half-extents by the zone's world scale so the detection
        // box matches the visual size of the plane even if it has been scaled.
        Vector3 halfExtents = Vector3.Scale(
            _boxCollider.size * 0.5f,
            transform.lossyScale
        );

        // Sample all colliders inside the box
        Collider[] hits = Physics.OverlapBox(
            worldCentre,
            halfExtents,
            transform.rotation,
            plantLayerMask,
            QueryTriggerInteraction.Collide // also detect trigger colliders
        );

        List<PlantState> plants = new List<PlantState>();

        foreach (Collider col in hits)
        {
            // Check the hit object first, then walk up to the root.
            // PlantState is on the plant's root GameObject; the collider
            // may be on a child (pot mesh, leaf mesh, etc.).
            PlantState state = col.GetComponent<PlantState>()
                            ?? col.GetComponentInParent<PlantState>();

            // Deduplicate — multiple collider children on one plant root
            // would otherwise add the same PlantState more than once.
            if (state != null && !plants.Contains(state))
                plants.Add(state);
        }

        return plants;
    }

    // -------------------------------------------------------------------------
    // Score conversion
    // -------------------------------------------------------------------------

    /// <summary>
    /// Maps a PlantStateEnum value (from PlantState.CurrentState) to the
    /// happiness score defined in this zone's Inspector fields.
    /// All three score values are adjustable without recompiling.
    /// </summary>
    private float StateToScore(PlantStateEnum state)
    {
        switch (state)
        {
            case PlantStateEnum.Revived:      return scoreRevived;
            case PlantStateEnum.Intermediate: return scoreIntermediate;
            case PlantStateEnum.Dead:         return scoreDead;
            default:                          return 0f;
        }
    }

    // -------------------------------------------------------------------------
    // Editor visualisation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Draws the detection volume as a coloured wire cube in the Scene view.
    /// Green = Healthy, Yellow = Neglected, Red = Critical.
    /// Zero runtime cost — editor-only.
    /// </summary>
    private void OnDrawGizmos()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) return;

        switch (Negligence)
        {
            case NegligenceLevel.Healthy:   Gizmos.color = new Color(0.2f, 0.9f, 0.2f, 0.25f); break;
            case NegligenceLevel.Neglected: Gizmos.color = new Color(1.0f, 0.9f, 0.0f, 0.25f); break;
            case NegligenceLevel.Critical:  Gizmos.color = new Color(1.0f, 0.1f, 0.1f, 0.25f); break;
        }

        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(
            transform.TransformPoint(col.center),
            transform.rotation,
            transform.lossyScale
        );
        Gizmos.DrawCube(Vector3.zero, col.size);
        Gizmos.DrawWireCube(Vector3.zero, col.size);
        Gizmos.matrix = prev;
    }
}
