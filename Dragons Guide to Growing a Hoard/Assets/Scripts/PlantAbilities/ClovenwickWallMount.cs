using UnityEngine;

// =============================================================
// ClovenwickWallMount.cs
// -------------------------------------------------------------
// STUB — Clovenwick has "no abilities per se", just needs a wall
// placement grid distinct from the horizontal pot grid (GreenhouseSurface
// / GridData / GridVisual, which all assume a flat XZ plane — see
// GridVisual.WorldToCell/BuildCellQuads, both hard-coded to the XZ
// axes). Doing this properly means a WallSurface counterpart to
// GreenhouseSurface that works in the wall's own local XY plane, plus
// a GridVisual variant (or a generalisation of the existing one) that
// draws/raycasts against that plane instead. That's a big enough
// chunk of work to warrant its own pass rather than folding it into
// this one — flagging it explicitly rather than half-building it.
//
// For NOW: this just marks a valid wall mount point with a gizmo and
// a weight class, so mushroom prefabs have somewhere to visually sit
// and level designers can start placing them by hand while the real
// wall-grid system gets built.
// =============================================================
public class ClovenwickWallMount : MonoBehaviour
{
    public enum WeightClass { Small, Medium } // "average basset hound" vs "puppy St. Bernard"

    public WeightClass maxWeight = WeightClass.Small;
    public GameObject mountedMushroomPrefab;

    private void OnDrawGizmos()
    {
        Gizmos.color = maxWeight == WeightClass.Medium
            ? new Color(0.6f, 0.4f, 0.2f, 0.8f)
            : new Color(0.4f, 0.6f, 0.3f, 0.8f);

        Gizmos.DrawWireCube(transform.position, Vector3.one * (maxWeight == WeightClass.Medium ? 0.6f : 0.35f));
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.3f); // mount-facing direction
    }
}
