using UnityEngine;

// =============================================================
// PlantOverheadBarsView.cs
// -------------------------------------------------------------
// Attach this to the ROOT of a prefab, not to individual plants.
//
// Build the whole layered look by hand in the Editor, once:
//   PlantOverheadBars (this component + a World Space Canvas)
//    ├─ HealthBar   (ImageFillBar + its Background/Fill/Outline Images,
//    │               sprites + gradient set right here in the Inspector
//    │               — see ImageFillBar.cs's "SCENE SETUP" comment)
//    ├─ MiasmaBar    (same structure, its own sprites/tint)
//    └─ WaterBar     (same structure again — OPTIONAL. Only shown while
//                     PlantUI finds a PotWaterGizmo on the plant's pot,
//                     i.e. only after Dewdrop has actually been used on
//                     it. Leave this field unassigned on templates that
//                     don't want a water readout at all; PlantUI copes
//                     fine either way.)
//
// Then drag the objects below onto this component's fields.
//
// PlantUI.cs never builds or aligns any of this — at runtime it just
// Instantiate()s this prefab once per plant and calls SetNormalized()
// on healthBar/miasmaBar/waterBar. Change the art, spacing, or gradients
// by editing THIS prefab; every plant that uses it updates automatically,
// no per-plant editing required.
// =============================================================
public class PlantOverheadBarsView : MonoBehaviour
{
    [Tooltip("The World Space Canvas at the root of this prefab.")]
    public Canvas canvas;

    [Tooltip("RectTransform of that same Canvas — cached separately since PlantUI reads .rect.height every frame.")]
    public RectTransform canvasRect;

    [Tooltip("Top bar — plant health/growth. Sprites and (optionally) a colour gradient are set on this " +
             "ImageFillBar directly in the Inspector, not from code.")]
    public ImageFillBar healthBar;

    [Tooltip("Bottom bar — how much miasma is currently influencing this plant.")]
    public ImageFillBar miasmaBar;

    [Tooltip("OPTIONAL third bar — the plant's water level, sourced from PotContents.WaterLevel. " +
             "PlantUI only shows this while the plant's pot has a PotWaterGizmo attached (i.e. Dewdrop " +
             "has been used on it) and hides it entirely otherwise. Leave unassigned if a given template " +
             "doesn't want a water readout — PlantUI checks for null before touching it.")]
    public ImageFillBar waterBar;
}