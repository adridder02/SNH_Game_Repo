using UnityEngine;

// =============================================================
// PotContents.cs
// -------------------------------------------------------------
// Manages the state of a single pot: soil, plant, and water.
//
// CHANGES:
//   • Pot starts with NO soil (hasSoil = false, waterLevel = 0)
//     until SetSoil() or Initialise() is explicitly called.
//   • Water only drains when a plant is present (hasPlant).
//   • waterLevel starts at 0; filled by the player via AddWater().
//   • Soil prefabs: assign one prefab per SoilKind in the Inspector.
//     When the player sets a soil type, the matching prefab is
//     spawned at the plantAnchor. Its Renderer material is swapped
//     to the corresponding soil material so each type looks distinct.
//   • potSize uses PlantSize — the single shared size enum defined
//     in PlantState.cs.
//   • Miasma integration: water drain rate is multiplied by
//     miasma's water drain multiplier when plant is affected.
// =============================================================

[RequireComponent(typeof(Collider))]
public class PotContents : MonoBehaviour
{
    // ---------------------------------------------------------------
    [Header("Setup")]
    public Transform plantAnchor;

    [Tooltip("When true this pot is a permanent world object. It cannot be placed or moved via the grid " +
             "system, accepts plants of any size, and is never registered with PlacementSystem.")]
    public bool isStaticPot = false;

    [Tooltip("Optional: the PotData asset this pot instance was spawned from. When assigned, this becomes " +
             "the single source of truth for potSize (see SyncPotSizeFromData below) — leave it null only " +
             "for pots that were hand-authored directly in the scene without going through PotData.")]
    public PotData potData;

    [Tooltip("The physical size of this pot. Ignored for static pots — they accept any plant size. " +
             "If 'Pot Data' above is assigned, this is auto-synced from potData.correspondingPlantSize " +
             "every Awake/OnValidate — don't hand-edit it in that case, it'll just get overwritten.")]
    public PlantSize potSize = PlantSize.Medium;

    [Tooltip("Extra upward nudge applied after surface-snapping. Increase if the plant still clips; decrease if it floats above the soil.")]
    public float plantSurfaceOffset = 0f;

    // ---------------------------------------------------------------
    [Header("Soil Prefabs")]
    [Tooltip("Prefab spawned inside the pot when Clay soil is chosen.")]
    public GameObject claySoilPrefab;

    [Tooltip("Prefab spawned inside the pot when Loam soil is chosen.")]
    public GameObject loamSoilPrefab;

    [Tooltip("Prefab spawned inside the pot when Sandy soil is chosen.")]
    public GameObject sandySoilPrefab;

    [Tooltip("Prefab spawned inside the pot when Water soil is chosen (e.g. a water-surface mesh).")]
    public GameObject waterSoilPrefab;

    [Header("Soil Materials")]
    [Tooltip("Material applied to the clay soil prefab's Renderer. Leave null to keep prefab default.")]
    public Material clayMaterial;

    [Tooltip("Material applied to the loam soil prefab's Renderer. Leave null to keep prefab default.")]
    public Material loamMaterial;

    [Tooltip("Material applied to the sandy soil prefab's Renderer. Leave null to keep prefab default.")]
    public Material sandyMaterial;

    [Tooltip("Material applied to the water soil prefab's Renderer. Leave null to keep prefab default.")]
    public Material waterMaterial;

    // ---------------------------------------------------------------
    [Header("Water")]
    public float plantWaterMax = 10f;
    public float baseDrainRate = 0.3f;
    public float sunDrainMultiplier = 0.4f;

    [Header("Runtime")]
    [Tooltip("Current water level used by PlantState debuffs and scoring")]
    public float waterLevel = 0f;
    
    [Header("Mission")]
    [Tooltip("The mission whose tasks this pot reports into. Assumed task order (index): " +
             "0=RemovedPlant, 1=MovePot, 2=AddedSoil, 3=PlantedSeed, 4=AddedWater. " +
             "Only indices 0/2/3/4 are touched from this script — 1 is reported from PlacementSystem.")]
    [SerializeField] private MissionData tutorialMission;
    // ---------------------------------------------------------------
    [Header("Runtime")]
    [SerializeField] private SoilKind currentSoil = SoilKind.Loam;

    [SerializeField] private bool hasSoil = false;
    [SerializeField] private bool hasPlant = false;

    public SoilKind CurrentSoil => currentSoil;
    public float WaterLevel => waterLevel;
    public bool HasSoil => hasSoil;
    public bool HasPlant => hasPlant;
    public PlantSize PotSize => potSize;
    public bool IsStatic => isStaticPot;

    public PlantState Plant { get; private set; }

    // Live reference to the spawned soil mesh inside the pot.
    private GameObject currentSoilObject;
    [Header("Start Plant")]
    [SerializeField] private GameObject currentPlantPrefab;

    // These fields are set by PlacementSystem when a pot is placed/moved
    [HideInInspector] public Vector3Int GridOrigin;
    [HideInInspector] public GridData GridData;

    // ---------------------------------------------------------------
    // SyncPotSizeFromData — PotData is the source of truth for potSize
    // whenever one is assigned. Called from Awake (runtime) and
    // OnValidate (editor, so the Inspector reflects it immediately
    // after you drag a PotData asset in).
    // ---------------------------------------------------------------
    private void SyncPotSizeFromData()
    {
        if (potData != null)
            potSize = potData.correspondingPlantSize;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SyncPotSizeFromData();
    }
#endif

    // ---------------------------------------------------------------
    // Awake — pot starts completely empty.
    // ---------------------------------------------------------------
    private void Awake()
    {
        SyncPotSizeFromData();

        waterLevel = 0f;
        hasSoil = false;
        hasPlant = false;
        
        if(this.currentPlantPrefab){// aslo add tutorial boollean to say this is only for the tutorial
            SetSoil(currentSoil);
            //currentPlantPrefab = prefab;
            PlantState candidate = currentPlantPrefab.GetComponentInChildren<PlantState>();

            // Static pots accept any plant size; only enforce size for normal grid pots.
            if (!isStaticPot && candidate != null && candidate.plantSize != potSize)
            {
                Debug.LogWarning(
                    $"[PotContents] Size mismatch — plant is {candidate.plantSize} " +
                    $"but pot needs {potSize}. Plant not added.");
                //return false;
            }

            Transform anchor = plantAnchor != null ? plantAnchor : transform;

            // Spawn without a parent first so world-space Renderer bounds are accurate.
            GameObject go = Object.Instantiate(currentPlantPrefab, anchor.position, anchor.rotation);

            Plant = go.GetComponentInChildren<PlantState>();

            if (Plant == null)
            {
                Object.Destroy(go);
                Debug.LogWarning("[PotContents] Plant prefab missing PlantState.");
                //return false;
            }

            // Per-plant placement overrides (set on PlantState in the Inspector).
            float perPlantOffset = Plant.potPlacementOffset;
            Vector3 perPlantEuler = Plant.potPlacementRotation;

            // Apply the per-plant rotation BEFORE measuring bounds so the
            // renderer extents reflect the actual in-pot orientation.
            go.transform.rotation = anchor.rotation * Quaternion.Euler(perPlantEuler);

            // Surface-snap: lift the plant so its lowest renderer bound sits flush
            // with the anchor's Y, preventing it from clipping into the soil.
            // If the prefab has no Renderers the pivot stays at the anchor as-is.
            float lowestY = float.MaxValue;
            foreach (Renderer rend in go.GetComponentsInChildren<Renderer>())
            {
                if (rend.bounds.min.y < lowestY)
                    lowestY = rend.bounds.min.y;
            }

            // Combined offset: pot-wide baseline + per-plant fine-tune.
            float totalOffset = plantSurfaceOffset + perPlantOffset;

            if (lowestY < float.MaxValue)
            {
                float snapOffset = anchor.position.y - lowestY + totalOffset;
                go.transform.position += new Vector3(0f, snapOffset, 0f);
            }
            else if (totalOffset != 0f)
            {
                go.transform.position += new Vector3(0f, totalOffset, 0f);
            }

            // Parent after snapping so the corrected world position is preserved.
            go.transform.SetParent(anchor, worldPositionStays: true);

            hasPlant = true;
            Plant.SetPotContents(this);
        }
    }

    // Called after placement or when a plant is added to this pot
    public void CachePlantReference()
    {
        if (Plant == null)
            Plant = GetComponentInChildren<PlantState>();
    }

    // Optional helper to clear grid references when removed
    public void ClearGridInfo()
    {
        GridData = null;
        GridOrigin = new Vector3Int(0, 0, 0);
    }

    // Example helper used by PlantState.SetPotContents
    public void NotifyPlantSet()
    {
        CachePlantReference();
        if (Plant != null)
            Plant.SetPotContents(this);
    }

    // ---------------------------------------------------------------
    // Initialise — called by placement systems that pre-load a soil type.
    // ---------------------------------------------------------------
    public void Initialise(SoilKind soil)
    {
        currentSoil = soil;
        hasSoil = true;
        waterLevel = (soil == SoilKind.Water) ? plantWaterMax : 0f;
        SpawnSoilPrefab(soil);
    }

    // ---------------------------------------------------------------
    // SetSoil — called from PotInteraction when the player picks a soil.
    // Also resets miasma effects when soil is replaced.
    // ---------------------------------------------------------------
    public void SetSoil(SoilKind kind)
    {
        currentSoil = kind;
        hasSoil = true;

        // Water soil is always saturated — fill immediately on switch.
        if (kind == SoilKind.Water)
            waterLevel = plantWaterMax;

        SpawnSoilPrefab(kind);

        // Reset miasma effects when soil is replaced
        if (Plant != null)
        {
            Plant.ResetMiasmaEffects();
            Plant.OnSoilChanged(currentSoil);
        }

        Debug.Log($"[PotContents] Soil set to {kind} - Miasma effects reset");
    }

    // ---------------------------------------------------------------
    // SpawnSoilPrefab — destroys the previous soil object and spawns
    // the one matching 'kind', then applies the per-kind material.
    // ---------------------------------------------------------------
    private void SpawnSoilPrefab(SoilKind kind)
    {
        // Remove previous soil visual.
        if (currentSoilObject != null)
        {
            Destroy(currentSoilObject);
            currentSoilObject = null;
        }

        GameObject prefabToSpawn = kind switch
        {
            SoilKind.Clay => claySoilPrefab,
            SoilKind.Loam => loamSoilPrefab,
            SoilKind.Sandy => sandySoilPrefab,
            SoilKind.Water => waterSoilPrefab,
            _ => null
        };

        Material materialToApply = kind switch
        {
            SoilKind.Clay => clayMaterial,
            SoilKind.Loam => loamMaterial,
            SoilKind.Sandy => sandyMaterial,
            SoilKind.Water => waterMaterial,
            _ => null
        };

        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"[PotContents] No soil prefab assigned for {kind}. " +
                             "Drag a prefab into the Inspector slot.");
            return;
        }

        Transform anchor = plantAnchor != null ? plantAnchor : transform;

        // Instantiate at anchor position/rotation to preserve prefab's original scale
        currentSoilObject = Instantiate(prefabToSpawn, anchor.position, anchor.rotation);
        currentSoilObject.transform.SetParent(anchor, worldPositionStays: true);

        // Reset to local zero while keeping world scale intact
        currentSoilObject.transform.localPosition = Vector3.zero;
        currentSoilObject.transform.localRotation = Quaternion.identity;
        currentSoilObject.name = $"SoilVisual_{kind}";

        // Apply the soil-specific material to every Renderer in the prefab.
        if (materialToApply != null)
        {
            foreach (Renderer rend in currentSoilObject.GetComponentsInChildren<Renderer>())
                rend.material = materialToApply;
            if (tutorialMission != null && tutorialMission.tasks.Count > 2)
                MissionProgressManager.Instance?.CompleteTask(tutorialMission, tutorialMission.tasks[2]); // AddedSoil
        }
    }

    // ---------------------------------------------------------------
    // AddPlant — returns false if already planted or size mismatches.
    // ---------------------------------------------------------------
    public bool AddPlant(GameObject prefab)
    {
        if (hasPlant) return false;

        currentPlantPrefab = prefab;
        PlantState candidate = prefab.GetComponentInChildren<PlantState>();

        // Static pots accept any plant size; only enforce size for normal grid pots.
        if (!isStaticPot && candidate != null && candidate.plantSize != potSize)
        {
            Debug.LogWarning(
                $"[PotContents] Size mismatch — plant is {candidate.plantSize} " +
                $"but pot needs {potSize}. Plant not added.");
            return false;
        }

        Transform anchor = plantAnchor != null ? plantAnchor : transform;

        // Spawn without a parent first so world-space Renderer bounds are accurate.
        GameObject go = Object.Instantiate(prefab, anchor.position, anchor.rotation);

        Plant = go.GetComponentInChildren<PlantState>();

        if (Plant == null)
        {
            Object.Destroy(go);
            Debug.LogWarning("[PotContents] Plant prefab missing PlantState.");
            return false;
        }

        // Per-plant placement overrides (set on PlantState in the Inspector).
        float perPlantOffset = Plant.potPlacementOffset;
        Vector3 perPlantEuler = Plant.potPlacementRotation;

        // Apply the per-plant rotation BEFORE measuring bounds so the
        // renderer extents reflect the actual in-pot orientation.
        go.transform.rotation = anchor.rotation * Quaternion.Euler(perPlantEuler);

        // Surface-snap: lift the plant so its lowest renderer bound sits flush
        // with the anchor's Y, preventing it from clipping into the soil.
        // If the prefab has no Renderers the pivot stays at the anchor as-is.
        float lowestY = float.MaxValue;
        foreach (Renderer rend in go.GetComponentsInChildren<Renderer>())
        {
            if (rend.bounds.min.y < lowestY)
                lowestY = rend.bounds.min.y;
        }

        // Combined offset: pot-wide baseline + per-plant fine-tune.
        float totalOffset = plantSurfaceOffset + perPlantOffset;

        if (lowestY < float.MaxValue)
        {
            float snapOffset = anchor.position.y - lowestY + totalOffset;
            go.transform.position += new Vector3(0f, snapOffset, 0f);
        }
        else if (totalOffset != 0f)
        {
            go.transform.position += new Vector3(0f, totalOffset, 0f);
        }

        // Parent after snapping so the corrected world position is preserved.
        go.transform.SetParent(anchor, worldPositionStays: true);

        hasPlant = true;
        Plant.SetPotContents(this);
        if (tutorialMission != null && tutorialMission.tasks.Count > 3)
            MissionProgressManager.Instance?.CompleteTask(tutorialMission, tutorialMission.tasks[3]); // PlantedSeed
        return true;
    }

   public void RemovePlant(PlayerInventory dragonInventory)
    {
        if (!hasPlant || Plant == null) return;
        
        // Store reference before destroying
        GameObject plantToDestroy = Plant.gameObject;
        GameObject prefabToReturn = currentPlantPrefab;

        // Read back the icon/image/name PotMenuUIController.ChoosePlant() stashed here at
        // planting time — without this, AddPlantToInventory() below has no icon/name to
        // work with and the returned item falls back to a raw prefab name and a blank slot.
        PlantDisplayInfo info = plantToDestroy != null ? plantToDestroy.GetComponent<PlantDisplayInfo>() : null;

        // DATABASE FALLBACK: info is null for any plant that was planted directly in the scene
        // (PotContents.Awake()'s currentPlantPrefab path) rather than through ChoosePlant() —
        // those never got a PlantDisplayInfo stashed on them. Same resolution CollectablePlant
        // uses: fall back to the prefab's linked PlantSpeciesData (PlantState.journalSpecies),
        // so the plant still comes back with a correct icon/image/name instead of showing up
        // blank in the inventory grid and the pot's Choose Plant panel.
        PlantSpeciesData species = prefabToReturn != null
            ? prefabToReturn.GetComponentInChildren<PlantState>()?.journalSpecies
            : null;

        Sprite icon = info != null && info.icon != null ? info.icon
            : species != null ? species.journalIcon
            : null;

        Sprite displayImage = info != null && info.displayImage != null ? info.displayImage
            : species != null ? (species.journalImage != null ? species.journalImage : species.journalIcon)
            : null;

        string displayName = info != null && !string.IsNullOrEmpty(info.displayName) ? info.displayName
            : species != null ? species.displayName
            : null;

        // Add to inventory first
        if (prefabToReturn != null && dragonInventory != null)
        {
            dragonInventory.AddPlantToInventory(prefabToReturn, icon, displayImage, displayName);
        }
        
        // Clear references BEFORE destroying
        Plant = null;
        currentPlantPrefab = null;
        hasPlant = false;
        
        // Now destroy the plant
        if (plantToDestroy != null)
        {
            if (tutorialMission != null && tutorialMission.tasks.Count > 0)
                MissionProgressManager.Instance?.CompleteTask(tutorialMission, tutorialMission.tasks[0]); // RemovedPlant
            Destroy(plantToDestroy);
        }
    }

    // ---------------------------------------------------------------
    // HarvestPlant — called instead of RemovePlant once the plant's
    // PlantProgress has reached Complete (see PotMenuUIController's
    // "Harvest" button). Deliberately a separate entry point from
    // RemovePlant, even though it does the same thing today, so a real
    // "turn into a different item depending on the plant" system can
    // slot in here later without having to touch PotMenuUIController
    // or the manual-removal path at all.
    // ---------------------------------------------------------------
    public void HarvestPlant(PlayerInventory dragonInventory)
    {
        if (!hasPlant || Plant == null) return;

        string harvestedName = Plant.gameObject.name.Replace("(Clone)", "").Trim();
        Debug.Log($"[PotContents] Harvested '{harvestedName}'. (Dummy: returned to inventory as-is for now — " +
                  "swap this for a real harvested-item lookup once that system exists.)");

        // TODO: once harvested items are their own thing, resolve and grant that item here
        // instead of just returning the plant prefab via RemovePlant's normal inventory path.
        RemovePlant(dragonInventory);
    }

    // ---------------------------------------------------------------
    // AddWater — returns false if already full.
    // ---------------------------------------------------------------
    public bool AddWater(float amount)
    {
        if (waterLevel >= plantWaterMax) return false;
        waterLevel = Mathf.Min(waterLevel + amount, plantWaterMax);
        if (tutorialMission != null && tutorialMission.tasks.Count > 4)
            MissionProgressManager.Instance?.CompleteTask(tutorialMission, tutorialMission.tasks[4]); // AddedWater
        return true;
    }

    // ---------------------------------------------------------------
    // Update — water only drains while a plant is in the pot.
    // Now includes miasma water drain multiplier.
    // ---------------------------------------------------------------
    private void Update()
    {
        if (!hasPlant || waterLevel <= 0f) return;

        // Water soil keeps the medium permanently saturated — no draining.
        if (currentSoil == SoilKind.Water)
        {
            waterLevel = plantWaterMax;
            return;
        }

        float drain = baseDrainRate;

        // Add sun-based drain
        if (Plant != null && Plant.LightSensor != null)
            drain += Plant.LightSensor.NormalisedIntensity * sunDrainMultiplier;

        // Apply miasma water drain multiplier if plant is affected
        if (Plant != null)
            drain *= Plant.GetWaterDrainMultiplier();

        waterLevel = Mathf.Max(0f, waterLevel - drain * Time.deltaTime);
    }

    // ---------------------------------------------------------------
    // ReplaceSoil — public method to manually replace soil and reset miasma
    // ---------------------------------------------------------------
    public void ReplaceSoil(SoilKind newSoil)
    {
        SetSoil(newSoil);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (plantAnchor == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(plantAnchor.position, 0.1f);
    }
#endif
}