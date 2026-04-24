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
//   • Always faces the camera
//   • Supports dummy values for testing
//
// NOTE:
//   PlayerWatering has been fully removed.
//   Water now comes from PotContents only.
// =============================================================

public class PlantUI : MonoBehaviour
{
    // ---------------------------------------------------------
    // TESTING VALUES
    // ---------------------------------------------------------
    [Header("Dummy Values")]
    public bool useDummyValues = true;

    [Range(1, 5)] public int dummySunlight = 3;
    [Range(1, 5)] public int dummySoil = 4;
    [Range(1, 5)] public int dummyWater = 2;

    // ---------------------------------------------------------
    // LAYOUT
    // ---------------------------------------------------------
    [Header("Layout")]
    public float heightOffset = 2.2f;
    public float canvasScale = 0.008f;

    // ---------------------------------------------------------
    // REFERENCES
    // ---------------------------------------------------------
    [Header("References")]
    public PlantState plantState;
    public LightSensor lightSensor;

    // ---------------------------------------------------------
    // UI REFERENCES
    // ---------------------------------------------------------
    private Canvas canvasRef;
    private TextMeshProUGUI stateLabel;

    private Image[] sunPips;
    private Image[] soilPips;
    private Image[] waterPips;

    // ---------------------------------------------------------
    // COLOURS
    // ---------------------------------------------------------
    private static readonly Color ColRevived =
        new Color(0.33f, 0.85f, 0.45f);

    private static readonly Color ColIntermediate =
        new Color(0.95f, 0.78f, 0.20f);

    private static readonly Color ColDead =
        new Color(0.85f, 0.28f, 0.22f);

    private static readonly Color ColPipFilled =
        new Color(1f, 1f, 1f, 0.95f);

    private static readonly Color ColPipEmpty =
        new Color(1f, 1f, 1f, 0.18f);

    private static readonly Color ColPanelBg =
        new Color(0.06f, 0.08f, 0.06f, 0.88f);

    // ---------------------------------------------------------
    private void Start()
    {
        if (plantState == null)
            plantState = GetComponent<PlantState>();

        if (lightSensor == null)
            lightSensor = GetComponent<LightSensor>();

        BuildUI();
    }

    // ---------------------------------------------------------
    private void Update()
    {
        RefreshValues();
        BillboardCanvas();
    }

    // =========================================================
    // BUILD UI
    // =========================================================
    private void BuildUI()
    {
        GameObject canvasGO = new GameObject("PlantUI_Canvas");
        canvasGO.transform.SetParent(transform);
        canvasGO.transform.localPosition = Vector3.up * heightOffset;
        canvasGO.transform.localRotation = Quaternion.identity;
        canvasGO.transform.localScale = Vector3.one * canvasScale;

        canvasRef = canvasGO.AddComponent<Canvas>();
        canvasRef.renderMode = RenderMode.WorldSpace;

        RectTransform canvasRect =
            canvasGO.GetComponent<RectTransform>();

        canvasRect.sizeDelta = new Vector2(260, 120);

        CanvasScaler scaler =
            canvasGO.AddComponent<CanvasScaler>();

        scaler.dynamicPixelsPerUnit = 10f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Panel
        GameObject panelGO =
            CreateUIObject("Panel", canvasGO.transform);

        RectTransform panelRect =
            panelGO.GetComponent<RectTransform>();

        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImg = panelGO.AddComponent<Image>();
        panelImg.color = ColPanelBg;

        // State Label
        stateLabel = CreateLabel(
            "StateLabel",
            panelGO.transform,
            new Vector2(0f, 0.72f),
            new Vector2(1f, 1f),
            "● REVIVED",
            13,
            ColRevived,
            FontStyles.Bold
        );

        // Rows
        sunPips = CreateStatRow(panelGO.transform, "Sunlight", 0.44f);
        soilPips = CreateStatRow(panelGO.transform, "Soil", 0.22f);
        waterPips = CreateStatRow(panelGO.transform, "Water", 0.02f);
    }

    // ---------------------------------------------------------
    private Image[] CreateStatRow(
        Transform parent,
        string labelText,
        float anchorY)
    {
        float rowHeight = 0.20f;

        CreateLabel(
            labelText + "_Lbl",
            parent,
            new Vector2(0.02f, anchorY),
            new Vector2(0.45f, anchorY + rowHeight),
            labelText,
            9,
            new Color(0.78f, 0.90f, 0.78f)
        );

        Image[] pips = new Image[5];

        float pipWidth = 0.10f;
        float spacing = 0.005f;
        float startX = 0.47f;

        for (int i = 0; i < 5; i++)
        {
            float x0 = startX + i * (pipWidth + spacing);
            float x1 = x0 + pipWidth;

            GameObject pipGO =
                CreateUIObject("Pip_" + i, parent);

            RectTransform rt =
                pipGO.GetComponent<RectTransform>();

            rt.anchorMin =
                new Vector2(x0, anchorY + 0.04f);

            rt.anchorMax =
                new Vector2(x1, anchorY + rowHeight - 0.04f);

            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = pipGO.AddComponent<Image>();
            img.color = ColPipEmpty;

            pips[i] = img;
        }

        return pips;
    }

    // ---------------------------------------------------------
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

        RectTransform rt =
            go.GetComponent<RectTransform>();

        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp =
            go.AddComponent<TextMeshProUGUI>();

        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = colour;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;

        return tmp;
    }

    // ---------------------------------------------------------
    private GameObject CreateUIObject(
        string name,
        Transform parent)
    {
        GameObject go =
            new GameObject(name, typeof(RectTransform));

        go.transform.SetParent(parent, false);

        return go;
    }

    // =========================================================
    // REFRESH VALUES
    // =========================================================
    private void RefreshValues()
    {
        int sunScore;
        int soilScore;
        int waterScore;

        if (useDummyValues)
        {
            sunScore = dummySunlight;
            soilScore = dummySoil;
            waterScore = dummyWater;
        }
        else
        {
            // Sun
            float lightRaw =
                lightSensor != null
                ? lightSensor.NormalisedIntensity
                : 0f;

            sunScore =
                Mathf.RoundToInt(Mathf.Lerp(1, 5, lightRaw));

            // Soil
            soilScore =
                plantState != null
                ? Mathf.Clamp(plantState.TotalScore - 2, 1, 5)
                : 1;

            // Water
            PotContents pot =
                plantState != null
                ? plantState.GetComponentInParent<PotContents>()
                : null;

            float waterRaw =
                pot != null ? pot.WaterLevel : 0f;

            float waterMax =
                pot != null ? pot.plantWaterMax : 10f;

            waterScore =
                Mathf.RoundToInt(
                    Mathf.Lerp(1, 5, waterRaw / waterMax));

            waterScore = Mathf.Max(1, waterScore);
        }

        UpdatePips(sunPips, sunScore);
        UpdatePips(soilPips, soilScore);
        UpdatePips(waterPips, waterScore);

        UpdateStateLabel(sunScore, soilScore, waterScore);
    }

    // ---------------------------------------------------------
    private void UpdateStateLabel(
        int sun,
        int soil,
        int water)
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

                case PlantStateEnum.Dead:
                    stateLabel.text = "● DEAD";
                    stateLabel.color = ColDead;
                    return;
            }
        }

        // Dummy mode
        int avg = (sun + soil + water) / 3;

        if (avg >= 4)
        {
            stateLabel.text = "● REVIVED";
            stateLabel.color = ColRevived;
        }
        else if (avg >= 2)
        {
            stateLabel.text = "● INTERMEDIATE";
            stateLabel.color = ColIntermediate;
        }
        else
        {
            stateLabel.text = "● DEAD";
            stateLabel.color = ColDead;
        }
    }

    // ---------------------------------------------------------
    private void UpdatePips(Image[] pips, int score)
    {
        score = Mathf.Clamp(score, 1, 5);

        for (int i = 0; i < pips.Length; i++)
        {
            pips[i].color =
                i < score ? ColPipFilled : ColPipEmpty;
        }
    }

    // ---------------------------------------------------------
    private void BillboardCanvas()
    {
        if (canvasRef == null || Camera.main == null)
            return;

        canvasRef.transform.LookAt(
            canvasRef.transform.position +
            Camera.main.transform.rotation * Vector3.forward,

            Camera.main.transform.rotation *
            Vector3.up
        );
    }
}