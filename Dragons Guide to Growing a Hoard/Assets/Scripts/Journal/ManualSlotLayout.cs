using UnityEngine;

// =============================================================
// ManualSlotLayout.cs
// -------------------------------------------------------------
// Shared math for the "read an existing template slot for its size
// and position, then wrap the rest into a grid from there" pattern —
// the same manual-positioning approach InventoryUIController uses
// for its grid (see its class comment), factored out here since
// JournalUIController and PotMenuUIController's Choose Plant panel
// both need it too.
//
// ASSUMPTIONS (same as the inventory grid):
//   • The template's RectTransform anchor/pivot is top-left (0,1),
//     and every row/container this is used in shares that convention.
//   • Callers spawn each slot via Instantiate(template, parent) —
//     that already clones the template's sizeDelta/anchors/pivot, so
//     this helper only needs to hand back the anchoredPosition.
// =============================================================
public static class ManualSlotLayout
{
    /// <summary>
    /// Anchored position for slot #index in a wrapping grid that starts at the
    /// template's own position and uses the template's own size as the cell size —
    /// i.e. move/resize the template once in the Editor and every generated slot
    /// follows, no separate "cell size" field to keep in sync by hand.
    /// </summary>
    public static Vector2 GetPosition(RectTransform template, int index, int columns, float gapX, float gapY)
    {
        if (template == null || columns <= 0) return Vector2.zero;

        int col = index % columns;
        int row = index / columns;

        float cellW = template.sizeDelta.x + gapX;
        float cellH = template.sizeDelta.y + gapY;

        return template.anchoredPosition + new Vector2(col * cellW, -row * cellH);
    }
}
