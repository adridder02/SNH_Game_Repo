using UnityEngine;

// =============================================================
// PlantUI.cs
// -------------------------------------------------------------
// Attach to the same GameObject as PlantState.cs
//
// WHAT IT DOES:
//   • Instantiates ONE copy of a PlantOverheadBarsView prefab above the
//     plant at runtime — no UI is built or aligned in code here, the
//     whole layered look (backgrounds/fills/outlines/gradients for both
//     bars) lives in that prefab. See PlantOverheadBarsView.cs.
//   • Drives two bars every frame:
//       - healthBar  — PlantState.HealthNormalized01 (same score
//                       CalculateState() uses for Dead/Intermediate/Revived)
//       - miasmaBar  — PlantState.MiasmaInfluence01 ("how much miasma
//                       is currently influencing this plant")
//   • Always faces the camera
//   • Fixed world-space size (fixedWorldHeight) — bars no longer grow or
//     shrink with camera distance, they just stay the size you set
//   • Only visible while the camera is within maxVisibleDistance
//   • Bottom of canvas hovers above top of plant collider/renderers
//
// SETUP (do this ONCE, not per plant):
//   1. Build a prefab with a World Space Canvas at its root, a HealthBar
//      child and a MiasmaBar child (each with its own ImageFillBar +
//      Background/Fill/Outline Images, sprites/gradient set right there
//      in the Inspector — see ImageFillBar.cs's SCENE SETUP comment).
//   2. Add a PlantOverheadBarsView component to the prefab's root and
//      wire its four fields to the Canvas/RectTransform/two ImageFillBars.
//   3a. EASIEST — save the prefab at Resources/PlantOverheadBarsTemplate
//       (any path, just that name) and leave overheadBarsTemplate below
//       unassigned on every plant. PlantUI auto-loads it, so none of
//       your dozens of existing plant prefabs need touching.
//   3b. OR assign overheadBarsTemplate explicitly per-plant if you want
//       some plants to use a different look — an explicit assignment
//       always wins over the Resources default.
// =============================================================

public class PlantUI : MonoBehaviour
{
    [Header("Dummy Values")]
    public bool useDummyValues = false;
    // 0 = Dead, 1 = Intermediate, 2 = Revived
    [Range(0, 2)] public int dummyState = 1;
    [Range(0f, 1f)] public float dummyMiasma = 0f;

    [Header("Layout")]
    [Tooltip("When enabled, ignores renderer bounds and places the UI at a fixed height above the plant's pivot.")]
    public bool useManualHeight = false;
    [Tooltip("Height above the plant's pivot point when useManualHeight is true.")]
    public float manualHeightOffset = 1.0f;

    [Tooltip("Gap between plant top and bottom of UI canvas.")]
    public float hoverGap = 0.14f;

    [Tooltip("Fixed world-space height of the whole bar stack, in world units (e.g. metres). Set once when " +
             "the UI is built and never changed afterward — the bars stay this size regardless of camera " +
             "distance, instead of the old behaviour of resizing every frame to hold a constant screen size.")]
    public float fixedWorldHeight = 0.3f;

    [Tooltip("Bars are only shown while the camera (a stand-in for the player, since the two normally stay " +
             "close together) is within this distance, in world units. Set to a large number to effectively disable.")]
    public float maxVisibleDistance = 6f;

    [Tooltip("Logs the camera-to-plant distance every frame the UI exists, so you can read off a real number " +
             "instead of guessing at maxVisibleDistance/fixedWorldHeight. Turn off once you've picked good values.")]
    public bool logVisibilityDebug = false;

    [Header("Overhead Bars Template")]
    [Tooltip("Prefab defining the whole look of the two bars (see PlantOverheadBarsView.cs) — set this up once, " +
             "not per plant. Leave unassigned to use the shared default loaded from " +
             "Resources/PlantOverheadBarsTemplate; only fill this in if THIS plant needs a different look.")]
    [SerializeField] private PlantOverheadBarsView overheadBarsTemplate;

    [Tooltip("Resources-folder path (no extension) used to auto-load the default template when " +
             "overheadBarsTemplate is left unassigned above.")]
    [SerializeField] private string defaultTemplateResourcePath = "PlantOverheadBarsTemplate";

    [Header("References")]
    public PlantState plantState;

    // ── private ──────────────────────────────────────────────
    // Cached across all PlantUI instances so dozens of plants only pay the Resources.Load cost once.
    private static PlantOverheadBarsView cachedDefaultTemplate;
    private static bool triedLoadDefault = false;

    private PlantOverheadBarsView barsInstance;
    private Canvas canvasRef;
    private RectTransform canvasRect;

    private bool uiBuilt   = false;
    private bool isVisible = true;

    // =========================================================
    private void Start()
    {
        if (plantState == null)
            plantState = GetComponent<PlantState>();
    }

    // =========================================================
    private void Update()
    {
        bool shouldShowUI = ShouldShowUI();

        if (shouldShowUI && !uiBuilt)
        {
            BuildUI();
            uiBuilt = true;
        }

        if (uiBuilt && canvasRef != null)
        {
            bool finalVisible = shouldShowUI && isVisible && WithinVisibleDistance();
            canvasRef.gameObject.SetActive(finalVisible);

            if (finalVisible)
            {
                RefreshValues();
                UpdateUIPosition();
                BillboardCanvas();
            }
        }
    }

    // =========================================================
    private bool WithinVisibleDistance()
    {
        if (Camera.main == null) return true; // no camera to measure against — default to visible

        float dist = Vector3.Distance(Camera.main.transform.position, transform.position);
        bool within = dist <= maxVisibleDistance;

        if (logVisibilityDebug)
            Debug.Log($"[PlantUI] '{name}' camera distance: {dist:F2} (max {maxVisibleDistance}) → {(within ? "VISIBLE" : "HIDDEN")}");

        return within;
    }

    // =========================================================
    private bool ShouldShowUI()
    {
        if (useDummyValues) return true;
        if (plantState == null) return false;

        PotContents pot = plantState.GetComponentInParent<PotContents>();
        return pot != null && pot.HasSoil;
    }

    // =========================================================
    public void SetVisible(bool visible)
    {
        isVisible = visible;
        ThirdPersonCameraController.CameraLocked = visible;

        if (canvasRef != null)
            canvasRef.gameObject.SetActive(ShouldShowUI() && isVisible && WithinVisibleDistance());
    }

    // =========================================================
    private PlantOverheadBarsView ResolveTemplate()
    {
        if (overheadBarsTemplate != null) return overheadBarsTemplate;

        if (!triedLoadDefault)
        {
            triedLoadDefault = true;
            cachedDefaultTemplate = Resources.Load<PlantOverheadBarsView>(defaultTemplateResourcePath);

            if (cachedDefaultTemplate == null)
                Debug.LogWarning($"[PlantUI] No overheadBarsTemplate assigned and no default prefab found at " +
                                  $"'Resources/{defaultTemplateResourcePath}'. Either assign a template per-plant " +
                                  "or save one at that Resources path so every plant picks it up automatically.");
        }

        return cachedDefaultTemplate;
    }

    // =========================================================
    private void BuildUI()
    {
        PlantOverheadBarsView template = ResolveTemplate();
        if (template == null) return;

        barsInstance = Instantiate(template, transform.position, Quaternion.identity);
        barsInstance.transform.SetParent(null);
        barsInstance.name = "PlantUI_OverheadBars";

        canvasRef  = barsInstance.canvas;
        canvasRect = barsInstance.canvasRect;

        if (canvasRef == null || canvasRect == null || barsInstance.healthBar == null || barsInstance.miasmaBar == null)
            Debug.LogWarning($"[PlantUI] Template '{template.name}' is missing a reference (canvas/canvasRect/" +
                              "healthBar/miasmaBar) — check its PlantOverheadBarsView component in the Inspector.", template);

        // Set the world-space size ONCE, here, rather than recalculating it every frame — that's what used
        // to make the bars grow/shrink with camera distance. canvasRect.rect.height is in the prefab's own
        // canvas units; scaling it so the whole stack is fixedWorldHeight world units tall keeps it constant
        // from here on regardless of how far the camera is.
        if (canvasRef != null && canvasRect != null && canvasRect.rect.height > 0f)
        {
            float scale = fixedWorldHeight / canvasRect.rect.height;
            canvasRef.transform.localScale = Vector3.one * scale;
        }
    }

    // =========================================================
    private void RefreshValues()
    {
        if (barsInstance == null || barsInstance.healthBar == null || barsInstance.miasmaBar == null) return;

        float healthNormalized;
        float miasmaNormalized;

        if (useDummyValues)
        {
            // Roughly matches the old 1/3/5-out-of-5 pip counts, rescaled to 0–1.
            healthNormalized = dummyState == 2 ? 1f : dummyState == 1 ? 0.6f : 0.2f;
            miasmaNormalized = dummyMiasma;
        }
        else
        {
            healthNormalized = plantState != null ? plantState.HealthNormalized01 : 0f;
            miasmaNormalized = plantState != null ? plantState.MiasmaInfluence01 : 0f;
        }

        barsInstance.healthBar.SetNormalized(healthNormalized);
        barsInstance.miasmaBar.SetNormalized(miasmaNormalized);
    }

    // =========================================================
    // Positioning / scaling / billboard
    // =========================================================

    private void UpdateUIPosition()
    {
        if (canvasRef == null || canvasRect == null) return;

        float centerY;

        if (useManualHeight)
        {
            centerY = transform.position.y + manualHeightOffset;
        }
        else
        {
            float highestPoint = transform.position.y;

            foreach (Renderer rend in GetComponentsInChildren<Renderer>())
            {
                if (rend.bounds.max.y > highestPoint)
                    highestPoint = rend.bounds.max.y;
            }

            float canvasWorldHeight =
                canvasRect.rect.height *
                canvasRef.transform.localScale.y;

            centerY = highestPoint + hoverGap + (canvasWorldHeight * 0.5f);
        }

        Vector3 pos = transform.position;
        pos.y = centerY;
        canvasRef.transform.position = pos;
    }

    private void BillboardCanvas()
    {
        if (canvasRef == null || Camera.main == null) return;

        Vector3 toCamera =
            Camera.main.transform.position - canvasRef.transform.position;
        toCamera.y = 0f;

        if (toCamera.sqrMagnitude > 0.0001f)
            canvasRef.transform.rotation = Quaternion.LookRotation(-toCamera);
    }

    private void OnDestroy()
    {
        if (barsInstance != null)
            Destroy(barsInstance.gameObject);
    }
}