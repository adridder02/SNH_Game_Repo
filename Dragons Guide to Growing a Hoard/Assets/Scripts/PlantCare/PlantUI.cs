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
//   • Supports dummy values for testing (useDummyValues)
//
// PLANNED (placeholders visible in UI, logic not yet wired):
//   • Happiness  — proximity-based score from surrounding plants
//   • Ability    — boolean: plant is affected by another plant's power
//   • Miasma     — boolean: plant is under miasma debuff
//
// CHANGES (FIX):
//   • UI is now HIDDEN by default and only shows when the plant
//     has soil AND the pot is not being moved.
//   • Empty pot (no soil) → UI hidden completely
//   • No plant → UI doesn't exist
//   • All bars reflect actual game state, not dummy intermediate values
//   • SetVisible() properly hides/shows during grab/move operations
// =============================================================

public class PlantUI : MonoBehaviour
{
    // ---------------------------------------------------------
    // TESTING VALUES
    // ---------------------------------------------------------
    [Header("Dummy Values")]
    [Tooltip("Enable to drive pips with the sliders below instead of live game data. " +
             "Turn OFF for production.")]
    public bool useDummyValues = false;

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

    // Placeholder rows — not driven by live logic yet
    private Image[] happinessPips;   // proximity happiness (future)
    private TextMeshProUGUI abilityLabel;    // plant-power ability (future)
    private Image[] miasmaPips;      // miasma debuff bar (future)

    // ---------------------------------------------------------
    // COLOURS
    // ---------------------------------------------------------
    private static readonly Color ColRevived = new Color(0.33f, 0.85f, 0.45f);
    private static readonly Color ColIntermediate = new Color(0.95f, 0.78f, 0.20f);
    private static readonly Color ColDead = new Color(0.85f, 0.28f, 0.22f);
    private static readonly Color ColPipFilled = new Color(1f, 1f, 1f, 0.95f);
    private static readonly Color ColPipEmpty = new Color(1f, 1f, 1f, 0.18f);
    private static readonly Color ColPanelBg = new Color(0.06f, 0.08f, 0.06f, 0.88f);

    // Future / placeholder colours — muted so they read as "not yet active"
    private static readonly Color ColFuturePip = new Color(0.45f, 0.50f, 0.80f, 0.30f); // dim blue-purple
    private static readonly Color ColFutureLabel = new Color(0.55f, 0.60f, 0.85f, 0.65f); // same family, slightly brighter
    private static readonly Color ColFutureText = new Color(0.60f, 0.65f, 0.78f, 0.55f); // row label tint

    // ---------------------------------------------------------
    // RUNTIME FLAGS
    // ---------------------------------------------------------
    private bool uiBuilt = false;
    private bool isVisible = true;

    // ---------------------------------------------------------
    private void Start()
    {
        if (plantState == null)
            plantState = GetComponent<PlantState>();

        if (lightSensor == null)
            lightSensor = GetComponent<LightSensor>();

        // ── FIX: Don't build UI immediately ──
        // Wait until we have soil or are in dummy mode.
        // Update() will build it when appropriate.
    }

    // ---------------------------------------------------------
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
            canvasRef.gameObject.SetActive(shouldShowUI && isVisible);

            if (shouldShowUI && isVisible)
            {
                RefreshValues();
                BillboardCanvas();
            }
        }
    }

    // ---------------------------------------------------------
    private bool ShouldShowUI()
    {
        if (useDummyValues) return true;
        if (plantState == null) return false;

        PotContents pot = plantState.GetComponentInParent<PotContents>();
        if (pot == null || !pot.HasSoil) return false;

        return true;
    }

    // ---------------------------------------------------------
    public void SetVisible(bool visible)
    {
        isVisible = visible;

        if (canvasRef != null)
        {
            bool shouldShow = ShouldShowUI() && isVisible;
            canvasRef.gameObject.SetActive(shouldShow);
        }
    }

    // =========================================================
    // BUILD UI
    // =========================================================
    private void BuildUI()
    {
        // ── Canvas ──
        GameObject canvasGO = new GameObject("PlantUI_Canvas");
        canvasGO.transform.SetParent(transform);
        canvasGO.transform.localPosition = Vector3.up * heightOffset;
        canvasGO.transform.localRotation = Quaternion.identity;
        canvasGO.transform.localScale = Vector3.one * canvasScale;

        canvasRef = canvasGO.AddComponent<Canvas>();
        canvasRef.renderMode = RenderMode.WorldSpace;

        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(260, 290); // tall enough for all 6 rows + divider

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Panel ──
        GameObject panelGO = CreateUIObject("Panel", canvasGO.transform);
        RectTransform panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImg = panelGO.AddComponent<Image>();
        panelImg.color = ColPanelBg;

        // ── State label (top) ──
        stateLabel = CreateLabel(
            "StateLabel", panelGO.transform,
            new Vector2(0f, 0.88f), new Vector2(1f, 1f),
            "● DEAD", 13, ColDead, FontStyles.Bold
        );

        // ── Live stat rows — evenly spaced in the top 60% ──
        sunPips = CreateStatRow(panelGO.transform, "Sunlight", 0.70f, false);
        soilPips = CreateStatRow(panelGO.transform, "Soil", 0.56f, false);
        waterPips = CreateStatRow(panelGO.transform, "Water", 0.42f, false);

        // ── Divider hint label ──
        CreateLabel(
            "DividerLbl", panelGO.transform,
            new Vector2(0.02f, 0.34f), new Vector2(0.98f, 0.41f),
            "─── COMING SOON ───", 7,
            new Color(0.45f, 0.50f, 0.75f, 0.35f)
        );

        // ── Placeholder: Happiness pip row ──
        happinessPips = CreateStatRow(panelGO.transform, "Proximity", 0.22f, true);

        // ── Placeholder: Ability boolean ──
        abilityLabel = CreateBoolRow(panelGO.transform, "Ability", 0.11f);

        // ── Placeholder: Miasma boolean ──
        miasmaPips = CreateStatRow(panelGO.transform, "Miasma", 0.02f, true);
    }

    // ---------------------------------------------------------
    // CreateStatRow — builds a label + 5 pips.
    //   isFuture: pips rendered in placeholder colour and locked empty.
    // ---------------------------------------------------------
    private Image[] CreateStatRow(Transform parent, string labelText, float anchorY, bool isFuture)
    {
        float rowHeight = 0.16f;

        Color labelColour = isFuture
            ? ColFutureText
            : new Color(0.78f, 0.90f, 0.78f);

        CreateLabel(
            labelText + "_Lbl", parent,
            new Vector2(0.02f, anchorY),
            new Vector2(0.45f, anchorY + rowHeight),
            labelText, 9, labelColour
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
            // Future rows: all pips drawn in a dim placeholder tint, never filled.
            img.color = isFuture ? ColFuturePip : ColPipEmpty;
            pips[i] = img;
        }

        return pips;
    }

    // ---------------------------------------------------------
    // CreateBoolRow — builds a label + a small TRUE/FALSE badge.
    //   Always shown in placeholder style (not yet wired).
    // ---------------------------------------------------------
    private TextMeshProUGUI CreateBoolRow(Transform parent, string labelText, float anchorY)
    {
        float rowHeight = 0.10f;

        CreateLabel(
            labelText + "_Lbl", parent,
            new Vector2(0.02f, anchorY),
            new Vector2(0.45f, anchorY + rowHeight),
            labelText, 9, ColFutureText
        );

        // Badge — shows "—" (not yet implemented)
        TextMeshProUGUI badge = CreateLabel(
            labelText + "_Val", parent,
            new Vector2(0.47f, anchorY),
            new Vector2(0.98f, anchorY + rowHeight),
            "—", 9, ColFutureLabel
        );

        return badge;
    }

    // ---------------------------------------------------------
    private TextMeshProUGUI CreateLabel(
        string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        string text, int fontSize, Color colour,
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

    // ---------------------------------------------------------
    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
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
            // Sun — map normalised light (0–1) to pip count (1–5)
            float lightRaw = lightSensor != null ? lightSensor.NormalisedIntensity : 0f;
            sunScore = Mathf.RoundToInt(Mathf.Lerp(1f, 5f, lightRaw));

            // Soil — PlantState.SoilScore is 0–2
            if (plantState != null)
            {
                soilScore = plantState.SoilScore switch
                {
                    2 => 5,
                    1 => 3,
                    _ => 1
                };
            }
            else soilScore = 1;

            // Water — map WaterLevel (0–max) to pip count (1–5)
            PotContents pot = plantState != null
                ? plantState.GetComponentInParent<PotContents>()
                : null;

            float waterRaw = pot != null ? pot.WaterLevel : 0f;
            float waterMax = pot != null ? pot.plantWaterMax : 10f;

            waterScore = waterMax > 0f
                ? Mathf.RoundToInt(Mathf.Lerp(1f, 5f, waterRaw / waterMax))
                : 1;

            waterScore = Mathf.Max(1, waterScore);
        }

        UpdatePips(sunPips, sunScore);
        UpdatePips(soilPips, soilScore);
        UpdatePips(waterPips, waterScore);

        // Happiness / Ability / Miasma are not yet wired — leave them in
        // their static placeholder state (dim pips + "—" badge).
        // TODO: Replace the blocks below with real logic when ready.
        //
        //   Happiness  → call UpdatePips(happinessPips, proximityScore);
        //   Ability    → abilityLabel.text = hasAbility ? "✔ YES" : "✗ NO";
        //                abilityLabel.color = hasAbility ? ColRevived : ColDead;
        //   Miasma     → UpdatePips(miasmaPips, miasmaIntensity); // 1-5

        UpdateStateLabel();
    }

    // ---------------------------------------------------------
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

                case PlantStateEnum.Dead:
                    stateLabel.text = "● DEAD";
                    stateLabel.color = ColDead;
                    return;
            }
        }

        // Dummy mode — derive state from pip averages
        int avg = (dummySunlight + dummySoil + dummyWater) / 3;

        if (avg >= 4) { stateLabel.text = "● REVIVED"; stateLabel.color = ColRevived; }
        else if (avg >= 2) { stateLabel.text = "● INTERMEDIATE"; stateLabel.color = ColIntermediate; }
        else { stateLabel.text = "● DEAD"; stateLabel.color = ColDead; }
    }

    // ---------------------------------------------------------
    private void UpdatePips(Image[] pips, int score)
    {
        score = Mathf.Clamp(score, 1, 5);
        for (int i = 0; i < pips.Length; i++)
            pips[i].color = i < score ? ColPipFilled : ColPipEmpty;
    }

    // ---------------------------------------------------------
    // BillboardCanvas — rotates the canvas to face the camera on the
    // Y axis only (yaw). This prevents the warping/flipping that
    // happens with a full LookAt when the camera pitches steeply
    // above or below the canvas plane.
    // ---------------------------------------------------------
    private void BillboardCanvas()
    {
        if (canvasRef == null || Camera.main == null) return;

        // Direction from canvas to camera, projected onto the XZ plane.
        Vector3 toCamera = Camera.main.transform.position - canvasRef.transform.position;
        toCamera.y = 0f;

        // Guard: skip if camera is almost directly above/below (avoids NaN).
        if (toCamera.sqrMagnitude > 0.0001f)
        {
            canvasRef.transform.rotation = Quaternion.LookRotation(-toCamera);
        }
    }
}