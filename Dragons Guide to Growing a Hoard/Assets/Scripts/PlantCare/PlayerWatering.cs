// =============================================================
// PlayerWatering.cs
// -------------------------------------------------------------
// Attach this to the Player GameObject.
//
// HOW IT WORKS:
//   - The player holds a magical water pool that slowly refills
//     over time (no trips to a well required).
//   - Press Q while near a plant to transfer water from the
//     player's pool into the plant.
//   - The plant's water level slowly drains on its own over time.
//     When a LightSensor is provided, brighter sunlight increases
//     the drain rate (sun evaporates moisture faster).
//
// WATER SCORE (calculated in PlantState.cs, based on CurrentWaterLevel):
//   WaterLevel >= waterThresholdHigh → +2 points (well watered)
//   WaterLevel >= waterThresholdLow  → +1 point  (slightly dry)
//   WaterLevel <  waterThresholdLow  → +0 points (very dry)
//
// SETUP:
//   1. Attach to the Player.
//  ! 2. Drag this component into each plant's PlantState.playerWatering slot.
//   3. Optionally assign the plant's LightSensor to scale drain with sunlight.
// =============================================================

using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerWatering : MonoBehaviour
{
    // ---------------------------------------------------------------
    // INSPECTOR — Player magic water pool
    // ---------------------------------------------------------------
    [Header("Player Water Pool")]
    [Tooltip("Maximum magic water the player can hold.")]
    public float maxWaterPool = 20f;

    [Tooltip("How much water is transferred to the plant per Q press.")]
    public float waterPerPress = 2f;

    [Tooltip("Rate at which the player's magic pool refills per second.")]
    public float poolRefillRate = 1.5f;

    // ---------------------------------------------------------------
    // INSPECTOR — Plant water settings
    // ---------------------------------------------------------------
    [Header("Plant Water")]
    [Tooltip("Starting water level of the plant (0 to plantWaterMax).")]
    public float plantWaterStart = 10f;

    [Tooltip("Maximum water the plant can hold.")]
    public float plantWaterMax = 10f;

    [Tooltip("Base rate at which plant water drains per second, regardless of sunlight.")]
    public float baseDrainRate = 0.3f;

    [Tooltip("Additional drain per second at full sunlight intensity (0 = sun has no effect).")]
    public float sunDrainMultiplier = 0.4f;

    // ---------------------------------------------------------------
    // INSPECTOR — Watering range
    // ---------------------------------------------------------------
    [Header("Watering Range")]
    [Tooltip("Max distance between player and plant for Q to work.")]
    public float wateringRange = 3f;

    // ---------------------------------------------------------------
    // INSPECTOR — Optional light sensor reference
    // Assign the nearby plant's LightSensor, or let PlantState
    // pass it automatically via SetLightSensor().
    // ---------------------------------------------------------------
    [Header("Light Sensor (for sun-based drain)")]
    [Tooltip("Reference to the plant's LightSensor. Used to scale drain with sunlight.")]
    public LightSensor lightSensor;

    // ---------------------------------------------------------------
    // Runtime — visible in Inspector for debugging.
    // ---------------------------------------------------------------
    [Header("Runtime Info (read-only)")]
    [SerializeField] private float playerWaterPool;  // Current magic water
    [SerializeField] private float plantWaterLevel;  // Current plant water

    // ---------------------------------------------------------------
    // Public read-only property used by PlantState.cs to get the
    // current water level for score calculation.
    // ---------------------------------------------------------------
    public float CurrentWaterLevel => plantWaterLevel;

    // The plant currently within watering range (detected each frame).
    private PlantState nearbyPlant;

    // ---------------------------------------------------------------
    // Start — initialise both pools.
    // ---------------------------------------------------------------
    private void Start()
    {
        playerWaterPool = maxWaterPool;   // Player starts fully charged.
        plantWaterLevel = plantWaterStart; // Plant starts at configured level.
    }

    // ---------------------------------------------------------------
    // Update — three responsibilities every frame:
    //   1. Find the nearest plant in range.
    //   2. Handle Q key input.
    //   3. Refill player pool and drain plant water.
    // ---------------------------------------------------------------
    private void Update()
    {
        // --- 1. Find nearest plant in watering range --------------
        FindNearbyPlant();

        // --- 2. Q key: transfer water from player to plant --------
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            TryWaterPlant();
        }

        // --- 3. Passive pool refill and plant water drain ---------
        RefillPlayerPool();
        DrainPlantWater();
    }

    // ---------------------------------------------------------------
    // FindNearbyPlant — scans a sphere around the player each frame
    // and tracks the closest PlantState component within range.
    // ---------------------------------------------------------------
    private void FindNearbyPlant()
    {
        nearbyPlant = null;
        float closestDist = float.MaxValue;

        // OverlapSphere returns all colliders within the watering radius.
        Collider[] hits = Physics.OverlapSphere(transform.position, wateringRange);

        foreach (Collider hit in hits)
        {
            PlantState plant = hit.GetComponent<PlantState>();
            if (plant == null) continue;

            float dist = Vector3.Distance(transform.position, plant.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                nearbyPlant = plant; // Track the single closest plant.
            }
        }
    }

    // ---------------------------------------------------------------
    // TryWaterPlant — transfers water from the player pool to the
    // plant, clamped so neither pool overflows or goes negative.
    // ---------------------------------------------------------------
    private void TryWaterPlant()
    {
        if (nearbyPlant == null)
        {
            Debug.Log("[PlayerWatering] No plant in range. Move closer and press Q.");
            return;
        }

        if (playerWaterPool <= 0f)
        {
            Debug.Log("[PlayerWatering] Magic water pool is empty! Wait for it to refill.");
            return;
        }

        // Calculate the actual transfer: limited by pool, press amount,
        // and remaining space in the plant.
        float spaceInPlant   = plantWaterMax - plantWaterLevel;
        float actualTransfer = Mathf.Min(waterPerPress, playerWaterPool, spaceInPlant);

        if (actualTransfer <= 0f)
        {
            Debug.Log("[PlayerWatering] Plant is already full!");
            return;
        }

        // Apply the transfer.
        plantWaterLevel  += actualTransfer;
        playerWaterPool  -= actualTransfer;

        Debug.Log($"[PlayerWatering] Watered plant +{actualTransfer:F1} | " +
                  $"Plant: {plantWaterLevel:F1}/{plantWaterMax} | " +
                  $"Pool: {playerWaterPool:F1}/{maxWaterPool}");
    }

    // ---------------------------------------------------------------
    // RefillPlayerPool — slowly restores the player's magic water
    // over time so they are never permanently out of water.
    // ---------------------------------------------------------------
    private void RefillPlayerPool()
    {
        playerWaterPool = Mathf.Min(
            playerWaterPool + poolRefillRate * Time.deltaTime,
            maxWaterPool
        );
    }

    // ---------------------------------------------------------------
    // DrainPlantWater — removes water from the plant over time.
    //
    // Total drain rate = baseDrainRate + (sunlight × sunDrainMultiplier)
    //
    // This means:
    //   • Plants always lose some water (natural transpiration).
    //   • On bright sunny days they dry out faster.
    //   • At night or in low light the drain slows to the base rate.
    // ---------------------------------------------------------------
    private void DrainPlantWater()
    {
        if (plantWaterLevel <= 0f) return; // Nothing left to drain.

        // Start with the base drain rate.
        float drain = baseDrainRate;

        // Add sunlight-based drain if a LightSensor is available.
        // NormalisedIntensity is 0–1, so the multiplier scales it naturally.
        if (lightSensor != null)
        {
            drain += lightSensor.NormalisedIntensity * sunDrainMultiplier;
        }

        // Apply the drain, floored at zero.
        plantWaterLevel = Mathf.Max(0f, plantWaterLevel - drain * Time.deltaTime);
    }

    // ---------------------------------------------------------------
    // OnDrawGizmosSelected — draws the watering range sphere in the
    // Scene view so you can see it while selecting the player.
    // ---------------------------------------------------------------
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f); // Transparent blue
        Gizmos.DrawSphere(transform.position, wateringRange);
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.8f);  // Solid outline
        Gizmos.DrawWireSphere(transform.position, wateringRange);
    }
}
