// =============================================================
// TrackedTarget.cs
// -------------------------------------------------------------
// Attach to any GameObject you want a directional arrow for
// (plants, pots, NPCs, collectibles — anything).
// The DirectionalIndicator manager will pick these up automatically
// at runtime via FindObjectsByType without any manual wiring.
// =============================================================

using UnityEngine;

public class TrackedTarget : MonoBehaviour
{
    [Tooltip("Name shown on the label when the player faces this object.")]
    public string displayName = "Target";

    [Tooltip("Color of this target's arrow and label.")]
    public Color indicatorColor = Color.white;

    [Tooltip("Optional world-space offset so the arrow points to e.g. the top of a tall plant " +
             "rather than its pivot at the base.")]
    public Vector3 trackOffset = Vector3.zero;

    [Tooltip("Whether the arrow/label should be shown at Start. Leave ON for a target that should " +
             "always be tracked. Turn OFF for a target that should only be tracked some of the " +
             "time (e.g. a tutorial-only pointer at a placement grid or the water source) — this " +
             "GameObject itself stays fully active either way (it's still the real grid/water " +
             "source, not just an indicator), only its arrow/label starts hidden until " +
             "DirectionalIndicator.ActivateTarget(this) is called.")]
    public bool startTrackingActive = true;

    /// <summary>Whether this target's arrow/label should currently be shown. Distinct from the
    /// GameObject's own active state — toggling this never disables the object itself, only its
    /// indicator. Set via DirectionalIndicator.ActivateTarget/DeactivateTarget.</summary>
    public bool IsTrackingActive { get; set; }

    private void Awake()
    {
        IsTrackingActive = startTrackingActive;
    }

    // The world-space point the indicator should aim at.
    public Vector3 WorldPosition => transform.position + trackOffset;
}