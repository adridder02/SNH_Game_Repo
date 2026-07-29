using UnityEngine;

// =============================================================
// PlayerWaterSource.cs
// -------------------------------------------------------------
// Attach to the Player, alongside PlayerInventory.
//
// WHY THIS EXISTS:
// PlayerInventory used to try to refill water from OnCollisionEnter,
// checking the other object's layer against "WaterRefill". That
// never fired in practice — OnCollisionEnter only runs for solid
// (non-trigger) collisions, and water volumes are almost always set
// up as trigger colliders so the player can actually swim/wade
// through them instead of colliding with the surface like a wall.
// This script does the same job, correctly, via OnTriggerEnter/Exit.
//
// SETUP:
//   1. Attach this to the Player GameObject (same object as
//      PlayerInventory).
//   2. Make sure your water volume's Collider has "Is Trigger"
//      checked, and its GameObject is on the "WaterRefill" layer
//      (same layer name the old code used — change waterLayerName
//      below if yours is different).
//   3. Assign tutorialMission if you want find_water/water_refill
//      reported — same MissionData asset used on
//      CollectablePlant/HarvestNodeContainer/PotInteraction/
//      PlacementSystem. Leave blank to just fix the refill with no
//      mission reporting.
// =============================================================
[RequireComponent(typeof(PlayerInventory))]
public class PlayerWaterSource : MonoBehaviour
{
    [Tooltip("Auto-found on this GameObject if left empty.")]
    [SerializeField] private PlayerInventory playerInventory;

    [Tooltip("Layer name used on your water volume's GameObject. Must match exactly.")]
    [SerializeField] private string waterLayerName = "WaterRefill";

    [Header("Mission")]
    [Tooltip("'find_water' completes the moment the player enters the water trigger. 'water_refill' " +
             "completes once the pool is full while still inside it. Leave blank to disable reporting.")]
    [SerializeField] private MissionData tutorialMission;

    private int waterLayer;

    private void Awake()
    {
        if (playerInventory == null)
            playerInventory = GetComponent<PlayerInventory>();

        waterLayer = LayerMask.NameToLayer(waterLayerName);
        if (waterLayer < 0)
            Debug.LogWarning($"[PlayerWaterSource] Layer '{waterLayerName}' doesn't exist — check " +
                              "Project Settings > Tags and Layers, or fix the name in the Inspector.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer != waterLayer) return;

        if (tutorialMission != null)
            MissionProgressManager.Instance?.CompleteOrderedTask(tutorialMission, "find_water");
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.layer != waterLayer || playerInventory == null) return;

        playerInventory.refillWaterPool();

        if (tutorialMission != null)
            MissionProgressManager.Instance?.CompleteOrderedTask(tutorialMission, "water_refill");
    }
}
