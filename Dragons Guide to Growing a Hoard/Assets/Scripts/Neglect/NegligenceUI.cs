using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;


public class NegligenceUI : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Panel layout")]
    public float panelWidth  = 400f;
    public float padding     = 5f;
    public float spacing     = 6f;
    public float rowHeight   = 60f;

    [Tooltip("(0,1)=top-left  (1,1)=top-right  (0,0)=bottom-left")]
    public Vector2 panelAnchor = new Vector2(0f, 1f);
    public Vector2 panelOffset = new Vector2(14f, -14f);

    [Header("Colours")]
    public Color panelBg         = new Color(0.05f, 0.05f, 0.05f, 0.88f);
    public Color headerBg        = new Color(0.10f, 0.10f, 0.10f, 1.00f);
    public Color dividerColor    = new Color(0.30f, 0.30f, 0.30f, 1.00f);
    public Color healthyColor    = new Color(0.20f, 0.85f, 0.25f, 1.00f);
    public Color neglectedColor  = new Color(1.00f, 0.78f, 0.00f, 1.00f);
    public Color criticalColor   = new Color(0.90f, 0.15f, 0.15f, 1.00f);
    public Color labelColor      = new Color(0.95f, 0.95f, 0.95f, 1.00f);
    public Color dimColor        = new Color(0.55f, 0.55f, 0.55f, 1.00f);
    public Color barBgColor      = new Color(0.20f, 0.20f, 0.20f, 1.00f);
    public Color combinedBgColor = new Color(0.12f, 0.12f, 0.12f, 1.00f);

    [Header("Scaling")]
    [Tooltip("Global multiplier for all UI sizes (1 = normal, 0.8 = smaller, 1.2 = larger)")]
    public float uiScale = 0.8f;



    [Header("Pulse")]
    public bool  pulseOnCritical = true;
    public float pulseSpeed      = 2f;

    
    // -------------------------------------------------------------------------
    // Image-based bar (no Slider — avoids fill rect anchor bug)
    // -------------------------------------------------------------------------

    private class Bar
    {
        private readonly RectTransform _fillRT;
        private readonly Image         _fillImg;

        public Bar(RectTransform fillRT, Image fillImg)
        {
            _fillRT  = fillRT;
            _fillImg = fillImg;
        }

        public Image FillImage => _fillImg;

        public void SetValue(float value)
        {
            if (_fillRT == null) return;
            float t        = Mathf.Clamp01(value / 100f);
            _fillRT.anchorMin = Vector2.zero;
            _fillRT.anchorMax = new Vector2(t, 1f);
            _fillRT.offsetMin = Vector2.zero;
            _fillRT.offsetMax = Vector2.zero;
        }

        public void SetColor(Color c)
        {
            if (_fillImg != null) _fillImg.color = c;
        }
    }

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private Bar    _overallBar;
    private Text   _overallPct;
    private struct ZoneRow
    {
        public Text  nameLabel;
        public Text  pctLabel;
        public Bar   bar;
        public Image contribDot;
        public Image warningDot;
    }
    private List<ZoneRow> _rows = new List<ZoneRow>();

    private Text       _combinedLabel;
    private GameObject _finishedBanner;
    private RectTransform _panelRT;

    private bool _expanded = false;

    private List<ZoneHealth.NegligenceLevel> _zoneLevels
        = new List<ZoneHealth.NegligenceLevel>();
    private ZoneHealth.NegligenceLevel _overallLevel
        = ZoneHealth.NegligenceLevel.Healthy;

    private int        _builtForZoneCount = 0;
    private GameObject _canvasRoot;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        GreenhouseHealth gh = GetComponent<GreenhouseHealth>();
        BuildUI(gh != null ? gh.zones.Count : 0);
    }

    private void Update()
    {
        if (pulseOnCritical) AnimatePulse();
    }

    // -------------------------------------------------------------------------
    // Public refresh
    // -------------------------------------------------------------------------

    public void Refresh(GreenhouseHealth greenhouse)
    {
        if (greenhouse == null) return;

        if (greenhouse.zones.Count != _builtForZoneCount)
        {
            Destroy(_canvasRoot);
            _rows.Clear();
            _zoneLevels.Clear();
            BuildUI(greenhouse.zones.Count);
        }

        float overall = greenhouse.OverallHappiness;

        if (greenhouse.zones.Count > 0 && greenhouse.zones[0] != null)
            _overallLevel = ScoreToLevel(overall,
                greenhouse.zones[0].neglectedThreshold,
                greenhouse.zones[0].criticalThreshold);

        _overallBar?.SetValue(overall);
        _overallBar?.SetColor(LevelToColor(_overallLevel));
        if (_overallPct != null) _overallPct.text = $"{overall:F0}%";

        float totalScore = 0f;
        foreach (var z in greenhouse.zones)
            if (z != null) totalScore += z.ZoneHappiness;

        for (int i = 0; i < greenhouse.zones.Count && i < _rows.Count; i++)
        {
            ZoneHealth zone = greenhouse.zones[i];
            if (zone == null) continue;

            ZoneRow row   = _rows[i];
            float   score = zone.ZoneHappiness;

            _zoneLevels[i] = zone.Negligence;

            row.bar?.SetValue(score);
            row.bar?.SetColor(LevelToColor(zone.Negligence));

            if (row.nameLabel != null) row.nameLabel.text = zone.zoneName;
            if (row.pctLabel  != null) row.pctLabel.text  = $"{score:F0}%";

            if (row.contribDot != null)
            {
                float share   = totalScore > 0f ? score / totalScore : 0f;
                float dotSize = Mathf.Lerp(6f, 18f, share);
                row.contribDot.rectTransform.sizeDelta = new Vector2(dotSize, dotSize);
                row.contribDot.color = LevelToColor(zone.Negligence);
            }

            if (row.warningDot != null)
            {
                bool warn = zone.Negligence != ZoneHealth.NegligenceLevel.Healthy;
                row.warningDot.gameObject.SetActive(warn);
                if (warn) row.warningDot.color = LevelToColor(zone.Negligence);
            }
        }

        if (_combinedLabel != null)
        {
            string mode = greenhouse.weightedAverage ? "weighted avg" : "avg";
            _combinedLabel.text = greenhouse.zones.Count > 0
                ? $"Combined ({mode} of {greenhouse.zones.Count} zones)  →  {overall:F0}%"
                : "No zones assigned";
        }

        // Banner now tied to overall level instead of IsFinished
        if (_finishedBanner != null)
        {
            bool neglected = _overallLevel == ZoneHealth.NegligenceLevel.Critical;
            _finishedBanner.SetActive(neglected);
        }
    }

    // -------------------------------------------------------------------------
    // UI construction
    // -------------------------------------------------------------------------

    private void BuildUI(int zoneCount)
    {
        _builtForZoneCount = zoneCount;

        // ---- Canvas --------------------------------------------------------
        _canvasRoot = new GameObject("NegligenceCanvas");
        Canvas cv   = _canvasRoot.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 10;
        _canvasRoot.AddComponent<CanvasScaler>();
        _canvasRoot.AddComponent<GraphicRaycaster>();

        // ---- Panel ---------------------------------------------------------
        GameObject panel = NewObj("NegligencePanel", _canvasRoot.transform);
        Image panelImg   = panel.AddComponent<Image>();
        panelImg.color   = new Color(0,0,0,0); // transparent

        _panelRT = panel.GetComponent<RectTransform>();
        _panelRT.anchorMin        = panelAnchor;
        _panelRT.anchorMax        = panelAnchor;
        _panelRT.pivot            = panelAnchor;
        _panelRT.anchoredPosition = panelOffset * uiScale;
        _panelRT.sizeDelta        = new Vector2(panelWidth * uiScale, 0f);

        VerticalLayoutGroup vlg    = panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding                = new RectOffset(0, 0, 0, 0);
        vlg.spacing                = Mathf.RoundToInt(2f * uiScale);
        vlg.childControlWidth      = true;
        vlg.childForceExpandWidth  = true;
        vlg.childControlHeight     = false;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf       = panel.AddComponent<ContentSizeFitter>();
        csf.verticalFit             = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit           = ContentSizeFitter.FitMode.Unconstrained;

        // ---- Header --------------------------------------------------------
        GameObject header = NewFixedRow("Header", panel.transform, 24f * uiScale);
        var hhlg = header.GetComponent<HorizontalLayoutGroup>();
        hhlg.padding = new RectOffset((int)(padding * uiScale), (int)(padding * uiScale), 4, 4);
        NewLabel("Title", header.transform, "GREENHOUSE HEALTH", Mathf.RoundToInt(12 * uiScale),
                TextAnchor.MiddleLeft).fontStyle = FontStyle.Bold;

        // ---- Overall bar row -----------------------------------------------
        GameObject overallRow = NewFixedRow("OverallRow", panel.transform, 40f * uiScale);
        var ohlg = overallRow.GetComponent<HorizontalLayoutGroup>();
        ohlg.padding = new RectOffset((int)(padding * uiScale), (int)(padding * uiScale), 4, 4);
        ohlg.spacing = 6f * uiScale;

        Text overallTitle = NewLabel("OverallTitle", overallRow.transform, "Overall Health", Mathf.RoundToInt(13 * uiScale), TextAnchor.MiddleLeft);
        overallTitle.fontStyle = FontStyle.Bold;
        overallTitle.resizeTextForBestFit = true;

        _overallBar = NewBar("OverallBar", overallRow.transform, healthyColor, flex: true);

        _overallPct = NewLabel("Pct", overallRow.transform, "100%", Mathf.RoundToInt(13 * uiScale), TextAnchor.MiddleRight);
        _overallPct.resizeTextForBestFit = true;
        var pLE = _overallPct.gameObject.AddComponent<LayoutElement>();
        pLE.preferredWidth = 40f * uiScale;
        pLE.flexibleWidth  = 0f;

        // ---- Finished banner -----------------------------------------------
        _finishedBanner = NewObj("FinishedBanner", panel.transform);
        var fle = _finishedBanner.AddComponent<LayoutElement>();
        fle.preferredHeight = 24f * uiScale;
        fle.flexibleWidth   = 1f;
        _finishedBanner.AddComponent<Image>().color = new Color(0.75f, 0.08f, 0.08f, 0.92f);

        Text ft = NewLabel("FTxt", _finishedBanner.transform, "GREENHOUSE NEGLECTED", Mathf.RoundToInt(12 * uiScale), TextAnchor.MiddleCenter);
        ft.color     = Color.white;
        ft.fontStyle = FontStyle.Bold;
        StretchRT(ft.GetComponent<RectTransform>());
        _finishedBanner.SetActive(false);

        // ---- Divider -------------------------------------------------------
        //NewDivider(panel.transform);

        // ---- Zone rows -----------------------------------------------------
        _rows.Clear();
        _zoneLevels.Clear();

        for (int i = 0; i < zoneCount; i++)
        {
            GameObject row = NewFixedRow($"ZoneRow{i}", panel.transform, 24f * uiScale);
            var rhlg = row.GetComponent<HorizontalLayoutGroup>();
            rhlg.spacing        = 4f * uiScale;
            rhlg.padding        = new RectOffset((int)(padding * uiScale), (int)(padding * uiScale), 2, 2);
            rhlg.childAlignment = TextAnchor.MiddleLeft;

            Text nameLabel = NewLabel("Name", row.transform, $"Zone {i}", Mathf.RoundToInt(10 * uiScale), TextAnchor.MiddleLeft);
            nameLabel.color = dimColor;
            nameLabel.resizeTextForBestFit = true;

            var nle = nameLabel.gameObject.AddComponent<LayoutElement>();
            nle.preferredWidth = 50f * uiScale;
            nle.flexibleWidth  = 0f;

            Bar bar = NewBar($"Bar{i}", row.transform, healthyColor, flex: true);

            Text pctLabel = NewLabel("Pct", row.transform, "100%", Mathf.RoundToInt(10 * uiScale), TextAnchor.MiddleRight);
            pctLabel.color = dimColor;
            pctLabel.resizeTextForBestFit = true;

            var ple = pctLabel.gameObject.AddComponent<LayoutElement>();
            ple.preferredWidth = 30f * uiScale;
            ple.flexibleWidth  = 0f;

           
            _rows.Add(new ZoneRow
            {
                nameLabel  = nameLabel,
                pctLabel   = pctLabel,
                bar        = bar,
            });
            _zoneLevels.Add(ZoneHealth.NegligenceLevel.Healthy);
        }

        // ---- Combined label row --------------------------------------------
        GameObject combRow = NewFixedRow("CombRow", panel.transform, 20f * uiScale);
        _combinedLabel = NewLabel("CombLbl", combRow.transform,
                                "Combined  →  --%", Mathf.RoundToInt(10 * uiScale), TextAnchor.MiddleCenter);
        _combinedLabel.color = dimColor;
        StretchRT(_combinedLabel.GetComponent<RectTransform>());

        Debug.Log("[NegligenceUI] UI built (scaled). Zone count: " + zoneCount);
    }

    // -------------------------------------------------------------------------
    // Pulse
    // -------------------------------------------------------------------------

    private void AnimatePulse()
    {
        float a = 0.65f + Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) * 0.35f;

        for (int i = 0; i < _zoneLevels.Count; i++)
        {
            if (_zoneLevels[i] != ZoneHealth.NegligenceLevel.Critical) continue;
            if (i >= _rows.Count || _rows[i].bar?.FillImage == null) continue;
            Color c = _rows[i].bar.FillImage.color; c.a = a;
            _rows[i].bar.FillImage.color = c;
        }

        if (_overallLevel == ZoneHealth.NegligenceLevel.Critical
            && _overallBar?.FillImage != null)
        {
            Color c = _overallBar.FillImage.color; c.a = a;
            _overallBar.FillImage.color = c;
        }
    }

    // -------------------------------------------------------------------------
    // UI factory helpers
    // -------------------------------------------------------------------------

    private GameObject NewObj(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private GameObject NewFixedRow(string name, Transform parent, float height)
    {
        GameObject row = NewObj(name, parent);
        var le         = row.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.flexibleWidth   = 1f;

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.childControlHeight     = true;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth      = false;
        hlg.childForceExpandWidth  = false;
        return row;
    }

    private Text NewLabel(string name, Transform parent, string text,
                          int size, TextAnchor align)
    {
        GameObject go = NewObj(name, parent);
        Text t        = go.AddComponent<Text>();
        t.text        = text;
        t.fontSize    = size;
        t.color       = labelColor;
        t.alignment   = align;
        t.font        = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (t.font == null)
            t.font    = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return t;
    }

    private Sprite GradientSprite(Color start, Color end, int width = 128, int height = 6)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        for (int x = 0; x < width; x++)
        {
            Color c = Color.Lerp(start, end, (float)x / (width - 1));
            for (int y = 0; y < height; y++)
                tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }


    private Bar NewBar(string name, Transform parent, Color fillColor, bool flex)
    {
        GameObject container = NewObj(name, parent);
        var le               = container.AddComponent<LayoutElement>();
        le.preferredHeight   = 6f; // slim bar height everywhere
        if (flex) le.flexibleWidth = 1f;

        // Background — slim strip centered vertically
        GameObject bgGO    = NewObj("Bg", container.transform);
        Image bgImg        = bgGO.AddComponent<Image>();
        bgImg.color        = barBgColor;
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0.5f);
        bgRT.anchorMax = new Vector2(1, 0.5f);
        bgRT.offsetMin = new Vector2(0, -3f);
        bgRT.offsetMax = new Vector2(0, 3f);

        // Fill — same slim strip, width driven by SetValue()
        GameObject fillGO    = NewObj("Fill", container.transform);
        Image fillImg        = fillGO.AddComponent<Image>();
        fillImg.color        = fillColor;
        fillImg.sprite = GradientSprite(fillColor, Color.white * 0.8f); // subtle gradient
        fillImg.type   = Image.Type.Simple;

        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0, 0.5f);
        fillRT.anchorMax = new Vector2(0, 0.5f); // x updated by SetValue
        fillRT.offsetMin = new Vector2(0, -3f);
        fillRT.offsetMax = new Vector2(0, 3f);

        Bar bar = new Bar(fillRT, fillImg);
        bar.SetValue(100f); // initialize anchors
        return bar;
    }

    private void NewDivider(Transform parent)
    {
        GameObject d = NewObj("Divider", parent);
        var le       = d.AddComponent<LayoutElement>();
        le.preferredHeight = 1f;
        le.flexibleWidth   = 1f;
        d.AddComponent<Image>().color = dividerColor;
    }

    private void StretchRT(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private Sprite CircleSprite()
    {
        int res        = 32;
        Texture2D tex  = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Vector2 c      = new Vector2(res * 0.5f, res * 0.5f);
        float r        = res * 0.5f - 1f;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
                tex.SetPixel(x, y,
                    Vector2.Distance(new Vector2(x, y), c) <= r
                    ? Color.white : Color.clear);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
    }

    // -------------------------------------------------------------------------
    // Utilities
    // -------------------------------------------------------------------------

    private ZoneHealth.NegligenceLevel ScoreToLevel(
        float s, float neglect, float critical)
    {
        if (s >= neglect)  return ZoneHealth.NegligenceLevel.Healthy;
        if (s >= critical) return ZoneHealth.NegligenceLevel.Neglected;
        return ZoneHealth.NegligenceLevel.Critical;
    }

    private Color LevelToColor(ZoneHealth.NegligenceLevel l)
    {
        switch (l)
        {
            case ZoneHealth.NegligenceLevel.Healthy:   return healthyColor;
            case ZoneHealth.NegligenceLevel.Neglected: return neglectedColor;
            case ZoneHealth.NegligenceLevel.Critical:  return criticalColor;
            default:                                   return healthyColor;
        }
    }
}