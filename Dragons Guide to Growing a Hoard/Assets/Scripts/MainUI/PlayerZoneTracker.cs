// =============================================================
// PlayerZoneTracker.cs
// -------------------------------------------------------------
// Attach this to the PLAYER (the dragon) — specifically to (or a child of)
// whichever GameObject has the collider that actually moves around the
// world. NOT PlayerInventory — that lives on the Inventory UI and its
// transform is UI-space, not a world position, which is why the old
// raycast-from-playerInventory.transform approach in MainUIController
// never found a zone.
//
// HOW IT WORKS:
//   Each zone (ZoneHealth) already sits on a GameObject with a trigger
//   BoxCollider. Rather than polling/raycasting for the player's position
//   every frame, this just lets Unity's own physics tell us when the
//   player's collider enters/exits one of those trigger volumes.
//
// REQUIRES:
//   Somewhere in this GameObject's hierarchy there needs to be a
//   Rigidbody (kinematic is fine) for OnTriggerEnter/Exit to fire at
//   all — that's a general Unity physics requirement, not specific to
//   this script. If the player already moves via Rigidbody physics or a
//   CharacterController, this is very likely already satisfied.
//
// SETUP:
//   1. Attach to the player/dragon GameObject (same one whose collider
//      overlaps the zone planes as it flies/walks around).
//   2. In MainUIController, assign this component to playerZoneTracker.
//      MainUIController no longer needs playerInventory for zone
//      detection — only for the water bar.
// =============================================================

using System.Collections.Generic;
using UnityEngine;

public class PlayerZoneTracker : MonoBehaviour
{
    // Every zone the player is currently overlapping, most-recently-entered last. Using a list
    // rather than a single value means that if the player briefly overlaps two zones at once
    // (e.g. crossing the boundary between them), CurrentZone stays on whichever one they entered
    // most recently instead of flickering to null the instant they leave the first one.
    private readonly List<ZoneHealth> _overlappingZones = new List<ZoneHealth>();

    /// <summary>The zone the player is currently standing in, or null if they're in none of them.
    /// Read by MainUIController for the happiness bar.</summary>
    public ZoneHealth CurrentZone =>
        _overlappingZones.Count > 0 ? _overlappingZones[_overlappingZones.Count - 1] : null;

    private void OnTriggerEnter(Collider other)
    {
        ZoneHealth zone = other.GetComponent<ZoneHealth>();
        if (zone != null && !_overlappingZones.Contains(zone))
            _overlappingZones.Add(zone);
    }

    private void OnTriggerExit(Collider other)
    {
        ZoneHealth zone = other.GetComponent<ZoneHealth>();
        if (zone != null)
            _overlappingZones.Remove(zone);
    }
}
