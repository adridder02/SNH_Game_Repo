using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================
// PlantUI.cs
// -------------------------------------------------------------
// Attach to the same GameObject as PlantState.cs
//
// WHAT IT DOES:
//   • Creates a world-space UI above the plant at runtime
//   • Shows Sunlight / Soil / Water scores as 5 pips each
//   • Shows plant state (Revived / Intermediate / Dead)
//   • Includes placeholder rows:
//        - Proximity
//        - Ability
//        - Miasma
//   • Always faces the camera
//   • UI size stays consistent regardless of plant/pot scale
//   • Bottom of canvas hovers above top of plant collider/renderers
// =============================================================

public class PlantUI : MonoBehaviour
{
    [Header("Dummy Values")]
    public bool useDummyValues = false;

    [Range(1, 5)] public int dummySunlight = 3;
    [Range(1, 5)] public int dummySoil = 4;
    [Range(1, 5)] public int dummyWater = 2;

    [Header("Layout")]
    [Tooltip("Gap between plant top and bottom of UI canvas.")]
    public float hoverGap = 0.35f;

    public float targetScreenHeight = 0.15f;
    public Vector2 baseCanvasSize = new Vector2(2.6f, 2.9f);

    [Header("References")]
    public PlantState plantState;
    public LightSensor lightSensor;

    private Canvas canvasRef;
    private RectTransform canvasRect;
    private TextMeshProUGUI stateLabel;

    private Image[] sunPips;
    private Image[] soilPips;
    private Image[] waterPips;

    private Image[] happinessPips;
    private TextMeshProUGUI abilityLabel;
    private Image[] miasmaPips;

    private static readonly Color ColRevived = new Color(0.33f, 0.85f, 0.45f);
    private static readonly Color ColIntermediate = new Color(0.95f, 0.78f, 0.20f);
    private static readonly Color ColDead = new Color(0.85f, 0.28f, 0.22f);

    private static readonly Color ColPipFilled = new Color(1f, 1f, 1f, 0.95f);
    private static readonly Color ColPipEmpty = new Color(1f, 1f, 1f, 0.18f);
    private static readonly Color ColPanelBg = new Color(0.06f, 0.08f, 0.06f, 0.88f);

    private static readonly Color ColFuturePip = new Color(0.45f, 0.50f, 0.80f, 0.30f);
    private static readonly Color ColFutureLabel = new Color(0.55f, 0.60f, 0.85f, 0.65f);
    private static readonly Color ColFutureText = new Color(0.60f, 0.65f, 0.78f, 0.55f);

    private bool uiBuilt = false;
    private bool isVisible = true;

    // =========================================================
    private void Start()
    {
        if (plantState == null)
            plantState = GetComponent<PlantState>();

        if (lightSensor == null)
            lightSensor = GetComponent<LightSensor>();
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

        if (canvasRef != null)
            canvasRef.gameObject.SetActive(ShouldShowUI() && isVisible);
    }

    // =========================================================
    private void BuildUI()
    {
        GameObject canvasGO = new GameObject("PlantUI_Canvas");

        canvasGO.transform.SetParent(null);
        canvasGO.transform.position = transform.position;
        canvasGO.transform.rotation = Quaternion.identity;
        canvasGO.transform.localScale = Vector3.one;

        canvasRef = canvasGO.AddComponent<Canvas>();
        canvasRef.renderMode = RenderMode.WorldSpace;

        canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = baseCanvasSize * 100f;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject panelGO = CreateUIObject("Panel", canvasGO.transform);

        RectTransform panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImg = panelGO.AddComponent<Image>();
        panelImg.color = ColPanelBg;

        stateLabel = CreateLabel(
            "StateLabel",
            panelGO.transform,
            new Vector2(0f, 0.88f),
            new Vector2(1f, 1f),
            "● DEAD",
            13,
            ColDead,
            FontStyles.Bold
        );

        sunPips = CreateStatRow(panelGO.transform, "Sunlight", 0.70f, false);
        soilPips = CreateStatRow(panelGO.transform, "Soil", 0.56f, false);
        waterPips = CreateStatRow(panelGO.transform, "Water", 0.42f, false);

        CreateLabel(
            "Divider",
            panelGO.transform,
            new Vector2(0.02f, 0.34f),
            new Vector2(0.98f, 0.41f),
            "─── COMING SOON ───",
            7,
            new Color(0.45f, 0.50f, 0.75f, 0.35f)
        );

        happinessPips = CreateStatRow(panelGO.transform, "Proximity", 0.22f, true);
        abilityLabel = CreateBoolRow(panelGO.transform, "Ability", 0.11f);
        miasmaPips = CreateStatRow(panelGO.transform, "Miasma", 0.02f, true);
    }

    // =========================================================
    private Image[] CreateStatRow(Transform parent, string labelText, float anchorY, bool future)
    {
        float rowHeight = 0.16f;

        CreateLabel(
            labelText + "_Lbl",
            parent,
            new Vector2(0.02f, anchorY),
            new Vector2(0.45f, anchorY + rowHeight),
            labelText,
            9,
            future ? ColFutureText : new Color(0.78f, 0.90f, 0.78f)
        );

        Image[] pips = new Image[5];

        float pipWidth = 0.10f;
        float spacing = 0.005f;
        float startX = 0.47f;

        for (int i = 0; i < 5; i++)
        {
            float x0 = startX + i * (pipWidth + spacing);
            float x1 = x0 + pipWidth;

            GameObject pipGO = CreateUIObject("Pip_" + i, parent);
            RectTransform rt = pipGO.GetComponent<RectTransform>();

            rt.anchorMin = new Vector2(x0, anchorY + 0.03f);
            rt.anchorMax = new Vector2(x1, anchorY + rowHeight - 0.03f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = pipGO.AddComponent<Image>();
            img.color = future ? ColFuturePip : ColPipEmpty;

            pips[i] = img;
        }

        return pips;
    }

    // =========================================================
    private TextMeshProUGUI CreateBoolRow(Transform parent, string labelText, float anchorY)
    {
        float rowHeight = 0.10f;

        CreateLabel(
            labelText + "_Lbl",
            parent,
            new Vector2(0.02f, anchorY),
            new Vector2(0.45f, anchorY + rowHeight),
            labelText,
            9,
            ColFutureText
        );

        return CreateLabel(
            labelText + "_Val",
            parent,
            new Vector2(0.47f, anchorY),
            new Vector2(0.98f, anchorY + rowHeight),
            "—",
            9,
            ColFutureLabel
        );
    }

    // =========================================================
    private TextMeshProUGUI CreateLabel(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        string text,
        int fontSize,
        Color colour,
        FontStyles style = FontStyles.Normal)
    {
        GameObject go = CreateUIObject(name, parent);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = colour;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;

        return tmp;
    }

    // =========================================================
    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    // =========================================================
    private void RefreshValues()
    {
        int sunScore, soilScore, waterScore;

        if (useDummyValues)
        {
            sunScore = dummySunlight;
            soilScore = dummySoil;
            waterScore = dummyWater;
        }
        else
        {
            float lightRaw = lightSensor != null ? lightSensor.NormalisedIntensity : 0f;
            sunScore = Mathf.RoundToInt(Mathf.Lerp(1f, 5f, lightRaw));

            soilScore = plantState != null
                ? plantState.SoilScore switch { 2 => 5, 1 => 3, _ => 1 }
                : 1;

            PotContents pot = plantState != null
                ? plantState.GetComponentInParent<PotContents>()
                : null;

            float waterRaw = pot != null ? pot.WaterLevel : 0f;
            float waterMax = pot != null ? pot.plantWaterMax : 10f;

            waterScore = waterMax > 0f
                ? Mathf.RoundToInt(Mathf.Lerp(1f, 5f, waterRaw / waterMax))
                : 1;
        }

        UpdatePips(sunPips, sunScore);
        UpdatePips(soilPips, soilScore);
        UpdatePips(waterPips, waterScore);

        UpdateStateLabel();
    }

    // =========================================================
    private void UpdateStateLabel()
    {
        if (stateLabel == null) return;

        if (!useDummyValues && plantState != null)
        {
            switch (plantState.CurrentState)
            {
                case PlantStateEnum.Revived:
                    stateLabel.text = "● REVIVED";
                    stateLabel.color = ColRevived;
                    return;

                case PlantStateEnum.Intermediate:
                    stateLabel.text = "● INTERMEDIATE";
                    stateLabel.color = ColIntermediate;
                    return;

                default:
                    stateLabel.text = "● DEAD";
                    stateLabel.color = ColDead;
                    return;
            }
        }

        stateLabel.text = "● DEAD";
        stateLabel.color = ColDead;
    }

    // =========================================================
    private void UpdatePips(Image[] pips, int score)
    {
        score = Mathf.Clamp(score, 1, 5);

        for (int i = 0; i < pips.Length; i++)
            pips[i].color = i < score ? ColPipFilled : ColPipEmpty;
    }

    // =========================================================
    private void UpdateUIPosition()
    {
        if (canvasRef == null || canvasRect == null) return;

        float highestPoint = transform.position.y;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            if (rend.bounds.max.y > highestPoint)
                highestPoint = rend.bounds.max.y;
        }

        // World height of current canvas after scaling
        float canvasWorldHeight =
            canvasRect.rect.height *
            canvasRef.transform.localScale.y /
            100f;

        // Move center upward so bottom edge sits above plant
        float centerY =
            highestPoint +
            hoverGap +
            (canvasWorldHeight * 0.5f);

        Vector3 pos = transform.position;
        pos.y = centerY;

        canvasRef.transform.position = pos;
    }

    // =========================================================
    private void UpdateCanvasScale()
    {
        if (canvasRef == null || Camera.main == null) return;

        float distance = Vector3.Distance(
            Camera.main.transform.position,
            canvasRef.transform.position
        );

        float fovScale =
            2f *
            distance *
            Mathf.Tan(Camera.main.fieldOfView * 0.5f * Mathf.Deg2Rad);

        float targetScale = fovScale * targetScreenHeight;

        canvasRef.transform.localScale = Vector3.one * targetScale * 0.01f;
    }

    // =========================================================
    private void BillboardCanvas()
    {
        if (canvasRef == null || Camera.main == null) return;

        Vector3 toCamera =
            Camera.main.transform.position -
            canvasRef.transform.position;

        toCamera.y = 0f;

        if (toCamera.sqrMagnitude > 0.0001f)
            canvasRef.transform.rotation = Quaternion.LookRotation(-toCamera);
    }

    // =========================================================
    private void OnDestroy()
    {
        if (canvasRef != null)
            Destroy(canvasRef.gameObject);
    }
}