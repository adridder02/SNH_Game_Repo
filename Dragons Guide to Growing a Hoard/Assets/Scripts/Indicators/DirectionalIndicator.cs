// =============================================================
// DirectionalIndicator.cs
// -------------------------------------------------------------
// Add this component to your Player (or any persistent manager
// object). It discovers all TrackedTarget instances in the scene
// automatically and creates a UI arrow + name label for each one.
//
// BEHAVIOUR:
//   Always       → a big, pulsing, outlined MARKER ICON sits on the target with its name and distance, drawn
//                  on the overlay canvas so walls / scenery never hide it.
//   Off-screen   → the marker is pinned to the screen edge with an arrow pointing the way.
//   On-screen    → the marker sits on the target (bobbing + pulsing); no arrow.
//   (Set Always Show Label off to get the old "label only fades in when you look at it" behaviour.)
//
// SETUP:
//   1. Attach this to your Player (or a UI manager GameObject).
//   2. Assign a Canvas (Screen Space - Overlay) to 'canvas'.
//   3. Assign arrowSprite (any simple arrow/triangle sprite).
//   4. Assign labelFont if you have one; otherwise TMPro default is used.
//   5. Add TrackedTarget to any object you want tracked.
// =============================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DirectionalIndicator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("A Screen Space - Overlay canvas. Create one if you don't have one.")]
    public Canvas canvas;

    [Tooltip("The camera to project from. Leave null to use Camera.main.")]
    public Camera playerCamera;

    [Header("Arrow")]
    [Tooltip("Sprite for the directional arrow. A simple chevron/triangle works well.")]
    public Sprite arrowSprite;

    [Tooltip("Size of the arrow icon in pixels.")]
    public float arrowSize = 32f;

    [Tooltip("How many pixels from the screen edge the arrow is clamped to.")]
    public float edgePadding = 48f;

    [Header("Marker Icon (the thing that sticks out)")]
    [Tooltip("Icon drawn on / over the target - e.g. a bold exclamation mark, diamond or pin. Per-target override: " +
             "TrackedTarget.iconOverride. Falls back to the arrow sprite if empty.")]
    public Sprite markerSprite;
    [Tooltip("Size of the marker icon in pixels.")]
    public float markerSize = 72f;
    [Tooltip("Outline colour drawn around the marker and label so they read against any background.")]
    public Color markerOutlineColor = new Color(0f, 0f, 0f, 0.9f);
    [Tooltip("How much the marker pulses in size (0.15 = +/-15%).")]
    public float pulseAmount = 0.15f;
    [Tooltip("Pulses per second.")]
    public float pulseSpeed = 2.5f;
    [Tooltip("Pixels the marker bobs up and down while on screen.")]
    public float bobPixels = 8f;
    [Tooltip("Show the distance to the target under its name.")]
    public bool showDistance = true;
    [Tooltip("Format for the distance; {0} is metres.")]
    public string distanceFormat = "{0:0} m";
    [Tooltip("Keep the name label visible all the time. Off = old behaviour (fades in only when you look at it).")]
    public bool alwaysShowLabel = true;

    [Header("Name Label")]
    [Tooltip("Dot product threshold above which the name label starts fading in. " +
             "1.0 = only when looking dead-on; 0.9 = within ~25 degrees.")]
    [Range(0.7f, 1f)]
    public float facingThreshold = 0.92f;

    [Tooltip("How fast the label fades in and out.")]
    public float labelFadeSpeed = 4f;

    [Tooltip("Font size for the name label.")]
    public float labelFontSize = 18f;

    [Tooltip("Optional background behind the label text. Leave null to use no background.")]
    public Sprite labelBackgroundSprite;

    // ---------------------------------------------------------------
    // Per-target runtime state — one entry per TrackedTarget found.
    // ---------------------------------------------------------------
    private struct IndicatorEntry
    {
        public TrackedTarget target;
        public RectTransform arrowRect;
        public Image arrowImage;
        public RectTransform markerRect;
        public Image markerImage;
        public RectTransform labelRect;
        public TextMeshProUGUI labelText;
        public Image labelBackground;
        public float currentAlpha;
    }

    private List<IndicatorEntry> entries = new List<IndicatorEntry>();
    private RectTransform canvasRect;
    private Transform distanceOrigin;

    /// <summary>Auto-set in Awake — lets tutorial/gameplay code reach ActivateTarget/DeactivateTarget
    /// below without holding its own reference (same pattern as TutorialSequenceController.Instance).</summary>
    public static DirectionalIndicator Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    // ---------------------------------------------------------------
    // Only-sometimes-tracked targets — for a TrackedTarget that shouldn't always show an arrow (e.g.
    // a tutorial-only pointer at a placement grid or the water source), these toggle ONLY the
    // target's TrackedTarget.IsTrackingActive flag (see TrackedTarget.cs), never the GameObject's own
    // active state — that GameObject is usually the real grid/water source itself, so disabling it
    // would disable the actual object, not just its arrow.
    // ---------------------------------------------------------------
    /// <summary>Turns this target's arrow/label on immediately (the object itself is never touched).
    /// Wire this from a TutorialStep's onStepShown (see TutorialStepData) for a step that should point
    /// the player somewhere. Safe to call on an already-active target.</summary>
    public void ActivateTarget(TrackedTarget target)
    {
        if (target == null) return;
        target.IsTrackingActive = true;
    }

    /// <summary>Turns this target's arrow/label off immediately (the object itself is never touched).
    /// Wire this from a TutorialStep's onStepHidden to turn a step's indicator back off once that
    /// step is done (see TutorialStepData). Safe to call on an already-inactive target.</summary>
    public void DeactivateTarget(TrackedTarget target)
    {
        if (target == null) return;
        target.IsTrackingActive = false;
    }

    // ---------------------------------------------------------------
    private void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (canvas == null)
        {
            Debug.LogError("[DirectionalIndicator] No Canvas assigned. " +
                           "Create a Screen Space - Overlay canvas and assign it.");
            enabled = false;
            return;
        }

        canvasRect = canvas.GetComponent<RectTransform>();
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        distanceOrigin = pc != null ? pc.transform : (playerCamera != null ? playerCamera.transform : null);
        RefreshTargets();
    }

    // ---------------------------------------------------------------
    // RefreshTargets — call this any time you add/remove TrackedTargets
    // at runtime (e.g. when planting or removing plants).
    // ---------------------------------------------------------------
    public void RefreshTargets()
    {
        // Clean up existing entries.
        foreach (IndicatorEntry e in entries)
        {
            if (e.arrowRect != null) Destroy(e.arrowRect.gameObject);
            if (e.markerRect != null) Destroy(e.markerRect.gameObject);
            if (e.labelRect != null) Destroy(e.labelRect.gameObject);
        }
        entries.Clear();

        // Find all TrackedTargets in the scene.
        TrackedTarget[] targets = FindObjectsByType<TrackedTarget>();
        foreach (TrackedTarget t in targets)
            entries.Add(CreateEntry(t));

        Debug.Log($"[DirectionalIndicator] Tracking {entries.Count} targets.");
    }

    // ---------------------------------------------------------------
    // CreateEntry — builds the arrow Image and name label for one target.
    // ---------------------------------------------------------------
    private IndicatorEntry CreateEntry(TrackedTarget target)
    {
        // ------ Arrow ------
        GameObject arrowGO = new GameObject($"Arrow_{target.displayName}", typeof(RectTransform), typeof(Image));
        arrowGO.transform.SetParent(canvas.transform, false);

        RectTransform arrowRect = arrowGO.GetComponent<RectTransform>();
        arrowRect.sizeDelta = new Vector2(arrowSize, arrowSize);
        arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRect.pivot = new Vector2(0.5f, 0.5f);

        Image arrowImage = arrowGO.GetComponent<Image>();
        if (arrowSprite != null) arrowImage.sprite = arrowSprite;
        arrowImage.color = target.indicatorColor;

        // ------ Marker icon ------
        GameObject markerGO = new GameObject($"Marker_{target.displayName}", typeof(RectTransform), typeof(Image));
        markerGO.transform.SetParent(canvas.transform, false);
        RectTransform markerRect = markerGO.GetComponent<RectTransform>();
        markerRect.anchorMin = markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = new Vector2(markerSize, markerSize) * Mathf.Max(0.1f, target.markerScale);
        Image markerImage = markerGO.GetComponent<Image>();
        Sprite icon = target.iconOverride != null ? target.iconOverride : (markerSprite != null ? markerSprite : arrowSprite);
        if (icon != null) markerImage.sprite = icon;
        markerImage.preserveAspect = true;
        markerImage.raycastTarget = false;
        markerImage.color = target.indicatorColor;
        Outline markerOutline = markerGO.AddComponent<Outline>();
        markerOutline.effectColor = markerOutlineColor;
        markerOutline.effectDistance = new Vector2(3f, -3f);

        // ------ Label background (optional) ------
        GameObject labelGO = new GameObject($"Label_{target.displayName}", typeof(RectTransform));
        labelGO.transform.SetParent(canvas.transform, false);

        RectTransform labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);

        Image labelBG = null;
        if (labelBackgroundSprite != null)
        {
            Image bg = labelGO.AddComponent<Image>();
            bg.sprite = labelBackgroundSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            labelBG = bg;
        }

        // ------ Label text ------
        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(labelGO.transform, false);

        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = target.displayName;
        tmp.fontSize = labelFontSize;
        tmp.color = target.indicatorColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap; // replaces the deprecated enableWordWrapping = false

        tmp.fontStyle = FontStyles.Bold;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = markerOutlineColor;
        tmp.raycastTarget = false;

        // Size the label rect to fit the name (and a distance line) with a little padding.
        float width = Mathf.Max(tmp.preferredWidth, labelFontSize * 4f) + 24f;
        float height = (showDistance ? labelFontSize * 2.1f : labelFontSize * 1.1f) + 12f;
        labelRect.sizeDelta = new Vector2(width, height);
        tmp.alignment = TextAlignmentOptions.Center;

        return new IndicatorEntry
        {
            target = target,
            arrowRect = arrowRect,
            arrowImage = arrowImage,
            markerRect = markerRect,
            markerImage = markerImage,
            labelRect = labelRect,
            labelText = tmp,
            labelBackground = labelBG,
            currentAlpha = 0f
        };
    }

    // ---------------------------------------------------------------
    // Update — drives the arrow position/rotation and label alpha
    // for every tracked target each frame.
    // ---------------------------------------------------------------
    private void Update()
    {
        if (playerCamera == null || canvasRect == null) return;

        Vector2 canvasSize = canvasRect.rect.size;
        float canvasScale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        Vector2 half = canvasSize * 0.5f;
        float pad = edgePadding / canvasScale + markerSize * 0.5f;
        Vector3 camForward = playerCamera.transform.forward;
        float t = Time.unscaledTime;

        for (int i = 0; i < entries.Count; i++)
        {
            IndicatorEntry e = entries[i];
            if (e.target == null || !e.target.IsTrackingActive)
            {
                // Destroyed, or tracking turned off (e.g. tutorial step ended) - hide everything but leave the
                // target's own GameObject completely alone.
                if (e.arrowRect != null) e.arrowRect.gameObject.SetActive(false);
                if (e.markerRect != null) e.markerRect.gameObject.SetActive(false);
                SetAlpha(ref e, 0f);
                entries[i] = e;
                continue;
            }

            Vector3 worldPos = e.target.WorldPosition;
            Vector3 viewportPos = playerCamera.WorldToViewportPoint(worldPos);
            bool inFront = viewportPos.z > 0f;
            bool onScreen = inFront
                && viewportPos.x >= 0f && viewportPos.x <= 1f
                && viewportPos.y >= 0f && viewportPos.y <= 1f;

            float pulse = 1f + Mathf.Sin(t * pulseSpeed * Mathf.PI * 2f) * pulseAmount;
            float baseScale = Mathf.Max(0.1f, e.target.markerScale);

            Vector2 markerPos;
            if (onScreen)
            {
                e.arrowRect.gameObject.SetActive(false);

                Vector2 p = new Vector2((viewportPos.x - 0.5f) * canvasSize.x, (viewportPos.y - 0.5f) * canvasSize.y);
                // keep the whole marker visible even when the target is right at the screen edge
                p.x = Mathf.Clamp(p.x, -half.x + pad, half.x - pad);
                p.y = Mathf.Clamp(p.y, -half.y + pad, half.y - pad);
                p.y += Mathf.Sin(t * 3f) * bobPixels;
                markerPos = p;
                e.markerRect.localRotation = Quaternion.identity;
            }
            else
            {
                Vector2 dir = new Vector2(viewportPos.x - 0.5f, viewportPos.y - 0.5f);
                if (!inFront) dir = -dir;
                if (dir == Vector2.zero) dir = Vector2.up;
                dir.Normalize();

                float scaleX = Mathf.Abs(dir.x) > 0.001f ? (half.x - pad) / Mathf.Abs(dir.x) : float.MaxValue;
                float scaleY = Mathf.Abs(dir.y) > 0.001f ? (half.y - pad) / Mathf.Abs(dir.y) : float.MaxValue;
                markerPos = dir * Mathf.Min(scaleX, scaleY);

                // arrow sits just outside the marker, pointing the way
                e.arrowRect.gameObject.SetActive(true);
                e.arrowRect.anchoredPosition = markerPos + dir * (markerSize * baseScale * 0.5f + arrowSize * 0.6f);
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                e.arrowRect.localRotation = Quaternion.Euler(0f, 0f, angle);
                e.arrowRect.localScale = Vector3.one * pulse;
            }

            e.markerRect.gameObject.SetActive(true);
            e.markerRect.anchoredPosition = markerPos;
            e.markerRect.localScale = Vector3.one * pulse;

            // Label: always on (or fades in when faced, if Always Show Label is off). Sits below the marker.
            float targetAlpha = 1f;
            if (!alwaysShowLabel)
            {
                if (onScreen)
                {
                    Vector3 dirToTarget = (worldPos - playerCamera.transform.position).normalized;
                    targetAlpha = Mathf.InverseLerp(facingThreshold, 1f, Vector3.Dot(camForward, dirToTarget));
                }
                else targetAlpha = 0f;
            }
            e.currentAlpha = Mathf.MoveTowards(e.currentAlpha, targetAlpha, labelFadeSpeed * Time.deltaTime);

            if (e.labelRect != null)
            {
                float below = markerSize * baseScale * 0.5f + e.labelRect.sizeDelta.y * 0.5f + 4f;
                Vector2 labelPos = markerPos + Vector2.down * below;
                // keep the label on screen
                labelPos.x = Mathf.Clamp(labelPos.x, -half.x + e.labelRect.sizeDelta.x * 0.5f, half.x - e.labelRect.sizeDelta.x * 0.5f);
                labelPos.y = Mathf.Clamp(labelPos.y, -half.y + e.labelRect.sizeDelta.y * 0.5f, half.y - e.labelRect.sizeDelta.y * 0.5f);
                e.labelRect.anchoredPosition = labelPos;
            }

            if (e.labelText != null)
            {
                if (showDistance && distanceOrigin != null)
                {
                    float d = Vector3.Distance(distanceOrigin.position, worldPos);
                    e.labelText.text = e.target.displayName + "\n" + string.Format(distanceFormat, d);
                }
                else e.labelText.text = e.target.displayName;
            }

            ApplyAlpha(ref e);
            entries[i] = e;
        }
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------
    private void SetAlpha(ref IndicatorEntry e, float alpha)
    {
        e.currentAlpha = alpha;
        ApplyAlpha(ref e);
    }

    private void ApplyAlpha(ref IndicatorEntry e)
    {
        if (e.labelText != null)
        {
            Color c = e.labelText.color;
            c.a = e.currentAlpha;
            e.labelText.color = c;
        }

        if (e.labelBackground != null)
        {
            Color c = e.labelBackground.color;
            c.a = e.currentAlpha * 0.55f;
            e.labelBackground.color = c;
        }

    }

    private void OnDestroy()
    {
        // Clean up instantiated UI when this component is removed.
        foreach (IndicatorEntry e in entries)
        {
            if (e.arrowRect != null) Destroy(e.arrowRect.gameObject);
            if (e.markerRect != null) Destroy(e.markerRect.gameObject);
            if (e.labelRect != null) Destroy(e.labelRect.gameObject);
        }
    }
}