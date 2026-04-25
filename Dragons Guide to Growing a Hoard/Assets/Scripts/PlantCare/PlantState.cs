using UnityEngine;

// =============================================================
// PlantState.cs
// -------------------------------------------------------------
// Tracks the health state of a plant (Revived / Intermediate / Dead)
// by combining soil, light, and water scores.
//
// CHANGES:
//   • SoilScore, LightScore, WaterScore are now public so PlantUI
//     can read individual scores directly (accurate pip display).
//   • SetPotContents() now only sets isOnSoil if the pot actually
//     has soil loaded — respects the "no soil by default" rule.
// =============================================================

// ---------------------------------------------------------------
// PlantStateEnum
// ---------------------------------------------------------------
public enum PlantStateEnum
{
    Revived,
    Intermediate,
    Dead
}

public class PlantState : MonoBehaviour
{
    // ---------------------------------------------------------------
    // INSPECTOR — Soil Preference
    // ---------------------------------------------------------------
    [Header("Soil Preference")]
    public SoilKind preferredSoil = SoilKind.Loam;
    public SoilKind neutralSoil = SoilKind.Clay;

    // ---------------------------------------------------------------
    // INSPECTOR — Light Thresholds
    // ---------------------------------------------------------------
    [Header("Light Thresholds")]
    [Range(0f, 1f)] public float lightThresholdHigh = 0.6f;
    [Range(0f, 1f)] public float lightThresholdLow = 0.25f;

    // ---------------------------------------------------------------
    // INSPECTOR — Water Thresholds
    // ---------------------------------------------------------------
    [Header("Water Thresholds")]
    [Range(0f, 10f)] public float waterThresholdHigh = 6f;
    [Range(0f, 10f)] public float waterThresholdLow = 3f;

    // ---------------------------------------------------------------
    // INSPECTOR — Score Boundaries
    // ---------------------------------------------------------------
    [Header("Score Boundaries")]
    [Range(0, 6)] public int revivedMinScore = 5;
    [Range(0, 6)] public int intermediateMinScore = 2;

    // ---------------------------------------------------------------
    // REFERENCES
    // ---------------------------------------------------------------
    [Header("References")]
    public LightSensor lightSensor;

    // ---------------------------------------------------------------
    // OPTIONAL VISUALS
    // ---------------------------------------------------------------
    [Header("Visuals")]
    public GameObject revivedVisual;
    public GameObject intermediateVisual;
    public GameObject deadVisual;

    // ---------------------------------------------------------------
    // RUNTIME DEBUG
    // ---------------------------------------------------------------
    [Header("Runtime")]
    [SerializeField] private PlantStateEnum currentState = PlantStateEnum.Dead;
    [SerializeField] private SoilKind currentSoil = SoilKind.Sandy;
    [SerializeField] private bool isOnSoil = false;
    [SerializeField] private int lastTotalScore = 0;
    [SerializeField] private int soilScore = 0;
    [SerializeField] private int lightScore = 0;
    [SerializeField] private int waterScore = 0;

    // ---------------------------------------------------------------
    // PUBLIC ACCESSORS
    // PlantUI reads individual scores to drive each pip row accurately.
    // ---------------------------------------------------------------
    public PlantStateEnum CurrentState => currentState;
    public int TotalScore => lastTotalScore;
    public int SoilScore => soilScore;   // 0-2 → mapped to 1-5 pips by PlantUI
    public int LightScore => lightScore;  // 0-2 → mapped to 1-5 pips by PlantUI
    public int WaterScore => waterScore;  // 0-2 → mapped to 1-5 pips by PlantUI
    public LightSensor LightSensor => lightSensor;

    private PotContents ownerPot;

    // ---------------------------------------------------------------
    private void Awake()
    {
        if (lightSensor == null)
            lightSensor = GetComponent<LightSensor>();
    }

    private void Update()
    {
        PlantStateEnum newState = CalculateState();

        if (newState != currentState)
        {
            currentState = newState;
            UpdateVisuals();
            Debug.Log($"[PlantState] State -> {currentState} ({lastTotalScore}/6)");
        }
    }

    // ---------------------------------------------------------------
    // SetPotContents — called by PotContents.AddPlant().
    // Only marks the plant as being on soil if the pot already has
    // soil loaded; otherwise soil score stays 0 until SetSoil() fires.
    // ---------------------------------------------------------------
    public void SetPotContents(PotContents pot)
    {
        ownerPot = pot;

        if (pot.HasSoil)
        {
            isOnSoil = true;
            currentSoil = pot.CurrentSoil;
        }
        else
        {
            isOnSoil = false;
        }
    }

    // Called by PotContents.SetSoil() when the player changes the soil.
    public void OnSoilChanged(SoilKind newSoil)
    {
        currentSoil = newSoil;
        isOnSoil = true;
    }

    // ---------------------------------------------------------------
    // SetVisible — called by grab/pick-up systems to hide the UI
    // while the pot or plant is being held.
    // (Delegates to PlantUI if one is present on the same object.)
    // ---------------------------------------------------------------
    public void SetUIVisible(bool visible)
    {
        PlantUI ui = GetComponent<PlantUI>();
        if (ui != null) ui.SetVisible(visible);
    }

    // ---------------------------------------------------------------
    private PlantStateEnum CalculateState()
    {
        soilScore = CalculateSoilScore();

        float lightLevel = lightSensor != null ? lightSensor.NormalisedIntensity : 0f;
        lightScore = ScoreValue(lightLevel, lightThresholdHigh, lightThresholdLow);

        float waterLevel = ownerPot != null ? ownerPot.WaterLevel : 0f;
        waterScore = ScoreValue(waterLevel, waterThresholdHigh, waterThresholdLow);

        lastTotalScore = soilScore + lightScore + waterScore;

        if (lastTotalScore >= revivedMinScore) return PlantStateEnum.Revived;
        if (lastTotalScore >= intermediateMinScore) return PlantStateEnum.Intermediate;
        return PlantStateEnum.Dead;
    }

    private int CalculateSoilScore()
    {
        if (!isOnSoil) return 0;

        if (currentSoil == preferredSoil) return 2;
        if (currentSoil == neutralSoil) return 1;
        return 0;
    }

    private int ScoreValue(float value, float high, float low)
    {
        if (value >= high) return 2;
        if (value >= low) return 1;
        return 0;
    }

    private void UpdateVisuals()
    {
        if (revivedVisual) revivedVisual.SetActive(currentState == PlantStateEnum.Revived);
        if (intermediateVisual) intermediateVisual.SetActive(currentState == PlantStateEnum.Intermediate);
        if (deadVisual) deadVisual.SetActive(currentState == PlantStateEnum.Dead);
    }

    // ---------------------------------------------------------------
    // Legacy trigger support (soil patches placed directly in the scene)
    // ---------------------------------------------------------------
    private void OnTriggerEnter(Collider other)
    {
        SoilPatch patch = other.GetComponent<SoilPatch>();
        if (patch == null) return;

        isOnSoil = true;
        currentSoil = patch.soilKind;
    }

    private void OnTriggerExit(Collider other)
    {
        SoilPatch patch = other.GetComponent<SoilPatch>();
        if (patch == null) return;

        isOnSoil = false;
    }
}