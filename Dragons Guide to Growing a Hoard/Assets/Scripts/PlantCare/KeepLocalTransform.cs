using UnityEngine;

// =============================================================
// KeepLocalTransform.cs
// -------------------------------------------------------------
// Put this on an animated model that sits inside a prefab (e.g. SundialDead under Sundial Cactus).
//
// Some animation clips exported from Blender have the model's own position / rotation / scale baked
// into them, so the Animator moves, tips over and resizes the model every frame. This forces the
// transform back to the values typed below AFTER the Animator has run each frame, so only the real
// animation (the dying) shows.
//
// SETUP: add to the animated child, and type the values you want it to sit at (defaults are
// position 0, rotation 0, scale 100 - the usual for a model imported at 0.01 scale).
// Edit these in the prefab with Play mode OFF. In Play mode the Animator overrides the Transform.
// =============================================================
[DefaultExecutionOrder(10000)]
public class KeepLocalTransform : MonoBehaviour
{
    [Tooltip("Local position the model is held at (relative to its parent).")]
    public Vector3 localPosition = Vector3.zero;

    [Tooltip("Local rotation (degrees) the model is held at.")]
    public Vector3 localEuler = Vector3.zero;

    [Tooltip("Local scale the model is held at.")]
    public Vector3 localScale = new Vector3(100f, 100f, 100f);

    private void OnEnable() => Apply();
    private void LateUpdate() => Apply();

    private void Apply()
    {
        transform.localPosition = localPosition;
        transform.localRotation = Quaternion.Euler(localEuler);
        transform.localScale = localScale;
    }

    /// <summary>Edit-mode helper: right-click the component header and choose this to copy the model's
    /// CURRENT transform into the fields above.</summary>
    [ContextMenu("Copy Current Transform Into Fields")]
    private void CopyCurrent()
    {
        localPosition = transform.localPosition;
        localEuler = transform.localEulerAngles;
        localScale = transform.localScale;
    }
}