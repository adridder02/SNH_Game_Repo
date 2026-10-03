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

    [Header("Sound")]
    [Tooltip("Looping sound played while the pool is actively refilling (not yet full). Stops the " +
             "instant it's full, or the player leaves the water — whichever happens first.")]
    [SerializeField] private AudioClip refillLoopClip;
    [Tooltip("Optional one-shot played the moment the pool actually reaches full, on top of the loop " +
             "above stopping. Leave empty to skip it.")]
    [SerializeField] private AudioClip refillCompleteClip;
    [Tooltip("Auto-added on this GameObject if left empty.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Mission")]
    [Tooltip("'find_water' completes the moment the player enters the water trigger. 'water_refill' " +
             "completes once the pool is full while still inside it. Leave blank to disable reporting.")]
    [SerializeField] private MissionData tutorialMission;

    private int waterLayer;

    private void Awake()
    {
        if (playerInventory == null)
            playerInventory = GetComponent<PlayerInventory>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;

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

        bool wasFull = playerInventory.getWaterPool() >= playerInventory.getMaxWaterPool();

        // Gradual now, rather than instantly maxing the pool every physics tick — see
        // PlayerInventory.refillWaterPoolOverTime's own comment.
        playerInventory.refillWaterPool(Time.deltaTime);

        bool isFull = playerInventory.getWaterPool() >= playerInventory.getMaxWaterPool();

        if (!isFull)
        {
            // Still actively filling — keep the loop going (PlayOneShotLoop below no-ops if it's
            // already playing this same clip, so this is safe to call every tick).
            PlayRefillLoop();
        }
        else if (!wasFull)
        {
            // Just became full THIS tick — stop the loop and fire the one-shot completion sound.
            StopRefillLoop();
            if (refillCompleteClip != null)
                audioSource.PlayOneShot(refillCompleteClip);
        }

        if (tutorialMission != null && isFull)
            MissionProgressManager.Instance?.CompleteOrderedTask(tutorialMission, "water_refill");
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer != waterLayer) return;

        // Stop the fill loop the moment the player leaves the water, even mid-refill — it shouldn't
        // keep playing once they're not actually standing in it anymore.
        StopRefillLoop();
    }

    private void PlayRefillLoop()
    {
        if (refillLoopClip == null || audioSource == null) return;
        if (audioSource.isPlaying && audioSource.clip == refillLoopClip) return; // already going — don't restart it every tick

        audioSource.clip = refillLoopClip;
        audioSource.Play();
    }

    private void StopRefillLoop()
    {
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }
}