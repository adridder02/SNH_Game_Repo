using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------
// PlantState.cs
// Tracks the health state of a plant (Revived / Intermediate / Dead)
// Adds per-prefab debuff behaviour that affects 4-way neighbours.
// ---------------------------------------------------------------

public enum PlantSize
{
    Small,
    Medium,
    Large
}

public enum PlantStateEnum
{
    Revived,
    Intermediate,
    Dead
}

public class PlantState : MonoBehaviour
{
    // ---------------------------------------------------------------
    // INSPECTOR — Size
    // ---------------------------------------------------------------
    [Header("Size")]
    [Tooltip("Physical size of this plant. Must match the pot's PlantSize (potSize) for planting to succeed.")]
    public PlantSize plantSize = PlantSize.Medium;

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
    // Debuff spec (per prefab)
    // ---------------------------------------------------------------
    [System.Serializable]
    public class DebuffSpec
    {
        public bool enabled = false;
        [Tooltip("Seconds between neighbour scans")]
        public float scanInterval = 2f;
        [Tooltip("Amount applied per tick for drain or light penalty")]
        public float amountPerTick = 0.5f;
        [Tooltip("Total duration of the debuff applied to a neighbour")]
        public float duration = 5f;
        [Tooltip("How often the debuff ticks on the target")]
        public float tickInterval = 1f;
        public bool transferStolenWaterToSelf = false;
        public enum Mode { DrainWater, ReduceLight, ReduceSoilQuality }
        public Mode mode = Mode.DrainWater;
    }

    [Header("Debuff (per prefab)")]
    public DebuffSpec debuff = new DebuffSpec();

    // ---------------------------------------------------------------
    // MIASMA EFFECTS
    // ---------------------------------------------------------------
    [Header("Miasma Effects")]
    [SerializeField] private float miasmaLightPenalty = 0f;
    [SerializeField] private int miasmaSoilPenalty = 0;
    [SerializeField] private float miasmaWaterDrainMultiplier = 1f;
    [SerializeField] private bool isMiasmaDebuffActive = false;

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
    // ---------------------------------------------------------------
    public PlantStateEnum CurrentState => currentState;
    public int TotalScore => lastTotalScore;
    public int SoilScore => soilScore;
    public int LightScore => lightScore;
    public int WaterScore => waterScore;
    public LightSensor LightSensor => lightSensor;
    public float GetWaterDrainMultiplier() => isMiasmaDebuffActive ? miasmaWaterDrainMultiplier : 1f;

    private PotContents ownerPot;

    // Debuff runtime state
    private float lightDebuffAmount = 0f;
    private int soilQualityPenalty = 0;
    private Coroutine scannerCoroutine;
    private List<Coroutine> activeDebuffCoroutines = new List<Coroutine>();

    private static readonly Vector3Int[] neighbourOffsets4 = new Vector3Int[] {
        new Vector3Int(1,0,0),
        new Vector3Int(-1,0,0),
        new Vector3Int(0,0,1),
        new Vector3Int(0,0,-1)
    };

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
    // SetPotContents — called by PotContents.AddPlant() or PlacementSystem.
    // Starts/stops the neighbour scanner depending on debuff.enabled.
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

        // Start or stop neighbour scanner
        if (debuff != null && debuff.enabled && ownerPot != null && ownerPot.GridData != null)
        {
            if (scannerCoroutine == null)
                scannerCoroutine = StartCoroutine(NeighbourScanner());
        }
        else
        {
            if (scannerCoroutine != null)
            {
                StopCoroutine(scannerCoroutine);
                scannerCoroutine = null;
            }
        }
    }

    // Called by PotContents.SetSoil() when the player changes the soil.
    public void OnSoilChanged(SoilKind newSoil)
    {
        currentSoil = newSoil;
        isOnSoil = true;
    }

    // ---------------------------------------------------------------
    // MIASMA METHODS
    // ---------------------------------------------------------------
    public void ApplyMiasmaDebuff(float lightPenalty, int soilPenalty, float waterDrainMultiplier)
    {
        // Accumulate penalties over time (permanent until soil replacement)
        miasmaLightPenalty = Mathf.Clamp01(miasmaLightPenalty + lightPenalty);
        miasmaSoilPenalty += soilPenalty;
        miasmaWaterDrainMultiplier = Mathf.Max(1f, waterDrainMultiplier);
        isMiasmaDebuffActive = true;
        
        // Clamp soil penalty to reasonable max
        miasmaSoilPenalty = Mathf.Min(miasmaSoilPenalty, 5);
        
        Debug.Log($"[PlantState] Miasma debuff - Light: -{miasmaLightPenalty}, Soil: -{miasmaSoilPenalty}, Water: x{miasmaWaterDrainMultiplier}");
    }

    public void ResetMiasmaEffects()
    {
        miasmaLightPenalty = 0f;
        miasmaSoilPenalty = 0;
        miasmaWaterDrainMultiplier = 1f;
        isMiasmaDebuffActive = false;
        Debug.Log($"[PlantState] Miasma effects reset for {gameObject.name}");
    }

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
        float adjustedLight = Mathf.Clamp01(lightLevel - lightDebuffAmount - miasmaLightPenalty);
        lightScore = ScoreValue(adjustedLight, lightThresholdHigh, lightThresholdLow);

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
        int baseScore = 0;
        if (currentSoil == preferredSoil) baseScore = 2;
        else if (currentSoil == neutralSoil) baseScore = 1;
        else baseScore = 0;

        // Apply miasma permanent soil penalty
        int totalPenalty = soilQualityPenalty + miasmaSoilPenalty;
        int final = Mathf.Max(0, baseScore - totalPenalty);
        return final;
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
    // Neighbour scanner: finds 4-way neighbours and applies debuffs.
    // ---------------------------------------------------------------
    private IEnumerator NeighbourScanner()
    {
        GridData grid = ownerPot.GridData;

        while (true)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, debuff.scanInterval));

            if (ownerPot == null || ownerPot.GridData == null) yield break;

            Vector3Int origin = ownerPot.GridOrigin;

            foreach (var offset in neighbourOffsets4)
            {
                Vector3Int neighbourCell = origin + offset;
                PlacementData pd = grid.GetPlacement(neighbourCell);
                if (pd == null) continue;

                GameObject neighbourObj = pd.PlacedObject;
                if (neighbourObj == null) continue;

                PotContents neighbourPot = neighbourObj.GetComponent<PotContents>();
                PlantState neighbourPlant = neighbourPot != null ? neighbourPot.Plant : null;

                if (neighbourPlant == null) continue;

                DebuffSpec spec = new DebuffSpec {
                    enabled = true,
                    scanInterval = debuff.scanInterval,
                    amountPerTick = debuff.amountPerTick,
                    duration = debuff.duration,
                    tickInterval = debuff.tickInterval,
                    transferStolenWaterToSelf = debuff.transferStolenWaterToSelf,
                    mode = debuff.mode
                };

                neighbourPlant.ApplyDebuffFromNeighbour(spec, this);
            }
        }
    }

    public void ApplyDebuffFromNeighbour(DebuffSpec spec, PlantState source)
    {
        Coroutine c = StartCoroutine(ReceiveDebuff(spec, source));
        activeDebuffCoroutines.Add(c);
    }

    private IEnumerator ReceiveDebuff(DebuffSpec spec, PlantState source)
    {
        float elapsed = 0f;
        float tick = Mathf.Max(0.01f, spec.tickInterval);

        if (spec.mode == DebuffSpec.Mode.ReduceLight)
            lightDebuffAmount += spec.amountPerTick;
        else if (spec.mode == DebuffSpec.Mode.ReduceSoilQuality)
            soilQualityPenalty += Mathf.RoundToInt(spec.amountPerTick);

        while (elapsed < spec.duration)
        {
            if (spec.mode == DebuffSpec.Mode.DrainWater)
            {
                if (ownerPot != null)
                {
                    float available = ownerPot.WaterLevel;
                    float steal = Mathf.Min(available, spec.amountPerTick);
                    if (steal > 0f)
                    {
                        ownerPot.waterLevel = Mathf.Max(0f, ownerPot.WaterLevel - steal);

                        if (spec.transferStolenWaterToSelf && source != null && source.ownerPot != null)
                        {
                            source.ownerPot.waterLevel += steal;
                        }
                    }
                }
            }

            yield return new WaitForSeconds(tick);
            elapsed += tick;
        }

        if (spec.mode == DebuffSpec.Mode.ReduceLight)
            lightDebuffAmount = Mathf.Max(0f, lightDebuffAmount - spec.amountPerTick);
        else if (spec.mode == DebuffSpec.Mode.ReduceSoilQuality)
            soilQualityPenalty = Mathf.Max(0, soilQualityPenalty - Mathf.RoundToInt(spec.amountPerTick));

        activeDebuffCoroutines.RemoveAll(x => x == null);
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

    private void OnDestroy()
    {
        if (scannerCoroutine != null) StopCoroutine(scannerCoroutine);
        foreach (var c in activeDebuffCoroutines) if (c != null) StopCoroutine(c);
        activeDebuffCoroutines.Clear();
    }
}