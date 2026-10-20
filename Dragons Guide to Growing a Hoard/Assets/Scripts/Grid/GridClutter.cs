using System.Collections.Generic;
using UnityEngine;

// =============================================================
// GridClutter.cs
// -------------------------------------------------------------
// Put this on any object that should BLOCK a placement grid until it's cleared (decorative pots, props,
// a pot with a plant on it, ...). You choose the objects; each one works out for itself which grid(s) it
// is sitting on and locks them. A grid with any uncleared GridClutter on it is locked (drawn dim, can't be
// placed on); when the last one is gone it unlocks.
//
// CLEARED = this object is deactivated or destroyed (picked up, harvested away, cleared with E, ...).
//
// HOW THE PLAYER CLEARS IT
//   - Plain props: walk up, the object outlines and a "Clear" prompt shows; E clears it (can be switched off
//     with Player Can Clear if something else removes it).
//   - Pots / harvest-node plants keep their own interaction (pick up the pot, harvest the plant) and don't
//     get the Clear prompt. A pot with this on is automatically made pick-up-able once its plant is gone.
//
// SETUP: add this to the object (usually the root of the prop). Nothing else. Optionally assign Grids to
// pin it to specific grids instead of auto-detecting.
// =============================================================
public class GridClutter : MonoBehaviour
{
    [Tooltip("Leave EMPTY to auto-detect: the grid(s) this object is sitting on. Assign to force specific grids.")]
    [SerializeField] private GreenhouseSurface[] grids;

    [Tooltip("How far (world units) above/below a grid's surface the object can be and still count as on it.")]
    [SerializeField] private float heightTolerance = 0.25f;

    [Tooltip("The player can clear this with E (outline + prompt). Turn OFF if something else removes it. Forced " +
             "off for pots and harvest-node plants, which have their own interaction.")]
    [SerializeField] private bool playerCanClear = true;

    [Tooltip("Prompt shown when the player is next to this.")]
    public string promptLabel = "Clear";

    [Tooltip("Outline material for the highlight. Leave empty to reuse the one from any OutlineEffect in the scene.")]
    [SerializeField] private Material outlineMaterial;

    [Tooltip("If this is (or contains) a pot, set it to 'Removable Once Cleared' so the player can pick it up.")]
    [SerializeField] private bool autoMarkPotRemovable = true;

    private readonly List<GreenhouseSurface> registered = new List<GreenhouseSurface>();
    private OutlineEffect outline;
    private bool ownInteraction;

    /// <summary>True if the player can clear this with E (and it should show the prompt).</summary>
    public bool CanBeClearedByPlayer => playerCanClear && ownInteraction && !TouchingUncollectedPlant();

    private static readonly List<CollectablePlant> plantScratch = new List<CollectablePlant>();

    /// <summary>True while a collectable plant (not yet collected) overlaps this clutter. Collected plants are
    /// deactivated, so once the player picks it up this goes false and the clutter becomes clearable.</summary>
    private bool TouchingUncollectedPlant()
    {
        if (!TryGetBounds(out Bounds b)) return false;
        b.Expand(0.1f);
        CollectablePlant[] plants = FindObjectsByType<CollectablePlant>();
        for (int i = 0; i < plants.Length; i++)
        {
            CollectablePlant p = plants[i];
            if (p == null || !p.gameObject.activeInHierarchy) continue;
            if (p.transform.IsChildOf(transform) || transform.IsChildOf(p.transform)) continue;
            Collider c = p.GetComponentInChildren<Collider>();
            Bounds pb;
            if (c != null) pb = c.bounds;
            else
            {
                Renderer r = p.GetComponentInChildren<Renderer>();
                if (r == null) continue;
                pb = r.bounds;
            }
            if (b.Intersects(pb)) return true;
        }
        return false;
    }

    private void Start()
    {
        PotContents pot = GetComponentInChildren<PotContents>(true);
        if (pot == null) pot = GetComponentInParent<PotContents>();
        bool hasOwnInteraction = pot != null ||
                                 GetComponentInChildren<CollectablePlant>(true) != null ||
                                 GetComponentInParent<CollectablePlant>() != null;
        ownInteraction = !hasOwnInteraction;

        if (pot != null && autoMarkPotRemovable) pot.removableOnceCleared = true;

        if (grids != null && grids.Length > 0)
        {
            foreach (GreenhouseSurface g in grids) Register(g);
        }
        else if (TryGetBounds(out Bounds b))
        {
            foreach (GreenhouseSurface g in FindObjectsByType<GreenhouseSurface>())
                if (g != null && TouchesGrid(g, b)) Register(g);
        }

        if (registered.Count == 0)
            Debug.LogWarning($"[GridClutter] '{name}' isn't touching any grid, so it doesn't lock anything. " +
                             "Move it onto a grid, or assign Grids by hand.", this);
    }

    private void Register(GreenhouseSurface g)
    {
        if (g == null || registered.Contains(g)) return;
        registered.Add(g);
        g.RegisterClutter(this);
    }

    private void OnDisable() { ReleaseAll(); }
    private void OnDestroy() { ReleaseAll(); }

    private void ReleaseAll()
    {
        foreach (GreenhouseSurface g in registered)
            if (g != null) g.UnregisterClutter(this);
        registered.Clear();
    }

    // ---- bounds / grid overlap ----
    private bool TryGetBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
        }
        if (!any)
            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                if (!any) { bounds = c.bounds; any = true; } else bounds.Encapsulate(c.bounds);
            }
        return any;
    }

    private bool TouchesGrid(GreenhouseSurface g, Bounds b)
    {
        // Vertically: the object must span (or rest on) the grid plane.
        float gy = g.GridOriginWorld.y;
        if (b.min.y > gy + heightTolerance || b.max.y < gy - heightTolerance) return false;

        // Horizontally: the object's footprint, in the grid's own (possibly rotated) cell space, overlaps the grid.
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? b.min.x : b.max.x, gy, (i & 2) == 0 ? b.min.z : b.max.z);
            Vector2 c = g.WorldToGridCells(corner);
            if (c.x < minX) minX = c.x; if (c.x > maxX) maxX = c.x;
            if (c.y < minZ) minZ = c.y; if (c.y > maxZ) maxZ = c.y;
        }

        Vector2Int dims = g.GridDimensions;
        return maxX > 0f && minX < dims.x && maxZ > 0f && minZ < dims.y;
    }

    // ---- player clearing (driven by PotInteraction) ----
    public void SetOutline(bool on)
    {
        if (outline == null)
        {
            if (!on) return;
            if (outlineMaterial == null)
            {
                OutlineEffect any = FindAnyObjectByType<OutlineEffect>();
                if (any != null) outlineMaterial = any.outlineMaterial;
            }
            if (outlineMaterial == null) return;
            outline = OutlineEffect.GetOrAdd(gameObject, outlineMaterial);
        }
        if (outline != null) outline.SetOutline(on);
    }

    /// <summary>Clears this clutter (deactivates it); its grids unlock if it was the last.</summary>
    public void Clear()
    {
        SetOutline(false);
        gameObject.SetActive(false);
    }
}