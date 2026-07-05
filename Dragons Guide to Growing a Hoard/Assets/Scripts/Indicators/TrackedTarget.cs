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

    // The world-space point the indicator should aim at.
    public Vector3 WorldPosition => transform.position + trackOffset;
}
