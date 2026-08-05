using UnityEngine;

// =============================================================
// PotWaterGizmo.cs
// -------------------------------------------------------------
// Attached to a pot by Dewdrop's WaterIndicatorTag effect. For now
// this is a Scene-view (and always-on, since it draws every frame
// regardless of selection) gizmo bar above the plant's existing
// health/miasma bars — a real third ImageFillBar belongs on
// PlantOverheadBarsView/PlantUI once that prefab exists here; wiring
// it in is a couple of fields + one more SetNormalized() call once
// you're ready to swap this out, following the same pattern
// PlantUI.RefreshValues() already uses for healthBar/miasmaBar.
//
// Idempotent — attaching Dewdrop to a pot that already has one just
// keeps the existing indicator rather than stacking duplicates.
// =============================================================
[RequireComponent(typeof(PotContents))]
public class PotWaterGizmo : MonoBehaviour
{
    private PotContents pot;

    public static void AttachTo(PotContents pot)
    {
        if (pot == null) return;
        if (pot.GetComponent<PotWaterGizmo>() != null) return; // already tagged

        PotWaterGizmo gizmoComp = pot.gameObject.AddComponent<PotWaterGizmo>();
        gizmoComp.pot = pot;
    }

    private void Awake()
    {
        if (pot == null) pot = GetComponent<PotContents>();
    }

    private void OnDrawGizmos()
    {
        if (pot == null) return;

        float normalized = pot.plantWaterMax > 0f ? Mathf.Clamp01(pot.WaterLevel / pot.plantWaterMax) : 0f;

        Vector3 basePos = transform.position + Vector3.up * 1.5f;
        float barWidth = 0.5f;

        // Background
        Gizmos.color = new Color(0f, 0f, 0f, 0.4f);
        Gizmos.DrawCube(basePos, new Vector3(barWidth, 0.05f, 0.01f));

        // Fill
        Gizmos.color = new Color(0.2f, 0.5f, 0.95f, 0.9f);
        Vector3 fillCentre = basePos - Vector3.right * (barWidth * 0.5f) + Vector3.right * (barWidth * normalized * 0.5f);
        Gizmos.DrawCube(fillCentre, new Vector3(barWidth * normalized, 0.06f, 0.012f));
    }
}
