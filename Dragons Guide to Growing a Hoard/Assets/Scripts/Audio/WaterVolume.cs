using System.Collections.Generic;
using UnityEngine;

// =============================================================
// WaterVolume.cs
// -------------------------------------------------------------
// Put this on each water body's collider (a Box/Sphere/Capsule collider that fills the water from the floor to
// the surface; Is Trigger on so nothing bumps into it). Used for:
//   - the swim / splash sounds (PlayerWaterAudio)
//   - the "camera is underwater" panel (MainUIPopups) when you don't assign water volumes there by hand
// =============================================================
[RequireComponent(typeof(Collider))]
public class WaterVolume : MonoBehaviour
{
    private static readonly List<WaterVolume> all = new List<WaterVolume>();
    public static int Count => all.Count;

    private Collider col;

    void OnEnable() { col = GetComponent<Collider>(); all.Add(this); }
    void OnDisable() { all.Remove(this); }

    /// <summary>True if the world position is inside any water volume.</summary>
    public static bool Contains(Vector3 p)
    {
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null && all[i].Inside(p)) return true;
        return false;
    }

    private bool Inside(Vector3 p)
    {
        if (col == null || !col.enabled) return false;
        if (col is MeshCollider mc && !mc.convex) return col.bounds.Contains(p);
        return (col.ClosestPoint(p) - p).sqrMagnitude < 0.0001f;
    }
}
