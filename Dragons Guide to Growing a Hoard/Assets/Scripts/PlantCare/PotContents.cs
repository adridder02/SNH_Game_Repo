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
// =============================================================

[RequireComponent(typeof(Collider))]
public class PotContents : MonoBehaviour
{
    // ---------------------------------------------------------------
    [Header("Setup")]
    public Transform plantAnchor;

    [Header("Water")]
    public float plantWaterMax = 10f;
    public float baseDrainRate = 0.3f;
    public float sunDrainMultiplier = 0.4f;

    // ---------------------------------------------------------------
    [Header("Runtime")]
    [SerializeField] private SoilKind currentSoil = SoilKind.Loam;
    [SerializeField] private float waterLevel = 0f;   // starts empty
    [SerializeField] private bool hasSoil = false; // starts with no soil
    [SerializeField] private bool hasPlant = false;

    public SoilKind CurrentSoil => currentSoil;
    public float WaterLevel => waterLevel;
    public bool HasSoil => hasSoil;
    public bool HasPlant => hasPlant;

    public PlantState Plant { get; private set; }

    // ---------------------------------------------------------------
    // Awake — pot starts completely empty (no soil, no water, no plant).
    // ---------------------------------------------------------------
    private void Awake()
    {
        waterLevel = 0f;
        hasSoil = false;
        hasPlant = false;
    }

    // ---------------------------------------------------------------
    // Initialise — called by placement systems that pre-load a soil type.
    // Water begins at 0; player must water the plant after placing it.
    // ---------------------------------------------------------------
    public void Initialise(SoilKind soil)
    {
        currentSoil = soil;
        hasSoil = true;
        waterLevel = 0f;
    }

    // ---------------------------------------------------------------
    // SetSoil — called from PotInteraction when the player picks a soil.
    // ---------------------------------------------------------------
    public void SetSoil(SoilKind kind)
    {
        currentSoil = kind;
        hasSoil = true;

        if (Plant != null)
            Plant.OnSoilChanged(currentSoil);
    }

    // ---------------------------------------------------------------
    // AddPlant — spawns the plant prefab at the pot's anchor.
    // ---------------------------------------------------------------
    public void AddPlant(GameObject prefab)
    {
        if (hasPlant) return;

        Transform anchor = plantAnchor != null ? plantAnchor : transform;

        GameObject go = UnityEngine.Object.Instantiate(
            prefab,
            anchor.position,
            anchor.rotation,
            anchor
        );

        Plant = go.GetComponent<PlantState>();

        if (Plant == null)
        {
            UnityEngine.Object.Destroy(go);
            UnityEngine.Debug.LogWarning("[PotContents] Plant prefab missing PlantState.");
            return;
        }

        hasPlant = true;
        Plant.SetPotContents(this);
    }

    public void RemovePlant()
    {
        if (!hasPlant || Plant == null) return;

        UnityEngine.Object.Destroy(Plant.gameObject);
        Plant = null;
        hasPlant = false;
    }

    // ---------------------------------------------------------------
    // AddWater — returns false if already full.
    // ---------------------------------------------------------------
    public bool AddWater(float amount)
    {
        if (waterLevel >= plantWaterMax) return false;

        waterLevel = UnityEngine.Mathf.Min(waterLevel + amount, plantWaterMax);
        return true;
    }

    // ---------------------------------------------------------------
    // Update — water only drains while a plant is in the pot.
    // ---------------------------------------------------------------
    private void Update()
    {
        // No plant, or already dry — nothing to drain.
        if (!hasPlant || waterLevel <= 0f) return;

        float drain = baseDrainRate;

        if (Plant != null && Plant.LightSensor != null)
            drain += Plant.LightSensor.NormalisedIntensity * sunDrainMultiplier;

        waterLevel = UnityEngine.Mathf.Max(0f, waterLevel - drain * UnityEngine.Time.deltaTime);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (plantAnchor == null) return;

        UnityEngine.Gizmos.color = UnityEngine.Color.green;
        UnityEngine.Gizmos.DrawWireSphere(plantAnchor.position, 0.1f);
    }
#endif
}