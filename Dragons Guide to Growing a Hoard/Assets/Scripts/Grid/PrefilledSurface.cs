using System.Collections.Generic;
using UnityEngine;

// =============================================================
// PrefilledSurface.cs
// -------------------------------------------------------------
// Put this on a shelf/table that starts with pots (and harvest-node plants) already on it.
// The player can't place anything on the surface until every one of those pots has been cleared:
//   1. harvest the plant   (HarvestNodeContainer, E)
//   2. pick up the pot     (PotInteraction, E — pots with "Removable Once Cleared" ticked)
// When the last pot is gone the surface unlocks and works exactly like any other GreenhouseSurface.
//
// SETUP:
//   • Add to the object that is a parent of both the GreenhouseSurface and the pots (or just assign
//     them below). Leave Pots empty to auto-collect every PotContents under this object.
//   • Tick "Removable Once Cleared" on those pots (this component does it for you if Auto Mark is on).
// =============================================================
public class PrefilledSurface : MonoBehaviour
{
    [Tooltip("The surface that stays locked until the pots below are gone. Auto-found on this object, " +
             "its children, or its parents if left empty.")]
    [SerializeField] private GreenhouseSurface surface;

    [Tooltip("The pre-placed pots on this surface. Leave empty to auto-collect every PotContents " +
             "underneath this object.")]
    [SerializeField] private List<PotContents> pots = new List<PotContents>();

    [Tooltip("Tick 'Removable Once Cleared' on every pot in the list automatically.")]
    [SerializeField] private bool autoMarkPotsRemovable = true;

    private int remaining;

    public bool IsCleared => remaining <= 0;

    private void Awake()
    {
        if (surface == null) surface = GetComponent<GreenhouseSurface>();
        if (surface == null) surface = GetComponentInChildren<GreenhouseSurface>();
        if (surface == null) surface = GetComponentInParent<GreenhouseSurface>();

        if (pots.Count == 0)
            pots.AddRange(GetComponentsInChildren<PotContents>(true));

        pots.RemoveAll(p => p == null);

        if (autoMarkPotsRemovable)
            foreach (PotContents p in pots) p.removableOnceCleared = true;

        remaining = pots.Count;
    }

    private void Start()
    {
        if (surface == null)
        {
            Debug.LogWarning("[PrefilledSurface] No GreenhouseSurface found/assigned.", this);
            return;
        }
        surface.SetPlacementLocked(remaining > 0);
    }

    /// <summary>Called by PotContents.PickUpPot().</summary>
    public void NotifyPotRemoved(PotContents pot)
    {
        if (pot == null || !pots.Remove(pot)) return;
        remaining = pots.Count;
        if (remaining <= 0 && surface != null)
        {
            surface.SetPlacementLocked(false);
            Debug.Log($"[PrefilledSurface] '{name}' cleared - surface is now open for placement.", this);
        }
    }
}
