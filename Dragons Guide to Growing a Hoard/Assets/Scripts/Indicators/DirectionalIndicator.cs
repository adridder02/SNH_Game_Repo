// =============================================================
// DirectionalIndicator.cs
// -------------------------------------------------------------
// Add this component to your Player (or any persistent manager
// object). It discovers all TrackedTarget instances in the scene
// automatically and creates a UI arrow + name label for each one.
//
// BEHAVIOUR:
//   Off-screen  → arrow icon clamped to the screen edge, rotated
//                 to point toward the target.
//   On-screen   → arrow hides; name label fades in as the player
//                 looks more directly at the target (dot product).
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
        public RectTransform labelRect;
        public TextMeshProUGUI labelText;
        public Image labelBackground;
        public float currentAlpha;
    }

    private List<IndicatorEntry> entries = new List<IndicatorEntry>();
    private RectTransform canvasRect;

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
            if (e.labelRect != null) Destroy(e.labelRect.gameObject);
        }
        entries.Clear();

        // Find all TrackedTargets in the scene.
        TrackedTarget[] targets = FindObjectsByType<TrackedTarget>(FindObjectsSortMode.None);
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
        tmp.enableWordWrapping = false;

        // Size the label rect to fit the text with a little padding.
        Vector2 textSize = new Vector2(tmp.preferredWidth + 16f, labelFontSize + 10f);
        labelRect.sizeDelta = textSize;

        return new IndicatorEntry
        {
            target = target,
            arrowRect = arrowRect,
            arrowImage = arrowImage,
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

        Vector2 halfScreen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector3 camForward = playerCamera.transform.forward;

        for (int i = 0; i < entries.Count; i++)
        {
            IndicatorEntry e = entries[i];
            if (e.target == null)
            {
                // Target was destroyed — hide its UI and skip.
                SetAlpha(ref e, 0f);
                entries[i] = e;
                continue;
            }

            Vector3 worldPos = e.target.WorldPosition;
            Vector3 viewportPos = playerCamera.WorldToViewportPoint(worldPos);

            // Is the target in front of the camera?
            bool inFront = viewportPos.z > 0f;

            // Is it visible on screen?
            bool onScreen = inFront
                && viewportPos.x >= 0f && viewportPos.x <= 1f
                && viewportPos.y >= 0f && viewportPos.y <= 1f;

            if (onScreen)
            {
                // Hide the arrow; show label based on how directly we're facing the target.
                e.arrowRect.gameObject.SetActive(false);

                Vector3 dirToTarget = (worldPos - playerCamera.transform.position).normalized;
                float dot = Vector3.Dot(camForward, dirToTarget);

                float targetAlpha = Mathf.InverseLerp(facingThreshold, 1f, dot);
                e.currentAlpha = Mathf.MoveTowards(e.currentAlpha, targetAlpha, labelFadeSpeed * Time.deltaTime);

                // Position the label at the target's screen position.
                Vector2 screenPos = new Vector2(
                    (viewportPos.x - 0.5f) * canvasRect.sizeDelta.x,
                    (viewportPos.y - 0.5f) * canvasRect.sizeDelta.y);

                e.labelRect.anchoredPosition = screenPos + Vector2.up * (arrowSize * 0.5f + 4f);
            }
            else
            {
                // Off-screen: show the arrow clamped to the screen edge, hide the label.
                e.arrowRect.gameObject.SetActive(true);
                e.currentAlpha = Mathf.MoveTowards(e.currentAlpha, 0f, labelFadeSpeed * Time.deltaTime);

                // Convert viewport to a direction from screen centre, handle behind-camera case.
                Vector2 screenDir = new Vector2(viewportPos.x - 0.5f, viewportPos.y - 0.5f);
                if (!inFront) screenDir = -screenDir;

                if (screenDir == Vector2.zero) screenDir = Vector2.up;
                screenDir.Normalize();

                // Clamp to screen edge with padding.
                float halfW = halfScreen.x - edgePadding;
                float halfH = halfScreen.y - edgePadding;

                float scaleX = Mathf.Abs(screenDir.x) > 0.001f ? halfW / Mathf.Abs(screenDir.x) : float.MaxValue;
                float scaleY = Mathf.Abs(screenDir.y) > 0.001f ? halfH / Mathf.Abs(screenDir.y) : float.MaxValue;
                float scale = Mathf.Min(scaleX, scaleY);

                // anchoredPosition is in canvas space, not screen space — divide by canvas scale.
                float canvasScale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
                e.arrowRect.anchoredPosition = screenDir * scale / canvasScale;

                // Rotate arrow to point toward the target.
                float angle = Mathf.Atan2(screenDir.y, screenDir.x) * Mathf.Rad2Deg - 90f;
                e.arrowRect.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            // Apply alpha to both label text and optional background.
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
            if (e.labelRect != null) Destroy(e.labelRect.gameObject);
        }
    }
}
