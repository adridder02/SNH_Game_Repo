using UnityEngine;

// =============================================================
// RoomZoneRegistry.cs
// -------------------------------------------------------------
// The Journal's "rooms" (RoomType.Main/East/West — see PlantSpeciesData.room
// and ProgressPageUIController) and the greenhouse's "zones" (ZoneHealth —
// see Neglect/ZoneHealth.cs, tracks plant happiness/negligence) are two
// separate systems with no existing link between them. This is the one
// place that connects them, so anything that needs "how happy is room X"
// (see SpecialPlantUnlockGate) has a single source of truth instead of
// guessing by zone name.
//
// SETUP:
//   1. Put this on any persistent scene object (e.g. alongside
//      GreenhouseHealth).
//   2. Drag the ZoneHealth that physically corresponds to each journal
//      room into mainZone/eastZone/westZone below.
// =============================================================
public class RoomZoneRegistry : MonoBehaviour
{
    public static RoomZoneRegistry Instance { get; private set; }

    [Header("Room -> Zone mapping")]
    [Tooltip("The ZoneHealth whose happiness represents the Main Hall room.")]
    [SerializeField] private ZoneHealth mainZone;
    [Tooltip("The ZoneHealth whose happiness represents the East room.")]
    [SerializeField] private ZoneHealth eastZone;
    [Tooltip("The ZoneHealth whose happiness represents the West room.")]
    [SerializeField] private ZoneHealth westZone;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    /// <summary>The ZoneHealth linked to a given journal room, or null if unassigned.</summary>
    public ZoneHealth GetZone(RoomType room)
    {
        switch (room)
        {
            case RoomType.Main: return mainZone;
            case RoomType.East: return eastZone;
            case RoomType.West: return westZone;
            default: return null;
        }
    }
}
