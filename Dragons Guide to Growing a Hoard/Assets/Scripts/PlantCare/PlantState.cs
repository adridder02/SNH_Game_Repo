using UnityEngine;

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

    public PlantStateEnum CurrentState => currentState;
    public int TotalScore => lastTotalScore;
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
    public void SetPotContents(PotContents pot)
    {
        ownerPot = pot;
        isOnSoil = true;
        currentSoil = pot.CurrentSoil;
    }

    public void OnSoilChanged(SoilKind newSoil)
    {
        currentSoil = newSoil;
        isOnSoil = true;
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

        if (lastTotalScore >= revivedMinScore)
            return PlantStateEnum.Revived;

        if (lastTotalScore >= intermediateMinScore)
            return PlantStateEnum.Intermediate;

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
    // Legacy trigger support
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