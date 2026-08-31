using UnityEngine;

// =============================================================
// PlantHealthTint.cs
// -------------------------------------------------------------
// Attach alongside PlantState (same GameObject, or anywhere in the
// plant's hierarchy — it finds all renderers via GetComponentsInChildren).
//
// Multiplies each renderer's ORIGINAL material color by a tint that
// blends from white (no change) at full health to unhealthyTint
// (grayish-yellow by default) at zero health:
//
//     final = originalColor * Lerp(unhealthyTint, white, health)
//
// At health == 1, the multiplier is exactly white, so the plant shows
// its normal, unmodified material color — matching "Full health has
// only the normal plant color". As health drops toward 0, more of the
// grayish-yellow tint blends in.
//
// Uses MaterialPropertyBlock rather than renderer.material, which
// would silently create a unique material instance per plant and
// break GPU instancing/batching across all your plant prefabs. The
// property block approach keeps materials shared while still letting
// each individual plant render a different color.
//
// Reads PlantState.HealthNormalized01 every frame — same value
// PlantUI's health bar already polls every frame, so this matches
// the existing pattern in the codebase rather than adding an event
// system PlantState doesn't currently have.
// =============================================================
[RequireComponent(typeof(PlantState))]
public class PlantHealthTint : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Defaults to the PlantState on this GameObject if left unassigned.")]
    [SerializeField] private PlantState plantState;

    [Header("Tint")]
    [Tooltip("Multiplicative tint applied at zero health. At full health (1.0) no tint is applied " +
             "and the plant shows its normal material color.")]
    [SerializeField] private Color unhealthyTint = new Color(0.55f, 0.5f, 0.35f); // grayish yellow

    [Tooltip("Shader color property most of this project's materials use (URP Lit / Simple Lit). " +
             "Tried first on each material; falls back to legacyColorProperty if not present.")]
    [SerializeField] private string urpColorProperty = "_BaseColor";

    [Tooltip("Fallback shader color property for materials using the legacy Standard shader instead of URP.")]
    [SerializeField] private string legacyColorProperty = "_Color";

    // ---------------------------------------------------------------
    private struct RendererEntry
    {
        public Renderer renderer;
        public Color[] originalColors;     // one per material slot
        public string[] colorProperties;   // which property name each slot actually uses ("" = unsupported shader, skipped)
    }

    private RendererEntry[] entries;
    private MaterialPropertyBlock mpb;

    // =========================================================
    private void Awake()
    {
        if (plantState == null) plantState = GetComponent<PlantState>();
        mpb = new MaterialPropertyBlock();
        CacheRenderers();
    }

    // =========================================================
    private void CacheRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        entries = new RendererEntry[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            Material[] mats = r.sharedMaterials; // sharedMaterials — reading .materials here would ALSO instance them
            Color[] originals = new Color[mats.Length];
            string[] props = new string[mats.Length];

            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null)
                {
                    originals[m] = Color.white;
                    props[m] = "";
                    continue;
                }

                if (mat.HasProperty(urpColorProperty))
                {
                    props[m] = urpColorProperty;
                    originals[m] = mat.GetColor(urpColorProperty);
                }
                else if (mat.HasProperty(legacyColorProperty))
                {
                    props[m] = legacyColorProperty;
                    originals[m] = mat.GetColor(legacyColorProperty);
                }
                else
                {
                    // Material uses neither known property (an unusual custom shader) — leave it alone
                    // rather than guessing at a property name that might not exist on it.
                    originals[m] = Color.white;
                    props[m] = "";
                }
            }

            entries[i] = new RendererEntry { renderer = r, originalColors = originals, colorProperties = props };
        }
    }

    // =========================================================
    private void Update()
    {
        if (plantState == null || entries == null) return;

        float health = plantState.HealthNormalized01; // 0 (dead) .. 1 (full health)
        Color tintMultiplier = Color.Lerp(unhealthyTint, Color.white, health);

        foreach (RendererEntry entry in entries)
        {
            if (entry.renderer == null) continue;

            for (int m = 0; m < entry.colorProperties.Length; m++)
            {
                if (string.IsNullOrEmpty(entry.colorProperties[m])) continue; // unsupported shader on this slot

                mpb.Clear(); // avoid leaking a previous renderer/slot's properties into this one
                entry.renderer.GetPropertyBlock(mpb, m);
                mpb.SetColor(entry.colorProperties[m], entry.originalColors[m] * tintMultiplier);
                entry.renderer.SetPropertyBlock(mpb, m);
            }
        }
    }
}
