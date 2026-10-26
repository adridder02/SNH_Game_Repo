using UnityEngine;

// =============================================================
// FootstepSurface.cs
// -------------------------------------------------------------
// OPTIONAL marker: put it on a floor / ground object (or its parent) to say what it sounds like underfoot.
// Without it, FootstepSensor guesses from the material / object name (wood, stone, carpet, leaves, wet...).
// =============================================================
public class FootstepSurface : MonoBehaviour
{
    public SurfaceType surface = SurfaceType.Default;
}

public static class FootstepSensor
{
    /// <summary>Looks straight down from the player and works out what they're standing on.</summary>
    public static SurfaceType Detect(Vector3 position)
    {
        if (!Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            return SurfaceType.Default;

        FootstepSurface marker = hit.collider.GetComponentInParent<FootstepSurface>();
        if (marker != null) return marker.surface;

        string n = hit.collider.name;
        Renderer r = hit.collider.GetComponentInParent<Renderer>();
        if (r != null && r.sharedMaterial != null) n += " " + r.sharedMaterial.name;
        n = n.ToLowerInvariant();

        if (Has(n, "carpet", "rug", "fabric")) return SurfaceType.Carpet;
        if (Has(n, "wood", "plank", "parquet", "deck", "floorboard", "timber")) return SurfaceType.Wood;
        if (Has(n, "wet", "water", "puddle", "sand", "beach", "mud", "shore")) return SurfaceType.Wet;
        if (Has(n, "stone", "rock", "brick", "tile", "cobble", "concrete", "gravel", "path", "slab")) return SurfaceType.Stone;
        if (Has(n, "leaf", "leaves", "grass", "moss", "foliage", "dirt", "soil", "forest")) return SurfaceType.Leaves;
        return SurfaceType.Default;
    }

    private static bool Has(string s, params string[] words)
    {
        foreach (string w in words) if (s.Contains(w)) return true;
        return false;
    }
}
