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
//     in PlantState.cs. Having one enum eliminates the type-mismatch
//     compiler error that occurred when PotSize and PlantSize were
//     two separate enums with the same values.
// =============================================================

[RequireComponent(typeof(Collider))]
public class PotContents : MonoBehaviour
{
    // ---------------------------------------------------------------
    [Header("Setup")]
    public Transform plantAnchor;

    [Tooltip("The physical size of this pot. Only plants with the matching PlantSize can be planted here.")]
    public PlantSize potSize = PlantSize.Medium;

    // ---------------------------------------------------------------
    [Header("Soil Prefabs")]
    [Tooltip("Prefab spawned inside the pot when Clay soil is chosen.")]
    public GameObject claySoilPrefab;

    [Tooltip("Prefab spawned inside the pot when Loam soil is chosen.")]
    public GameObject loamSoilPrefab;

    [Tooltip("Prefab spawned inside the pot when Sandy soil is chosen.")]
    public GameObject sandySoilPrefab;

    [Header("Soil Materials")]
    [Tooltip("Material applied to the clay soil prefab's Renderer. Leave null to keep prefab default.")]
    public Material clayMaterial;

    [Tooltip("Material applied to the loam soil prefab's Renderer. Leave null to keep prefab default.")]
    public Material loamMaterial;

    [Tooltip("Material applied to the sandy soil prefab's Renderer. Leave null to keep prefab default.")]
    public Material sandyMaterial;

    // ---------------------------------------------------------------
    [Header("Water")]
    public float plantWaterMax = 10f;
    public float baseDrainRate = 0.3f;
    public float sunDrainMultiplier = 0.4f;

    // ---------------------------------------------------------------
    [Header("Runtime")]
    [SerializeField] private SoilKind currentSoil = SoilKind.Loam;
    [SerializeField] private float waterLevel = 0f;
    [SerializeField] private bool hasSoil = false;
    [SerializeField] private bool hasPlant = false;

    public SoilKind CurrentSoil => currentSoil;
    public float WaterLevel => waterLevel;
    public bool HasSoil => hasSoil;
    public bool HasPlant => hasPlant;
    public PlantSize PotSize => potSize;

    public PlantState Plant { get; private set; }

    // Live reference to the spawned soil mesh inside the pot.
    private GameObject currentSoilObject;

    // ---------------------------------------------------------------
    // Awake — pot starts completely empty.
    // ---------------------------------------------------------------
    private void Awake()
    {
        waterLevel = 0f;
        hasSoil = false;
        hasPlant = false;
    }

    // ---------------------------------------------------------------
    // Initialise — called by placement systems that pre-load a soil type.
    // ---------------------------------------------------------------
    public void Initialise(SoilKind soil)
    {
        currentSoil = soil;
        hasSoil = true;
        waterLevel = 0f;
        SpawnSoilPrefab(soil);
    }

    // ---------------------------------------------------------------
    // SetSoil — called from PotInteraction when the player picks a soil.
    // ---------------------------------------------------------------
    public void SetSoil(SoilKind kind)
    {
        currentSoil = kind;
        hasSoil = true;
        SpawnSoilPrefab(kind);

        if (Plant != null)
            Plant.OnSoilChanged(currentSoil);
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
            _ => null
        };

        Material materialToApply = kind switch
        {
            SoilKind.Clay => clayMaterial,
            SoilKind.Loam => loamMaterial,
            SoilKind.Sandy => sandyMaterial,
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
        }
    }

    // ---------------------------------------------------------------
    // AddPlant — returns false if already planted or size mismatches.
    //
    // Both candidate.plantSize and potSize are PlantSize values, so
    // the != comparison is now valid (no cross-enum comparison).
    // ---------------------------------------------------------------
    public bool AddPlant(GameObject prefab)
    {
        if (hasPlant) return false;

        // Size check — both sides are PlantSize, comparison is legal.
        PlantState candidate = prefab.GetComponent<PlantState>();
        if (candidate != null && candidate.plantSize != potSize)
        {
            Debug.LogWarning(
                $"[PotContents] Size mismatch — plant is {candidate.plantSize} " +
                $"but pot needs {potSize}. Plant not added.");
            return false;
        }

        Transform anchor = plantAnchor != null ? plantAnchor : transform;

        // Instantiate without a parent so world scale is applied correctly,
        // then re-parent while keeping the world transform.
        GameObject go = Object.Instantiate(prefab, anchor.position, anchor.rotation);
        go.transform.SetParent(anchor, worldPositionStays: true);

        Plant = go.GetComponent<PlantState>();

        if (Plant == null)
        {
            Object.Destroy(go);
            Debug.LogWarning("[PotContents] Plant prefab missing PlantState.");
            return false;
        }

        hasPlant = true;
        Plant.SetPotContents(this);
        return true;
    }

    public void RemovePlant()
    {
        if (!hasPlant || Plant == null) return;
        Object.Destroy(Plant.gameObject);
        Plant = null;
        hasPlant = false;
    }

    // ---------------------------------------------------------------
    // AddWater — returns false if already full.
    // ---------------------------------------------------------------
    public bool AddWater(float amount)
    {
        if (waterLevel >= plantWaterMax) return false;
        waterLevel = Mathf.Min(waterLevel + amount, plantWaterMax);
        return true;
    }

    // ---------------------------------------------------------------
    // Update — water only drains while a plant is in the pot.
    // ---------------------------------------------------------------
    private void Update()
    {
        if (!hasPlant || waterLevel <= 0f) return;

        float drain = baseDrainRate;
        if (Plant != null && Plant.LightSensor != null)
            drain += Plant.LightSensor.NormalisedIntensity * sunDrainMultiplier;

        waterLevel = Mathf.Max(0f, waterLevel - drain * Time.deltaTime);
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