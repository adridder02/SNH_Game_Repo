/* using UnityEngine;
using UnityEngine.UI;

// =============================================================
// ImageFillBar.cs
// -------------------------------------------------------------
// A status bar built entirely from Images — no Slider. Matches the
// 5-piece painterly bar art (bg1 / bar_bg2 / water_fill3 /
// level_indicator5 / bar_outline4): the pixel alpha in each file
// confirms the stacking order below (checked bottom-to-top):
//
//   1. background (bg1)      — fully opaque painted frame/trough.
//                               Always shown at 100%, never resized.
//   2. track (bar_bg2)       — faint translucent tint over the
//                               whole bar, reads as "empty capacity".
//                               OPTIONAL — leave unassigned for bars
//                               that don't have this layer (e.g. a
//                               4-image bar with just bg/fill/dot/outline).
//   3. fill (water_fill3)    — opaque fill color. Image Type MUST be
//                               set to Filled, with Fill Method matching
//                               fillAxis below (Horizontal/Origin Left,
//                               or Vertical/Origin Bottom) in the
//                               Inspector — SetNormalized() just drives
//                               fillAmount, it doesn't set the Type for you.
//   4. indicator (level_indicator5) — thin glow line/dot marking exactly
//                               where the fill currently ends. Moved
//                               by anchor position, not fillAmount,
//                               since it's a marker, not a bar.
//   5. outline (bar_outline4) — mostly-transparent crisp border,
//                               drawn last so it reads on top of the
//                               fill at every level.
//
// Reused for every image-based bar in the game — water level, plant
// growth progress, etc. — make one instance of this hierarchy/component
// per bar. If a bar has a fixed colour (e.g. water), just tint the fill
// Image's color per-instance in the Inspector as before and leave
// useFillGradient off. If a bar's colour should change with its value
// (e.g. plant progress going red → yellow → green), turn on
// useFillGradient and set fillColorGradient instead.
//`
// SCENE SETUP — HORIZONTAL bar (e.g. water level):
//   Bar (empty parent, width = full bar width)
//    ├─ Background   Image, sprite = bg1
//    ├─ Track        Image, sprite = bar_bg2 (optional)
//    ├─ Fill         Image, sprite = water_fill3, Type = Filled,
//    │                Fill Method = Horizontal, Origin = Left
//    ├─ Indicator    Image, sprite = level_indicator5, anchored
//    │                left/right at the SAME x (a point, not stretched),
//    │                pivot (0.5, 0.5) so it centers on that point
//    └─ Outline      Image, sprite = bar_outline4
//   Set fillAxis = Horizontal.
//
// SCENE SETUP — VERTICAL bar (e.g. plant growth progress, dot moves
// up/down instead of a bar filling left/right):
//   Same layer stack, but:
//    ├─ Fill         Type = Filled, Fill Method = Vertical, Origin = Bottom
//    ├─ Indicator    anchored top/bottom at the SAME y (a point), pivot (0.5, 0.5)
//   Set fillAxis = Vertical.
//
//   All layers stretch-anchor to fill the parent except Indicator.
//   Assign the layers you have, then drive it with SetNormalized() or
//   SetValue().
// =============================================================
[DisallowMultipleComponent]
public class ImageFillBar : MonoBehaviour
{
    public enum FillAxis { Horizontal, Vertical }

    [Header("Layers (back to front — see class comment)")]
    [SerializeField] private Image background;
    [SerializeField] private Image track;
    [SerializeField] private Image fill;
    [SerializeField] private RectTransform indicator;
    [SerializeField] private Image outline;

    public Image mainBar;   // purple
    public Image trailBar;  // white
    public float trailSpeed = 2f;


    [Header("Orientation")]
    [Tooltip("Horizontal = fill grows left→right, indicator (dot) moves along X. Vertical = fill grows " +
             "bottom→top, indicator moves along Y. Must match the Fill Method set on the fill Image's " +
             "Inspector (Type=Filled).")]
    [SerializeField] private FillAxis fillAxis = FillAxis.Horizontal;

    [Tooltip("Hide the indicator line/dot when the bar is fully empty or fully full — it sits right on " +
             "top of the end cap at those extremes and looks like a rendering glitch otherwise.")]
    [SerializeField] private bool hideIndicatorAtExtremes = true;

    [Header("Colour Gradient (optional)")]
    [Tooltip("When on, the fill (and optionally the indicator) is tinted along fillColorGradient based on " +
             "the current normalized value, instead of keeping the fill's static Inspector colour. Use this " +
             "for bars whose colour should communicate the value — e.g. a growth bar going red at low " +
             "progress to green at full. Leave off for bars with a fixed colour, like plain water level.")]
    [SerializeField] private bool useFillGradient = false;

    [Tooltip("Evaluated at the current normalized value (0 = empty, 1 = full) when useFillGradient is on. " +
             "E.g. red @ 0, yellow @ 0.5, green @ 1.")]
    [SerializeField] private Gradient fillColorGradient;

    [Tooltip("Also tint the indicator dot/line with the same gradient colour, not just the fill.")]
    [SerializeField] private bool tintIndicatorWithGradient = false;

    [Header("Fill Behaviour")]
    [Tooltip("When on, the fill image is left fully visible at all times (fillAmount always 1, or simply not " +
             "a Filled-type Image at all) — only its colour and the indicator's position change with value. " +
             "Use this for bars where the whole capsule should always look 'full', like a solid-colour dot " +
             "gauge (e.g. plant growth) rather than a bar that visibly grows/shrinks like the water level.")]
    [SerializeField] private bool keepFillFullyVisible = false;

    [Header("Indicator Travel Range")]
    [Tooltip("Normalized position the indicator sits at when value = 0. Raise this above 0 if the dot pokes " +
             "out past the bar's rounded end-cap at the minimum — e.g. 0.05 keeps its center 5% of the bar's " +
             "length in from the left/bottom edge.")]
    [Range(0f, 0.49f)]
    [SerializeField] private float indicatorMinPosition = 0f;

    [Tooltip("Normalized position the indicator sits at when value = 1. Lower this below 1 if the dot pokes " +
             "out past the bar's rounded end-cap at the maximum — e.g. 0.95 keeps its center 5% in from the " +
             "right/top edge.")]
    [Range(0.51f, 1f)]
    [SerializeField] private float indicatorMaxPosition = 1f;

    public float CurrentNormalized { get; private set; }

    void Awake()
    {
        if (fill == null) return;
        if (keepFillFullyVisible) return; // fill isn't driven by fillAmount in this mode — Type doesn't matter.

        if (fill.type != Image.Type.Filled)
        {
            Debug.LogWarning($"[ImageFillBar] '{name}': the fill Image's Type isn't set to Filled. " +
                              $"Set Type=Filled, Fill Method={fillAxis} in the Inspector, " +
                              "or SetNormalized() won't visibly do anything.", fill);
            return;
        }

        Image.FillMethod expected = fillAxis == FillAxis.Horizontal ? Image.FillMethod.Horizontal : Image.FillMethod.Vertical;
        if (fill.fillMethod != expected)
            Debug.LogWarning($"[ImageFillBar] '{name}': fillAxis is {fillAxis} but the fill Image's Fill " +
                              $"Method is {fill.fillMethod}. Set Fill Method={expected} in the Inspector to match.", fill);
    }

  
    void Update()
    {
        if (trailBar.fillAmount > mainBar.fillAmount)
            trailBar.fillAmount = Mathf.Max(mainBar.fillAmount, 
                trailBar.fillAmount - trailSpeed * Time.deltaTime);
        else
            trailBar.fillAmount = mainBar.fillAmount;
    }

    /// <summary>Sets the bar to a normalized 0–1 amount.</summary>
    public void SetNormalized(float value01)
    {
        CurrentNormalized = Mathf.Clamp01(value01);

        Color? gradientColor = null;
        if (useFillGradient && fillColorGradient != null)
            gradientColor = fillColorGradient.Evaluate(CurrentNormalized);

        if (fill != null)
        {
            // keepFillFullyVisible bars aren't Filled-type images (or we simply don't touch fillAmount) —
            // the capsule always reads as "full"; only its colour changes.
            if (!keepFillFullyVisible)
                fill.fillAmount = CurrentNormalized;

            if (gradientColor.HasValue)
                fill.color = gradientColor.Value;
        }

        if (indicator != null)
        {
            // Move the indicator to the same fraction along the bar's active axis, remapped into
            // [indicatorMinPosition, indicatorMaxPosition] so its center doesn't overshoot past the
            // bar's rounded end-caps at 0 or 1.
            float indicatorPos = Mathf.Lerp(indicatorMinPosition, indicatorMaxPosition, CurrentNormalized);

            if (fillAxis == FillAxis.Horizontal)
            {
                indicator.anchorMin = new Vector2(indicatorPos, indicator.anchorMin.y);
                indicator.anchorMax = new Vector2(indicatorPos, indicator.anchorMax.y);
                indicator.anchoredPosition = new Vector2(0f, indicator.anchoredPosition.y);
            }
            else // Vertical
            {
                indicator.anchorMin = new Vector2(indicator.anchorMin.x, indicatorPos);
                indicator.anchorMax = new Vector2(indicator.anchorMax.x, indicatorPos);
                indicator.anchoredPosition = new Vector2(indicator.anchoredPosition.x, 0f);
            }

            if (hideIndicatorAtExtremes)
                indicator.gameObject.SetActive(CurrentNormalized > 0.001f && CurrentNormalized < 0.999f);

            if (gradientColor.HasValue && tintIndicatorWithGradient)
            {
                Image indicatorImage = indicator.GetComponent<Image>();
                if (indicatorImage != null)
                    indicatorImage.color = gradientColor.Value;
            }
        }
    }

    /// <summary>Convenience wrapper so callers don't all repeat the same current/max division.</summary>
    public void SetValue(float current, float max)
    {
        SetNormalized(max > 0f ? current / max : 0f);
    }
} */


using UnityEngine;
using UnityEngine.UI;

// =============================================================
// ImageFillBar.cs
// -------------------------------------------------------------
// A status bar built entirely from Images — no Slider. Matches the
// 5-piece painterly bar art (bg1 / bar_bg2 / water_fill3 /
// level_indicator5 / bar_outline4): the pixel alpha in each file
// confirms the stacking order below (checked bottom-to-top):
//
//   1. background (bg1)      — fully opaque painted frame/trough.
//                               Always shown at 100%, never resized.
//   2. track (bar_bg2)       — faint translucent tint over the
//                               whole bar, reads as "empty capacity".
//                               OPTIONAL — leave unassigned for bars
//                               that don't have this layer.
//   3. trailFill (white)     — OPTIONAL. Sits directly behind `fill`,
//                               same rect, same Filled/Fill Method
//                               settings. Eases toward `fill`'s value
//                               instead of snapping, so it visibly
//                               "trails" behind the main fill and
//                               shows the player how much just
//                               changed. Leave unassigned for bars
//                               that don't have this effect.
//   4. fill (water_fill3)    — opaque fill color. Image Type MUST be
//                               set to Filled, with Fill Method matching
//                               fillAxis below (Horizontal/Origin Left,
//                               or Vertical/Origin Bottom) in the
//                               Inspector — SetNormalized() just drives
//                               fillAmount, it doesn't set the Type for you.
//   5. indicator (level_indicator5) — thin glow line/dot marking exactly
//                               where the fill currently ends. Moved
//                               by anchor position, not fillAmount,
//                               since it's a marker, not a bar.
//   6. outline (bar_outline4) — mostly-transparent crisp border,
//                               drawn last so it reads on top of the
//                               fill at every level.
//
// IMPORTANT — trailFill and fill must share the exact same
// RectTransform anchors/size (stretch to fill the same parent rect).
// If their anchors don't match, the two bars will visually drift
// apart at runtime even though their fillAmount values agree — this
// is the most common cause of a "gap" between a main bar and its
// trailing indicator bar.
//
// SCENE SETUP — HORIZONTAL bar (e.g. water level):
//   Bar (empty parent, width = full bar width)
//    ├─ Background   Image, sprite = bg1
//    ├─ Track        Image, sprite = bar_bg2 (optional)
//    ├─ TrailFill    Image, sprite = water_fill3 (white/pale variant),
//    │                Type = Filled, Fill Method = Horizontal, Origin = Left
//    │                anchors STRETCHED to match Fill exactly (optional layer)
//    ├─ Fill         Image, sprite = water_fill3, Type = Filled,
//    │                Fill Method = Horizontal, Origin = Left
//    │                anchors STRETCHED to match TrailFill exactly
//    ├─ Indicator    Image, sprite = level_indicator5, anchored
//    │                left/right at the SAME x (a point, not stretched),
//    │                pivot (0.5, 0.5) so it centers on that point
//    └─ Outline      Image, sprite = bar_outline4
//   Set fillAxis = Horizontal.
//
// SCENE SETUP — VERTICAL bar (e.g. plant growth progress, dot moves
// up/down instead of a bar filling left/right):
//   Same layer stack, but:
//    ├─ TrailFill    Type = Filled, Fill Method = Vertical, Origin = Bottom
//    ├─ Fill         Type = Filled, Fill Method = Vertical, Origin = Bottom
//    ├─ Indicator    anchored top/bottom at the SAME y (a point), pivot (0.5, 0.5)
//   Set fillAxis = Vertical.
//
//   All layers stretch-anchor to fill the parent except Indicator.
//   Assign the layers you have, then drive it with SetNormalized() or
//   SetValue().
// =============================================================
[DisallowMultipleComponent]
public class ImageFillBar : MonoBehaviour
{
    public enum FillAxis { Horizontal, Vertical }

    [Header("Layers (back to front — see class comment)")]
    [SerializeField] private Image background;
    [SerializeField] private Image track;
    [SerializeField] private Image trailFill; // white "trailing" bar — optional
    [SerializeField] private Image fill;
    [SerializeField] private RectTransform indicator;
    [SerializeField] private Image outline;

    [Header("Trail Fill Behaviour")]
    [Tooltip("How fast (in normalized units/sec) the trailFill bar eases down to catch up with fill when " +
             "fill decreases. Only relevant if trailFill is assigned.")]
    [SerializeField] private float trailSpeed = 1f;

    [Header("Orientation")]
    [Tooltip("Horizontal = fill grows left→right, indicator (dot) moves along X. Vertical = fill grows " +
             "bottom→top, indicator moves along Y. Must match the Fill Method set on the fill Image's " +
             "Inspector (Type=Filled).")]
    [SerializeField] private FillAxis fillAxis = FillAxis.Horizontal;

    [Tooltip("Hide the indicator line/dot when the bar is fully empty or fully full — it sits right on " +
             "top of the end cap at those extremes and looks like a rendering glitch otherwise.")]
    [SerializeField] private bool hideIndicatorAtExtremes = true;

    [Header("Colour Gradient (optional)")]
    [Tooltip("When on, the fill (and optionally the indicator) is tinted along fillColorGradient based on " +
             "the current normalized value, instead of keeping the fill's static Inspector colour. Use this " +
             "for bars whose colour should communicate the value — e.g. a growth bar going red at low " +
             "progress to green at full. Leave off for bars with a fixed colour, like plain water level.")]
    [SerializeField] private bool useFillGradient = false;

    [Tooltip("Evaluated at the current normalized value (0 = empty, 1 = full) when useFillGradient is on. " +
             "E.g. red @ 0, yellow @ 0.5, green @ 1.")]
    [SerializeField] private Gradient fillColorGradient;

    [Tooltip("Also tint the indicator dot/line with the same gradient colour, not just the fill.")]
    [SerializeField] private bool tintIndicatorWithGradient = false;

    [Header("Fill Behaviour")]
    [Tooltip("When on, the fill image is left fully visible at all times (fillAmount always 1, or simply not " +
             "a Filled-type Image at all) — only its colour and the indicator's position change with value. " +
             "Use this for bars where the whole capsule should always look 'full', like a solid-colour dot " +
             "gauge (e.g. plant growth) rather than a bar that visibly grows/shrinks like the water level.")]
    [SerializeField] private bool keepFillFullyVisible = false;

    [Header("Indicator Travel Range")]
    [Tooltip("Normalized position the indicator sits at when value = 0. Raise this above 0 if the dot pokes " +
             "out past the bar's rounded end-cap at the minimum — e.g. 0.05 keeps its center 5% of the bar's " +
             "length in from the left/bottom edge.")]
    [Range(0f, 0.49f)]
    [SerializeField] private float indicatorMinPosition = 0f;

    [Tooltip("Normalized position the indicator sits at when value = 1. Lower this below 1 if the dot pokes " +
             "out past the bar's rounded end-cap at the maximum — e.g. 0.95 keeps its center 5% in from the " +
             "right/top edge.")]
    [Range(0.51f, 1f)]
    [SerializeField] private float indicatorMaxPosition = 1f;

    public float CurrentNormalized { get; private set; }

    void Awake()
    {
        if (fill == null) return;
        if (keepFillFullyVisible) return; // fill isn't driven by fillAmount in this mode — Type doesn't matter.

        if (fill.type != Image.Type.Filled)
        {
            Debug.LogWarning($"[ImageFillBar] '{name}': the fill Image's Type isn't set to Filled. " +
                              $"Set Type=Filled, Fill Method={fillAxis} in the Inspector, " +
                              "or SetNormalized() won't visibly do anything.", fill);
            return;
        }

        Image.FillMethod expected = fillAxis == FillAxis.Horizontal ? Image.FillMethod.Horizontal : Image.FillMethod.Vertical;
        if (fill.fillMethod != expected)
            Debug.LogWarning($"[ImageFillBar] '{name}': fillAxis is {fillAxis} but the fill Image's Fill " +
                              $"Method is {fill.fillMethod}. Set Fill Method={expected} in the Inspector to match.", fill);

        if (trailFill != null)
        {
            if (trailFill.type != Image.Type.Filled)
                Debug.LogWarning($"[ImageFillBar] '{name}': trailFill's Type isn't set to Filled.", trailFill);

            RectTransform fillRect = fill.rectTransform;
            RectTransform trailRect = trailFill.rectTransform;
            if (fillRect.anchorMin != trailRect.anchorMin || fillRect.anchorMax != trailRect.anchorMax)
                Debug.LogWarning($"[ImageFillBar] '{name}': trailFill's anchors don't match fill's anchors. " +
                                  "They must be identical (both stretched to the same rect) or the two bars " +
                                  "will visually drift apart at runtime.", trailFill);

            // Start in sync so there's no spurious ease-in on the very first frame.
            trailFill.fillAmount = fill.fillAmount;
        }
    }

    void Update()
    {
        if (trailFill == null || fill == null) return;

        // trailFill only eases DOWN toward fill (e.g. after taking damage). If fill increases,
        // snap trailFill up immediately so it never sits ahead of a rising bar.
        if (trailFill.fillAmount > fill.fillAmount)
            trailFill.fillAmount = Mathf.Max(fill.fillAmount, trailFill.fillAmount - trailSpeed * Time.deltaTime);
        else
            trailFill.fillAmount = fill.fillAmount;
    }

    /// <summary>Sets the bar to a normalized 0–1 amount.</summary>
    public void SetNormalized(float value01)
    {
        CurrentNormalized = Mathf.Clamp01(value01);

        Color? gradientColor = null;
        if (useFillGradient && fillColorGradient != null)
            gradientColor = fillColorGradient.Evaluate(CurrentNormalized);

        if (fill != null)
        {
            // keepFillFullyVisible bars aren't Filled-type images (or we simply don't touch fillAmount) —
            // the capsule always reads as "full"; only its colour changes.
            if (!keepFillFullyVisible)
                fill.fillAmount = CurrentNormalized;

            if (gradientColor.HasValue)
                fill.color = gradientColor.Value;
        }
        // Note: trailFill is NOT set here directly — Update() eases it toward fill.fillAmount
        // every frame so it visibly trails instead of snapping instantly.

        if (indicator != null)
        {
            // Move the indicator to the same fraction along the bar's active axis, remapped into
            // [indicatorMinPosition, indicatorMaxPosition] so its center doesn't overshoot past the
            // bar's rounded end-caps at 0 or 1.
            float indicatorPos = Mathf.Lerp(indicatorMinPosition, indicatorMaxPosition, CurrentNormalized);

            if (fillAxis == FillAxis.Horizontal)
            {
                indicator.anchorMin = new Vector2(indicatorPos, indicator.anchorMin.y);
                indicator.anchorMax = new Vector2(indicatorPos, indicator.anchorMax.y);
                indicator.anchoredPosition = new Vector2(0f, indicator.anchoredPosition.y);
            }
            else // Vertical
            {
                indicator.anchorMin = new Vector2(indicator.anchorMin.x, indicatorPos);
                indicator.anchorMax = new Vector2(indicator.anchorMax.x, indicatorPos);
                indicator.anchoredPosition = new Vector2(indicator.anchoredPosition.x, 0f);
            }

            if (hideIndicatorAtExtremes)
                indicator.gameObject.SetActive(CurrentNormalized > 0.001f && CurrentNormalized < 0.999f);

            if (gradientColor.HasValue && tintIndicatorWithGradient)
            {
                Image indicatorImage = indicator.GetComponent<Image>();
                if (indicatorImage != null)
                    indicatorImage.color = gradientColor.Value;
            }
        }
    }

    /// <summary>Convenience wrapper so callers don't all repeat the same current/max division.</summary>
    public void SetValue(float current, float max)
    {
        SetNormalized(max > 0f ? current / max : 0f);
    }
}