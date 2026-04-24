using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PotContents : MonoBehaviour
{
    // ---------------------------------------------------------------
    [Header("Setup")]
    public Transform plantAnchor;

    [Header("Water")]
    public float plantWaterMax = 10f;
    public float plantWaterStart = 5f;
    public float baseDrainRate = 0.3f;
    public float sunDrainMultiplier = 0.4f;

    // ---------------------------------------------------------------
    [Header("Runtime")]
    [SerializeField] private SoilKind currentSoil = SoilKind.Loam;
    [SerializeField] private float waterLevel;
    [SerializeField] private bool hasSoil = false;
    [SerializeField] private bool hasPlant = false;

    public SoilKind CurrentSoil => currentSoil;
    public float WaterLevel => waterLevel;
    public bool HasSoil => hasSoil;
    public bool HasPlant => hasPlant;

    public PlantState Plant { get; private set; }

    // ---------------------------------------------------------------
    private void Awake()
    {
        waterLevel = plantWaterStart;
    }

    public void Initialise(SoilKind soil)
    {
        currentSoil = soil;
        hasSoil = true;
        waterLevel = plantWaterStart;
    }

    // ---------------------------------------------------------------
    public void SetSoil(SoilKind kind)
    {
        currentSoil = kind;
        hasSoil = true;

        if (Plant != null)
            Plant.OnSoilChanged(currentSoil);
    }

    // ---------------------------------------------------------------
    public void AddPlant(GameObject prefab)
    {
        if (hasPlant) return;

        Transform anchor = plantAnchor != null ? plantAnchor : transform;

        GameObject go = Instantiate(
            prefab,
            anchor.position,
            anchor.rotation,
            anchor
        );

        Plant = go.GetComponent<PlantState>();

        if (Plant == null)
        {
            Destroy(go);
            Debug.LogWarning("[PotContents] Plant prefab missing PlantState.");
            return;
        }

        hasPlant = true;
        Plant.SetPotContents(this);
    }

    public void RemovePlant()
    {
        if (!hasPlant || Plant == null) return;

        Destroy(Plant.gameObject);
        Plant = null;
        hasPlant = false;
    }

    // ---------------------------------------------------------------
    public bool AddWater(float amount)
    {
        if (waterLevel >= plantWaterMax) return false;

        waterLevel = Mathf.Min(waterLevel + amount, plantWaterMax);
        return true;
    }

    // ---------------------------------------------------------------
    private void Update()
    {
        if (waterLevel <= 0f) return;

        float drain = baseDrainRate;

        if (Plant != null && Plant.LightSensor != null)
        {
            drain += Plant.LightSensor.NormalisedIntensity * sunDrainMultiplier;
        }

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