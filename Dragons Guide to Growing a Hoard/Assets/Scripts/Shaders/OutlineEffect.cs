// =============================================================
// OutlineEffect.cs
// -------------------------------------------------------------
// Attach to the ROOT of anything you want to be outline-able
// (e.g. each plant prefab's root, next to PlantState).
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
    [Header("Outline Appearance")]
    public Color outlineColor = new Color(1f, 0.85f, 0.2f, 1f);
    [Range(0f, 0.1f)] public float outlineWidth = 0.02f;

    [Tooltip("Leave empty to auto-find Custom/InvertedHullOutline.")]
    public Shader outlineShader;

    // ---------------------------------------------------------------
    private Renderer[] renderers;
    private Material outlineMaterialInstance;
    private bool isOutlined = false;

    public bool IsOutlined => isOutlined;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();

        if (outlineShader == null)
            outlineShader = Shader.Find("Custom/InvertedHullOutline");

        if (outlineShader == null)
        {
            Debug.LogWarning($"[OutlineEffect] Could not find Custom/InvertedHullOutline shader on {name}.");
            return;
        }

        outlineMaterialInstance = new Material(outlineShader) { name = "OutlineMaterial_Instance" };
        outlineMaterialInstance.SetColor("_OutlineColor", outlineColor);
        outlineMaterialInstance.SetFloat("_OutlineWidth", outlineWidth);
    }

    // ---------------------------------------------------------------
    // SetOutline — adds/removes the outline material from every
    // renderer under this object. Idempotent: calling it with the
    // same value twice is a no-op.
    // ---------------------------------------------------------------
    public void SetOutline(bool enable)
    {
        if (outlineMaterialInstance == null) return;
        if (enable == isOutlined) return;
        isOutlined = enable;

        foreach (Renderer rend in renderers)
        {
            if (rend == null) continue;

            List<Material> mats = new List<Material>(rend.sharedMaterials);

            if (enable)
            {
                if (!mats.Contains(outlineMaterialInstance))
                    mats.Add(outlineMaterialInstance);
            }
            else
            {
                mats.Remove(outlineMaterialInstance);
            }

            rend.materials = mats.ToArray();
        }
    }

    // Lets a manager nudge color/width at runtime (e.g. pulse on hover)
    // without allocating a new material.
    public void SetAppearance(Color color, float width)
    {
        outlineColor = color;
        outlineWidth = width;

        if (outlineMaterialInstance != null)
        {
            outlineMaterialInstance.SetColor("_OutlineColor", color);
            outlineMaterialInstance.SetFloat("_OutlineWidth", width);
        }
    }

    private void OnDestroy()
    {
        if (outlineMaterialInstance != null)
            Destroy(outlineMaterialInstance);
    }
}
