// =============================================================
// SoilType.cs
// -------------------------------------------------------------
// Attach this script to any plane (or collider) that represents
// a patch of soil in your scene.
//
// HOW IT WORKS:
//   1. Create a plane GameObject in Unity.
//   2. Add a Collider component and tick "Is Trigger".
//   3. Attach this script and choose the SoilKind in the Inspector.
//   4. Each plant reads the soil type when it enters/exits the trigger.
//
// SOIL SCORE CONTRIBUTION (read by PlantState.cs):
//   Each plant species declares which soil it prefers and which is neutral.
//   The soil score is then calculated as:
//     Preferred soil → +2 points  (ideal match, plant loves it)
//     Neutral soil   → +1 point   (tolerable, plant can survive)
//     Wrong soil     → +0 points  (bad match, no soil contribution)
//
//   This score is added to the light and water scores to get a
//   total health score that drives the plant state.
// =============================================================

using UnityEngine;

// ------------------------------------------------------------------
// SoilKind — the three soil varieties available in the game.
// ------------------------------------------------------------------
public enum SoilKind
{
    Clay,   // Heavy, moisture-retaining soil
    Loam,   // Rich, balanced soil — the most fertile
    Sandy,  // Light, fast-draining soil
    Water   // Hydroponic / aquatic growing medium — constantly saturated
}

// ------------------------------------------------------------------
// SoilPatch — placed on each soil plane in the scene.
// PlantState.cs detects this via OnTrigger to know what soil
// type the plant is currently standing on.
// ------------------------------------------------------------------
public class SoilPatch : MonoBehaviour
{
    // ---------------------------------------------------------------
    // Inspector — choose the soil type for this patch here.
    // ---------------------------------------------------------------
    [Header("Soil Configuration")]
    [Tooltip("The type of soil this patch represents.")]
    public SoilKind soilKind = SoilKind.Loam;

    // ---------------------------------------------------------------
    // Optional: automatically tint the plane so soil types are
    // visually distinguishable during development and gameplay.
    // Disable this once you have proper soil materials/textures.
    // ---------------------------------------------------------------
    [Header("Debug Visuals (optional)")]
    [Tooltip("Tint the plane's material to match the soil type.")]
    public bool applyDebugColour = true;

    private void Start()
    {
        if (!applyDebugColour) return;

        Renderer rend = GetComponent<Renderer>();
        if (rend == null) return;

        // Apply a distinctive colour per soil type.
        switch (soilKind)
        {
            case SoilKind.Clay:
                rend.material.color = new Color(0.65f, 0.33f, 0.18f); // Reddish-brown
                break;
            case SoilKind.Loam:
                rend.material.color = new Color(0.35f, 0.22f, 0.10f); // Dark earth
                break;
            case SoilKind.Sandy:
                rend.material.color = new Color(0.85f, 0.78f, 0.55f); // Pale tan
                break;
            case SoilKind.Water:
                rend.material.color = new Color(0.18f, 0.52f, 0.85f); // Clear blue
                break;
        }
    }
}