// =============================================================
// PlantUI.cs
// -------------------------------------------------------------
// Attach this to your Plant GameObject alongside PlantState.cs.
//
// HOW IT WORKS:
//   - Creates a World Space Canvas above the plant at runtime.
//   - Displays three stat bars: Sunlight, Soil, and Water.
//   - Each stat is scored 1–5 and shown as filled pip icons.
//   - The panel also shows the current plant state label
//     (Revived / Intermediate / Dead) with a colour indicator.
//   - The UI always faces the main camera (billboard behaviour).
//
// SETUP:
//   1. Attach PlantUI.cs to the same GameObject as PlantState.cs.
//   2. Assign the three references in the Inspector if auto-find
//      doesn't pick them up.
//   3. Adjust heightOffset to move the panel up/down above the plant.
//   4. The canvas is created entirely in code — no prefab needed.
//
// DUMMY VALUES:
//   dummySunlight, dummySoil, dummyWater in the Inspector let you
//   test the UI without needing the full game systems running.
//   Set useDummyValues = false to connect to live data.
// =============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro; // Requires TextMeshPro package (included in Unity 2019.1+)

public class PlantUI : MonoBehaviour
{
    // ---------------------------------------------------------------
    // INSPECTOR — Dummy values for testing (1–5 scale)
    // ---------------------------------------------------------------
    [Header("Dummy Values (for testing)")]
    [Tooltip("Use dummy values instead of live game data.")]
    public bool useDummyValues = true;

    [Range(1, 5)] public int dummySunlight = 3;
    [Range(1, 5)] public int dummySoil     = 4;
    [Range(1, 5)] public int dummyWater    = 2;

    // ---------------------------------------------------------------
    // INSPECTOR — Layout
    // ---------------------------------------------------------------
    [Header("Layout")]
    [Tooltip("How far above the plant's pivot the UI panel floats.")]
    public float heightOffset = 2.2f;

    [Tooltip("Scale of the world-space canvas. Shrink if the UI appears too large.")]
    public float canvasScale = 0.008f;

    // ---------------------------------------------------------------
    // INSPECTOR — References (auto-found if left empty)
    // ---------------------------------------------------------------
    [Header("References")]
    public PlantState    plantState;
    public LightSensor   lightSensor;
    public PlayerWatering playerWatering;

    // ---------------------------------------------------------------
    // Private — runtime UI references
    // ---------------------------------------------------------------
    private Canvas      _canvas;
    private TextMeshProUGUI _stateLabel;

    // Pip arrays — 5 images per row, filled/unfilled to show score
    private Image[] _sunPips;
    private Image[] _soilPips;
    private Image[] _waterPips;

    // Colours used across the UI
    private static readonly Color ColRevived      = new Color(0.33f, 0.85f, 0.45f); // Green
    private static readonly Color ColIntermediate = new Color(0.95f, 0.78f, 0.20f); // Amber
    private static readonly Color ColDead         = new Color(0.85f, 0.28f, 0.22f); // Red
    private static readonly Color ColPipFilled    = new Color(1f,    1f,    1f, 0.95f);
    private static readonly Color ColPipEmpty     = new Color(1f,    1f,    1f, 0.18f);
    private static readonly Color ColPanelBg      = new Color(0.06f, 0.08f, 0.06f, 0.88f);

    // ---------------------------------------------------------------
    // Start — auto-find references and build the UI.
    // ---------------------------------------------------------------
    private void Start()
    {
        // Auto-find components on this GameObject if not assigned.
        if (plantState    == null) plantState    = GetComponent<PlantState>();
        if (lightSensor   == null) lightSensor   = GetComponent<LightSensor>();
        if (playerWatering == null)
            Debug.LogWarning("[PlantUI] PlayerWatering not assigned — water bar will show 0.");

        BuildUI();
    }

    // ---------------------------------------------------------------
    // Update — refresh values every frame and billboard toward camera.
    // ---------------------------------------------------------------
    private void Update()
    {
        RefreshValues();
        BillboardCanvas();
    }

    // ===============================================================
    // UI CONSTRUCTION
    // All UI elements are created in code — no prefab required.
    // ===============================================================

    private void BuildUI()
    {
        // ── Canvas ────────────────────────────────────────────────
        // A World Space canvas sits in the 3D scene above the plant.
        GameObject canvasGO = new GameObject("PlantUI_Canvas");
        canvasGO.transform.SetParent(transform);
        canvasGO.transform.localPosition = Vector3.up * heightOffset;
        canvasGO.transform.localRotation = Quaternion.identity;
        canvasGO.transform.localScale    = Vector3.one * canvasScale;

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;

        // Make the canvas a fixed pixel size in world space.
        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(260, 120);

        // Add a CanvasScaler so text size is consistent.
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Panel background ──────────────────────────────────────
        GameObject panelGO = CreateUIObject("Panel", canvasGO.transform);
        RectTransform panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImage = panelGO.AddComponent<Image>();
        panelImage.color = ColPanelBg;

        // Rounded corners via sprite — if you have a rounded rect sprite,
        // assign it here. Otherwise the panel will be a plain rectangle.
        panelImage.type = Image.Type.Sliced;

        // ── State label (top) ─────────────────────────────────────
        _stateLabel = CreateLabel("StateLabel", panelGO.transform,
            new Vector2(0f, 0.72f), new Vector2(1f, 1f),
            "● REVIVED", 13, ColRevived, FontStyles.Bold);

        // ── Stat rows ─────────────────────────────────────────────
        // Three rows: Sunlight, Soil, Water — each with a label + 5 pips.
        _sunPips  = CreateStatRow(panelGO.transform, "☀  Sunlight", 0.44f);
        _soilPips = CreateStatRow(panelGO.transform, "⬡  Soil",     0.22f);
        _waterPips= CreateStatRow(panelGO.transform, "◈  Water",    0.02f);
    }

    // ---------------------------------------------------------------
    // CreateStatRow — builds one label + 5 pip row at a given
    // vertical anchor position (0 = bottom, 1 = top of panel).
    // Returns the array of pip Images so we can colour them later.
    // ---------------------------------------------------------------
    private Image[] CreateStatRow(Transform parent, string labelText, float anchorY)
    {
        float rowHeight = 0.20f;

        // Row label (left side)
        CreateLabel(labelText + "_Lbl", parent,
            new Vector2(0.02f, anchorY),
            new Vector2(0.45f, anchorY + rowHeight),
            labelText, 9, new Color(0.78f, 0.90f, 0.78f));

        // Five pip circles (right side)
        Image[] pips = new Image[5];
        float pipWidth = 0.10f;
        float pipSpacing = 0.005f;
        float startX = 0.47f;

        for (int i = 0; i < 5; i++)
        {
            float x0 = startX + i * (pipWidth + pipSpacing);
            float x1 = x0 + pipWidth;

            GameObject pipGO = CreateUIObject($"Pip_{i}", parent);
            RectTransform rt = pipGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(x0, anchorY + 0.04f);
            rt.anchorMax = new Vector2(x1, anchorY + rowHeight - 0.04f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            Image img = pipGO.AddComponent<Image>();
            img.color = ColPipEmpty; // Start empty; RefreshValues fills them.
            pips[i] = img;
        }

        return pips;
    }

    // ---------------------------------------------------------------
    // CreateLabel — helper to create a TextMeshProUGUI element.
    // ---------------------------------------------------------------
    private TextMeshProUGUI CreateLabel(
        string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        string text, int fontSize,
        Color colour, FontStyles style = FontStyles.Normal)
    {
        GameObject go = CreateUIObject(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = colour;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        return tmp;
    }

    // ---------------------------------------------------------------
    // CreateUIObject — creates a bare GameObject with a RectTransform,
    // parented and zeroed out, ready for UI components.
    // ---------------------------------------------------------------
    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    // ===============================================================
    // VALUE REFRESH
    // Called every frame — reads live or dummy values and updates UI.
    // ===============================================================

    private void RefreshValues()
    {
        int sunScore, soilScore, waterScore;

        if (useDummyValues)
        {
            // ── Dummy mode: use Inspector sliders directly ─────────
            sunScore   = dummySunlight;
            soilScore  = dummySoil;
            waterScore = dummyWater;
        }
        else
        {
            // ── Live mode: convert real values to 1–5 scale ────────

            // Light: NormalisedIntensity is 0–1 → map to 1–5
            float lightRaw = (lightSensor != null) ? lightSensor.NormalisedIntensity : 0f;
            sunScore = Mathf.RoundToInt(Mathf.Lerp(1, 5, lightRaw));

            // Soil: soil score from PlantState is 0–2 → map to 1–5
            // We read it via the public TotalScore minus other factors
            // as an approximation. For a cleaner solution, expose
            // individual scores from PlantState.
            soilScore = (plantState != null)
                ? Mathf.Clamp(plantState.TotalScore - 2, 1, 5)
                : 1;

            // Water: CurrentWaterLevel is 0–10 → map to 1–5
            float waterRaw = (playerWatering != null) ? playerWatering.CurrentWaterLevel : 0f;
            waterScore = Mathf.RoundToInt(Mathf.Lerp(1, 5, waterRaw / 10f));
            waterScore = Mathf.Max(waterScore, 1); // Minimum 1
        }

        // Update pip visuals for each stat.
        UpdatePips(_sunPips,   sunScore);
        UpdatePips(_soilPips,  soilScore);
        UpdatePips(_waterPips, waterScore);

        // Update state label text and colour.
        if (_stateLabel != null && plantState != null)
        {
            switch (plantState.CurrentState)
            {
                case PlantStateEnum.Revived:
                    _stateLabel.text  = "● REVIVED";
                    _stateLabel.color = ColRevived;
                    break;
                case PlantStateEnum.Intermediate:
                    _stateLabel.text  = "● INTERMEDIATE";
                    _stateLabel.color = ColIntermediate;
                    break;
                case PlantStateEnum.Dead:
                    _stateLabel.text  = "● DEAD";
                    _stateLabel.color = ColDead;
                    break;
            }
        }
        else if (_stateLabel != null && useDummyValues)
        {
            // Dummy state: derive from average score.
            int avg = (sunScore + soilScore + waterScore) / 3;
            if      (avg >= 4) { _stateLabel.text = "● REVIVED";      _stateLabel.color = ColRevived; }
            else if (avg >= 2) { _stateLabel.text = "● INTERMEDIATE"; _stateLabel.color = ColIntermediate; }
            else               { _stateLabel.text = "● DEAD";         _stateLabel.color = ColDead; }
        }
    }

    // ---------------------------------------------------------------
    // UpdatePips — fills the first `score` pips and empties the rest.
    // Score is clamped to the 1–5 range.
    // ---------------------------------------------------------------
    private void UpdatePips(Image[] pips, int score)
    {
        score = Mathf.Clamp(score, 1, 5);
        for (int i = 0; i < pips.Length; i++)
        {
            pips[i].color = (i < score) ? ColPipFilled : ColPipEmpty;
        }
    }

    // ---------------------------------------------------------------
    // BillboardCanvas — rotates the canvas to always face the camera.
    // This keeps the UI readable regardless of where the player stands.
    // ---------------------------------------------------------------
    private void BillboardCanvas()
    {
        if (_canvas == null || Camera.main == null) return;

        // Point the canvas forward toward the camera.
        _canvas.transform.LookAt(
            _canvas.transform.position + Camera.main.transform.rotation * Vector3.forward,
            Camera.main.transform.rotation * Vector3.up
        );
    }
}
