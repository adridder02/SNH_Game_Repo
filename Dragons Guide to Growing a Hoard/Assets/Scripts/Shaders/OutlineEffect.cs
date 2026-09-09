// =============================================================
// OutlineEffect.cs
// -------------------------------------------------------------
// Attach to the ROOT of anything you want to be outline-able
// (e.g. each plant prefab's root, next to PlantState).
//
// SETUP:
//   Assign `outlineMaterial` in the Inspector to your custom
//   outline Material asset. It's fine if that material is
//   currently sitting in a renderer's material slot already —
//   Awake() strips it out of the baseline so it starts OFF.
//
// USAGE:
//   OutlineEffect fx = plant.GetComponent<OutlineEffect>();
//   fx.SetOutline(true);   // turn the rim on
//   fx.SetOutline(false);  // turn it off
//
// This is intentionally just a renderer-level toggle with no
// dependency on input, menus, or pots — it doesn't care WHO is
// telling it to highlight, only whether it should be on or off.
// That makes it safe to drive from a singleton OutlineManager:
// OutlineManager just needs to call SetOutline(true) on the new
// target and SetOutline(false) on the previous one, independent
// of whatever PotInteraction's menu state is doing.
// =============================================================

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class OutlineEffect : MonoBehaviour
{
    [Header("Outline Material")]
    [Tooltip("Your pre-made outline Material asset (your custom shader). " +
             "It's OK if it's already assigned in a renderer's slot — " +
             "Awake() will strip it out so the object starts un-outlined.")]
    public Material outlineMaterial;

    // ---------------------------------------------------------------
    private Renderer[] renderers;
    // Per-renderer material list with outlineMaterial guaranteed absent.
    private Material[][] baseMaterials;
    private bool isOutlined = false;

    public bool IsOutlined => isOutlined;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        baseMaterials = new Material[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rend = renderers[i];
            if (rend == null) continue;

            List<Material> mats = new List<Material>(rend.sharedMaterials);
            if (outlineMaterial != null)
                mats.RemoveAll(m => m == outlineMaterial);

            baseMaterials[i] = mats.ToArray();
        }

        if (outlineMaterial == null)
            Debug.LogWarning($"[OutlineEffect] No outlineMaterial assigned on {name}.");
    }

    // ---------------------------------------------------------------
    // SetOutline — adds/removes outlineMaterial from every renderer
    // under this object. Idempotent: calling it with the same value
    // twice is a no-op.
    // ---------------------------------------------------------------
    public void SetOutline(bool enable)
    {
        if (outlineMaterial == null) return;
        if (enable == isOutlined) return;
        isOutlined = enable;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rend = renderers[i];
            if (rend == null) continue;

            if (!enable)
            {
                rend.materials = baseMaterials[i];
                continue;
            }

            List<Material> mats = new List<Material>(baseMaterials[i]);
            mats.Add(outlineMaterial);
            rend.materials = mats.ToArray();
        }
    }
}