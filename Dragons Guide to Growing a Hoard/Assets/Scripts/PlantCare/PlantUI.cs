using UnityEngine;
using UnityEngine.UI;

// =============================================================
// PlantUI.cs
// -------------------------------------------------------------
// Attach to the same GameObject as PlantState.cs
//
// WHAT IT DOES:
//   • Creates a small world-space UI above the plant at runtime
//   • Shows plant health as 5 colour-coded pips:
//       Dead         = 1 pip (red)
//       Intermediate = 3 pips (yellow)
//       Revived      = 5 pips (green)
//   • No background panel — canvas fits snugly around the pips
//   • Always faces the camera
//   • UI size stays consistent regardless of plant/pot scale
//   • Bottom of canvas hovers above top of plant collider/renderers
// =============================================================

public class PlantUI : MonoBehaviour
{
    [Header("Dummy Values")]
    public bool useDummyValues = false;
    // 0 = Dead, 1 = Intermediate, 2 = Revived
    [Range(0, 2)] public int dummyState = 1;

    [Header("Layout")]
    [Tooltip("When enabled, ignores renderer bounds and places the UI at a fixed height above the plant's pivot.")]
    public bool useManualHeight = false;
    [Tooltip("Height above the plant's pivot point when useManualHeight is true.")]
    public float manualHeightOffset = 1.0f;

    [Tooltip("Gap between plant top and bottom of UI canvas.")]
    public float hoverGap = 0.14f;
    public float targetScreenHeight = 0.10f;

    [Tooltip("Size of each pip in canvas units.")]
    public float pipSize = 22f;
    [Tooltip("Gap between pips in canvas units.")]
    public float pipGap = 7f;
    [Tooltip("Padding around the pip row in canvas units.")]
    public float pipPadding = 1f;

    [Header("References")]
    public PlantState plantState;

    // ── private ──────────────────────────────────────────────
    private Canvas canvasRef;
    private RectTransform canvasRect;
    private Image[] healthPips;

    private static readonly Color ColRevived      = new Color(0.33f, 0.85f, 0.45f);
    private static readonly Color ColIntermediate = new Color(0.95f, 0.78f, 0.20f);
    private static readonly Color ColDead         = new Color(0.85f, 0.28f, 0.22f);
    private static readonly Color ColPipEmpty     = new Color(1f, 1f, 1f, 0.18f);

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
            bool finalVisible = shouldShowUI && isVisible;
            canvasRef.gameObject.SetActive(finalVisible);

            if (finalVisible)
            {
                RefreshValues();
                UpdateCanvasScale();
                UpdateUIPosition();
                BillboardCanvas();
            }
        }
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
            canvasRef.gameObject.SetActive(ShouldShowUI() && isVisible);
    }

    // =========================================================
    private void BuildUI()
    {
        // ── Canvas — sized to fit pips + padding, no background ──
        // Total canvas width  = 5 pips + 4 gaps + left/right padding
        // Total canvas height = 1 pip  + top/bottom padding
        float canvasW = 5 * pipSize + 4 * pipGap + 2 * pipPadding;
        float canvasH = pipSize + 2 * pipPadding;

        GameObject canvasGO = new GameObject("PlantUI_Canvas");
        canvasGO.transform.SetParent(null);
        canvasGO.transform.position   = transform.position;
        canvasGO.transform.rotation   = Quaternion.identity;
        canvasGO.transform.localScale = Vector3.one;

        canvasRef = canvasGO.AddComponent<Canvas>();
        canvasRef.renderMode = RenderMode.WorldSpace;

        canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(canvasW, canvasH);

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Pips — positioned with absolute offsets from canvas centre ──
        // Each pip is a square pipSize × pipSize canvas units.
        // They sit in a centred row; no panel Image behind them.
        healthPips = new Image[5];

        float half = pipSize * 0.5f;

        // X centre of the leftmost pip, relative to canvas centre
        float firstCentreX = -(2f * (pipSize + pipGap));   // symmetric around 0

        for (int i = 0; i < 5; i++)
        {
            float cx = firstCentreX + i * (pipSize + pipGap);

            GameObject pipGO = CreateUIObject("HealthPip_" + i, canvasGO.transform);
            RectTransform rt = pipGO.GetComponent<RectTransform>();

            // Anchor + pivot both at canvas centre; offsets define the rect
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(cx - half, -half);
            rt.offsetMax = new Vector2(cx + half,  half);

            healthPips[i] = pipGO.AddComponent<Image>();
            healthPips[i].color = ColPipEmpty;
        }
    }

    // =========================================================
    private void RefreshValues()
    {
        if (healthPips == null) return;

        PlantStateEnum state;

        if (useDummyValues)
        {
            state = dummyState == 2 ? PlantStateEnum.Revived
                  : dummyState == 1 ? PlantStateEnum.Intermediate
                  :                   PlantStateEnum.Dead;
        }
        else
        {
            state = plantState != null ? plantState.CurrentState : PlantStateEnum.Dead;
        }

        int filledCount;
        Color pipColour;

        switch (state)
        {
            case PlantStateEnum.Revived:
                filledCount = 5;
                pipColour   = ColRevived;
                break;
            case PlantStateEnum.Intermediate:
                filledCount = 3;
                pipColour   = ColIntermediate;
                break;
            default: // Dead
                filledCount = 1;
                pipColour   = ColDead;
                break;
        }

        for (int i = 0; i < healthPips.Length; i++)
            healthPips[i].color = i < filledCount ? pipColour : ColPipEmpty;
    }

    // =========================================================
    // UI helpers
    // =========================================================

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
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
                canvasRef.transform.localScale.y / 100f;

            centerY = highestPoint + hoverGap + (canvasWorldHeight * 0.5f);
        }

        Vector3 pos = transform.position;
        pos.y = centerY;
        canvasRef.transform.position = pos;
    }

    private void UpdateCanvasScale()
    {
        if (canvasRef == null || Camera.main == null) return;

        float distance = Vector3.Distance(
            Camera.main.transform.position,
            canvasRef.transform.position);

        float fovScale =
            2f * distance *
            Mathf.Tan(Camera.main.fieldOfView * 0.5f * Mathf.Deg2Rad);

        canvasRef.transform.localScale =
            Vector3.one * fovScale * targetScreenHeight * 0.01f;
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
        if (canvasRef != null)
            Destroy(canvasRef.gameObject);
    }
}