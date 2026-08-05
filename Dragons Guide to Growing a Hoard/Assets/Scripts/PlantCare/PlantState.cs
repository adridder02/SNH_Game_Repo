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

// The filter "tag" for this plant, used by the inventory's Filter bar
// (sun/moon/wave/skull icons). Deliberately separate from PlantStateEnum
// above — that's the plant's live health status (Revived/Intermediate/Dead),
// this is a fixed classification of what kind of plant it is.
public enum PlantType
{
    Sunny,
    Dark,
    Water,
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

    [Header("Type")]
    [Tooltip("Which filter category this plant belongs to (shown as the sun/moon/wave/skull icons " +
             "in the inventory's Filter bar).")]
    public PlantType plantType = PlantType.Sunny;

    [Header("Journal")]
    [Tooltip("Links this prefab to its journal entry. Optional — if left empty, harvesting this " +
             "plant simply won't unlock anything in the Plants journal.")]
    public PlantSpeciesData journalSpecies;

    [Header("Pot Placement")]
    [Tooltip("Per-plant vertical fine-tune when placed in a pot. Stacks on top of the pot's plantSurfaceOffset. " +
             "Positive = higher, negative = lower.")]
    public float potPlacementOffset = 0f;

    [Tooltip("Euler rotation applied when placed in a pot. Corrects plants that appear rotated " +
             "incorrectly after planting. Example: (0, 90, 0) rotates 90 degrees around Y.")]
    public Vector3 potPlacementRotation = Vector3.zero;

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

    // Reference-counted immunity: several independent sources can grant immunity at once
    // (Windmill Aster's ongoing sunlight-gated radius, Verdant Algae's timed soil ward,
    // a closed Sparkmint circuit) without one source's expiry clobbering another's.
    private readonly HashSet<object> miasmaImmunitySources = new HashSet<object>();

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

    /// <summary>True while any ability source (Windmill Aster, Verdant Algae, a closed Sparkmint
    /// circuit, ...) is actively warding this plant against miasma. See AddMiasmaImmunitySource.</summary>
    public bool IsMiasmaImmune => miasmaImmunitySources.Count > 0;

    /// <summary>Registers 'source' as granting this plant miasma immunity. Pass a stable object
    /// (the ability component itself is fine) so RemoveMiasmaImmunitySource can find it again.
    /// Multiple sources can be active at once; immunity holds until all of them are removed.</summary>
    public void AddMiasmaImmunitySource(object source)
    {
        if (source == null) return;
        bool wasImmune = IsMiasmaImmune;
        miasmaImmunitySources.Add(source);
        if (!wasImmune) ResetMiasmaEffects();
    }

    /// <summary>Un-registers a previously-added immunity source. Safe to call even if it was never added.</summary>
    public void RemoveMiasmaImmunitySource(object source)
    {
        if (source == null) return;
        miasmaImmunitySources.Remove(source);
    }

    /// <summary>0–1 read of overall plant health for UI (PlantUI's health bar) — just the existing
    /// soil+light+water score out of its max of 6, so the bar tracks the same thing CalculateState()
    /// already uses to decide Dead/Intermediate/Revived.</summary>
    public float HealthNormalized01 => Mathf.Clamp01(lastTotalScore / 6f);

    /// <summary>0–1 read of how strongly miasma is currently affecting this plant, for UI (PlantUI's
    /// miasma bar). Blends the three penalty channels into one "how bad is it right now" number rather
    /// than exposing them separately — light/soil are already 0–1-ish, water's drain multiplier is
    /// remapped from its x1–x3 range. Tune the divisors below if miasma's numeric ranges ever change.</summary>
    public float MiasmaInfluence01
    {
        get
        {
            if (!isMiasmaDebuffActive) return 0f;

            float lightPart = Mathf.Clamp01(miasmaLightPenalty);                        // already 0–1
            float soilPart  = Mathf.Clamp01(miasmaSoilPenalty / 5f);                    // soil penalty caps at 5
            float waterPart = Mathf.Clamp01((miasmaWaterDrainMultiplier - 1f) / 2f);    // x1–x3 -> 0–1

            return Mathf.Clamp01((lightPart + soilPart + waterPart) / 3f);
        }
    }

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
        // Warded by an active ability source (Windmill Aster / Verdant Algae / closed Sparkmint
        // circuit) — miasma simply has no effect on this plant right now.
        if (IsMiasmaImmune) return;

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

                DebuffSpec spec = new DebuffSpec
                {
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