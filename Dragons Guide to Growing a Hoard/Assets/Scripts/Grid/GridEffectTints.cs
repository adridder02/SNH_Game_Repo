using UnityEngine;

// =============================================================
// GridEffectTints.cs
// -------------------------------------------------------------
// Small helper for colouring grid cells to show what an ability is affecting:
//   blue   = a pot a Waterbell is watering          pink   = a Sparkmint leaf's cell
//   purple = a pot a Melodybloom is draining
// The tints live on each surface's GridVisual, so they show whenever the grid is shown (placement mode).
// Every call is keyed by a "source" (the ability itself) so it can later remove only its own colour.
// =============================================================
public static class GridEffectTints
{
    public static readonly Color Water = new Color(0.25f, 0.55f, 1.00f, 0.55f);
    public static readonly Color Sparkmint = new Color(1.00f, 0.45f, 0.75f, 0.60f);
    public static readonly Color Melodybloom = new Color(0.62f, 0.28f, 0.92f, 0.60f);

    private static GreenhouseSurface[] cache;

    private static GreenhouseSurface[] Surfaces()
    {
        bool stale = cache == null || cache.Length == 0;
        if (!stale) foreach (GreenhouseSurface s in cache) if (s == null) { stale = true; break; }
        if (stale) cache = Object.FindObjectsByType<GreenhouseSurface>(FindObjectsInactive.Include);
        return cache;
    }

    /// <summary>The surface whose grid the world position sits on (nearest in height if several overlap).</summary>
    public static GreenhouseSurface SurfaceAt(Vector3 world)
    {
        GreenhouseSurface best = null;
        float bestDy = float.MaxValue;
        foreach (GreenhouseSurface s in Surfaces())
        {
            if (s == null || s.GridVisual == null) continue;
            Vector2 c = s.WorldToGridCells(world);
            if (c.x < -0.5f || c.y < -0.5f || c.x > s.GridDimensions.x + 0.5f || c.y > s.GridDimensions.y + 0.5f) continue;
            float dy = Mathf.Abs(world.y - s.GridOriginWorld.y);
            if (dy < bestDy) { bestDy = dy; best = s; }
        }
        return best;
    }

    public static void TintCells(object source, GreenhouseSurface surface, Vector3Int origin, Vector2Int size, Color color)
    {
        if (surface == null || surface.GridVisual == null) return;
        surface.GridVisual.AddTint(source, new Vector2Int(origin.x, origin.z), size, color);
    }

    /// <summary>Tints the grid cells a placed pot covers. Pass the surface if you know it, otherwise it's found by position.</summary>
    public static void TintPot(object source, PotContents pot, Color color, GreenhouseSurface surface = null)
    {
        if (pot == null) return;
        if (surface == null) surface = SurfaceAt(pot.transform.position);
        if (surface == null) return;

        PlacementData pd = pot.GridData != null ? pot.GridData.GetPlacement(pot.GridOrigin) : null;
        Vector3Int origin = pd != null ? pd.Origin : pot.GridOrigin;
        Vector2Int size = pd != null ? pd.Size : Vector2Int.one;
        TintCells(source, surface, origin, size, color);
    }

    /// <summary>Removes everything this source tinted, on every surface.</summary>
    public static void Clear(object source)
    {
        foreach (GreenhouseSurface s in Surfaces())
            if (s != null && s.GridVisual != null) s.GridVisual.RemoveTints(source);
    }
}
