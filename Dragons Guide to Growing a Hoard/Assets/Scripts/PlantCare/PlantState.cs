// =============================================================
// PlantState.cs
// -------------------------------------------------------------
// Attach this to your Plant GameObject.
//
// HOW THE HEALTH SCORE WORKS:
//   All three factors — soil, light, and water — contribute points
//   to a total health score (0–6). The score determines the state:
//
//   SOIL (0–2 pts):
//     Preferred soil  → +2   (ideal for this plant species)
//     Neutral soil    → +1   (plant can tolerate it)
//     Wrong soil      → +0   (wrong type, no contribution)
//
//   LIGHT (0–2 pts):
//     NormalisedIntensity >= lightThresholdHigh → +2  (bright)
//     NormalisedIntensity >= lightThresholdLow  → +1  (dim)
//     NormalisedIntensity <  lightThresholdLow  → +0  (dark)
//
//   WATER (0–2 pts):
//     CurrentWaterLevel >= waterThresholdHigh   → +2  (well watered)
//     CurrentWaterLevel >= waterThresholdLow    → +1  (slightly dry)
//     CurrentWaterLevel <  waterThresholdLow    → +0  (very dry)
//
//   TOTAL → STATE:
//     5–6 pts → Revived      (thriving)
//     2–4 pts → Intermediate (surviving)
//     0–1 pts → Dead         (wilting)
//
//   Tune the thresholds and score boundaries in the Inspector to
//   balance your game.
//
// DEPENDENCIES:
//   SoilType.cs, LightSensor.cs, PlayerWatering.cs must all be
//   present in the project.
//
// SETUP:
// !  1. Attach to each plant.
// !  2. Attach LightSensor.cs to the same GameObject.
//   3. Drag the Player's PlayerWatering into the playerWatering slot.
//   4. Set preferredSoil and neutralSoil for this plant species.
//   5. Optionally assign revivedVisual, intermediateVisual, deadVisual.
// =============================================================

using UnityEngine;

// ------------------------------------------------------------------
// PlantStateEnum — the three visible states of the plant.
// ------------------------------------------------------------------
public enum PlantStateEnum
{
    Revived,        // Healthy and thriving
    Intermediate,   // Surviving but not flourishing
    Dead            // Wilting or dead
}

public class PlantState : MonoBehaviour
{
    // ---------------------------------------------------------------
    // INSPECTOR — Soil preference for this plant species
    // ---------------------------------------------------------------
    [Header("Soil Preference")]
    [Tooltip("The soil type this plant thrives in (contributes +2 to score).")]
    public SoilKind preferredSoil = SoilKind.Loam;

    [Tooltip("A soil type the plant can tolerate (contributes +1 to score). " +
             "The third type is automatically treated as wrong (+0).")]
    public SoilKind neutralSoil = SoilKind.Clay;

    // ---------------------------------------------------------------
    // INSPECTOR — Light score thresholds
    // ---------------------------------------------------------------
    [Header("Light Thresholds")]
    [Tooltip("NormalisedIntensity (0–1) at or above which the plant gets +2 light points.")]
    [Range(0f, 1f)] public float lightThresholdHigh = 0.6f;

    [Tooltip("NormalisedIntensity at or above which the plant gets +1 light point.")]
    [Range(0f, 1f)] public float lightThresholdLow  = 0.25f;

    // ---------------------------------------------------------------
    // INSPECTOR — Water score thresholds
    // ---------------------------------------------------------------
    [Header("Water Thresholds")]
    [Tooltip("Water level (0–10) at or above which the plant gets +2 water points.")]
    [Range(0f, 10f)] public float waterThresholdHigh = 6f;

    [Tooltip("Water level at or above which the plant gets +1 water point.")]
    [Range(0f, 10f)] public float waterThresholdLow  = 3f;

    // ---------------------------------------------------------------
    // INSPECTOR — State score boundaries
    // Tune these to make the game easier or harder.
    // ---------------------------------------------------------------
    [Header("Score → State Boundaries  (max score is 6)")]
    [Tooltip("Minimum total score required for the Revived state.")]
    [Range(0, 6)] public int revivedMinScore       = 5;

    [Tooltip("Minimum total score required for the Intermediate state.")]
    [Range(0, 6)] public int intermediateMinScore  = 2;

    // Any score below intermediateMinScore results in Dead.

    // ---------------------------------------------------------------
    // INSPECTOR — External component references
    // ---------------------------------------------------------------
    [Header("Component References")]
    [Tooltip("LightSensor on this plant. Auto-found if left empty.")]
    public LightSensor lightSensor;

    [Tooltip("PlayerWatering on the player. Must be dragged in manually.")]
    public PlayerWatering playerWatering;

    // ---------------------------------------------------------------
    // INSPECTOR — Optional visual GameObjects
    // Swap these for your actual plant mesh variants or prefabs.
    // ---------------------------------------------------------------
    [Header("Visuals (optional)")]
    [Tooltip("Shown when plant is Revived.")]
    public GameObject revivedVisual;

    [Tooltip("Shown when plant is Intermediate.")]
    public GameObject intermediateVisual;

    [Tooltip("Shown when plant is Dead.")]
    public GameObject deadVisual;

    // ---------------------------------------------------------------
    // Runtime — visible in Inspector for live debugging.
    // ---------------------------------------------------------------
    [Header("Runtime Info (read-only)")]
    [SerializeField] private PlantStateEnum currentState  = PlantStateEnum.Dead;
    [SerializeField] private SoilKind       currentSoil   = SoilKind.Sandy;
    [SerializeField] private bool           isOnSoil      = false;
    [SerializeField] private int            lastTotalScore = 0;
    [SerializeField] private int            soilScore     = 0;
    [SerializeField] private int            lightScore    = 0;
    [SerializeField] private int            waterScore    = 0;

    // Public read-only access for other systems (UI, achievements, etc.).
    public PlantStateEnum CurrentState => currentState;
    public int            TotalScore   => lastTotalScore;

    // ---------------------------------------------------------------
    // Awake — auto-find LightSensor; warn if PlayerWatering is missing.
    // ---------------------------------------------------------------
    private void Awake()
    {
        if (lightSensor == null)
            lightSensor = GetComponent<LightSensor>();

        if (playerWatering == null)
            Debug.LogWarning("[PlantState] PlayerWatering is not assigned! " +
                             "Drag the player's PlayerWatering component into the Inspector.");
    }

    // ---------------------------------------------------------------
    // Update — recalculate the health score and state every frame.
    // ---------------------------------------------------------------
    private void Update()
    {
        PlantStateEnum newState = CalculateState();

        // Only call UpdateVisuals when the state actually changes,
        // to avoid redundant SetActive calls every frame.
        if (newState != currentState)
        {
            currentState = newState;
            UpdateVisuals();
            Debug.Log($"[PlantState] State → {currentState} (score: {lastTotalScore}/6)");
        }
    }

    // ---------------------------------------------------------------
    // CalculateState — scores all three factors and returns a state.
    //
    // Each factor independently contributes 0, 1, or 2 points.
    // The total (0–6) maps to Dead, Intermediate, or Revived.
    // ---------------------------------------------------------------
    private PlantStateEnum CalculateState()
    {
        // ── Soil score ─────────────────────────────────────────────
        soilScore = CalculateSoilScore();

        // ── Light score ────────────────────────────────────────────
        float lightLevel = (lightSensor != null) ? lightSensor.NormalisedIntensity : 0f;
        lightScore = ScoreValue(lightLevel, lightThresholdHigh, lightThresholdLow);

        // ── Water score ────────────────────────────────────────────
        float waterLevel = (playerWatering != null) ? playerWatering.CurrentWaterLevel : 0f;
        waterScore = ScoreValue(waterLevel, waterThresholdHigh, waterThresholdLow);

        // ── Total score ────────────────────────────────────────────
        lastTotalScore = soilScore + lightScore + waterScore; // Range: 0–6

        // ── Map score to state ─────────────────────────────────────
        if (lastTotalScore >= revivedMinScore)
            return PlantStateEnum.Revived;

        if (lastTotalScore >= intermediateMinScore)
            return PlantStateEnum.Intermediate;

        return PlantStateEnum.Dead;
    }

    // ---------------------------------------------------------------
    // CalculateSoilScore — translates current soil into 0, 1, or 2.
    // ---------------------------------------------------------------
    private int CalculateSoilScore()
    {
        // Not on any recognised soil patch → no contribution.
        if (!isOnSoil) return 0;

        if (currentSoil == preferredSoil) return 2; // Perfect soil
        if (currentSoil == neutralSoil)   return 1; // Tolerable soil
        return 0;                                    // Wrong soil
    }

    // ---------------------------------------------------------------
    // ScoreValue — generic helper that turns a float value into
    // a score of 2 (good), 1 (okay), or 0 (poor) based on thresholds.
    //
    // Used for both light and water to keep logic consistent.
    // ---------------------------------------------------------------
    private int ScoreValue(float value, float highThreshold, float lowThreshold)
    {
        if (value >= highThreshold) return 2; // Above high threshold → good
        if (value >= lowThreshold)  return 1; // Between thresholds   → okay
        return 0;                             // Below low threshold  → poor
    }

    // ---------------------------------------------------------------
    // UpdateVisuals — activates only the mesh matching the current
    // state and deactivates the other two.
    // ---------------------------------------------------------------
    private void UpdateVisuals()
    {
        // If no visuals are assigned, log only — don't throw an error.
        if (!revivedVisual && !intermediateVisual && !deadVisual) return;

        if (revivedVisual)
            revivedVisual.SetActive(currentState == PlantStateEnum.Revived);

        if (intermediateVisual)
            intermediateVisual.SetActive(currentState == PlantStateEnum.Intermediate);

        if (deadVisual)
            deadVisual.SetActive(currentState == PlantStateEnum.Dead);
    }

    // ===============================================================
    // TRIGGER DETECTION
    // Detects which SoilPatch the plant is standing on.
    //
    // Requirements:
    //   • Plant needs a Collider at its base (Sphere or Box).
    //   • Soil plane needs a Collider with "Is Trigger" ticked.
    //   • Add a Rigidbody to the plant with all axes constrained,
    //     OR make the plant's collider a trigger — Unity needs at
    //     least one Rigidbody for OnTrigger events to fire.
    // ===============================================================

    private void OnTriggerEnter(Collider other)
    {
        SoilPatch patch = other.GetComponent<SoilPatch>();
        if (patch == null) return;

        isOnSoil   = true;
        currentSoil = patch.soilKind;
        Debug.Log($"[PlantState] Entered {currentSoil} soil.");
    }

    private void OnTriggerExit(Collider other)
    {
        SoilPatch patch = other.GetComponent<SoilPatch>();
        if (patch == null) return;

        isOnSoil = false;
        Debug.Log("[PlantState] Left soil patch.");
    }
}
