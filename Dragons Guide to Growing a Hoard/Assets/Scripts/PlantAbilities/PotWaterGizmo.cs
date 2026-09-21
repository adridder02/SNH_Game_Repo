using UnityEngine;

// =============================================================
// PotWaterGizmo.cs
// -------------------------------------------------------------
// Attached to a pot by Dewdrop's WaterIndicatorTag effect (see
// AbilityConsumableEffects.WaterIndicatorTag). Its PRESENCE on a pot is
// what PlantUI checks each frame to decide whether to show the water
// bar on that plant's overhead UI (see PlantUI.RefreshWaterBar()) — attach
// = bar visible, no component = bar hidden. There's currently no "detach"
// path (AttachTo is permanent/idempotent), so once Dewdrop's been used on
// a pot the water bar stays on for that pot from then on.
//
// The OnDrawGizmos bar below is the OLD Scene-view-only placeholder this
// was originally built as — left in as a harmless Scene-view debug aid,
// but the real UI ImageFillBar on PlantOverheadBarsView.waterBar is what
// players actually see now.
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
        if (pot == null)
        pot = GetComponent<PotContents>();

        if (pot == null) return;

        float normalized = pot.plantWaterMax > 0f ? Mathf.Clamp01(pot.WaterLevel / pot.plantWaterMax) : 0f;

        Vector3 basePos = transform.position + Vector3.up * 2f;
        float barWidth = 5f;

        // Background
        Gizmos.color = new Color(0f, 0f, 0f, 0.4f);
        Gizmos.DrawCube(basePos, new Vector3(barWidth, 0.05f, 0.01f));

        // Fill
        Gizmos.color = new Color(0.2f, 0.5f, 0.95f, 0.9f);
        Vector3 fillCentre = basePos - Vector3.right * (barWidth * 0.5f) + Vector3.right * (barWidth * normalized * 0.5f);
        Gizmos.DrawCube(fillCentre, new Vector3(barWidth * normalized, 0.06f, 0.012f));
    }
}