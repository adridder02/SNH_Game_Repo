using UnityEngine;
using UnityEngine.UI;

// =============================================================
// UIScaleApplier.cs
// -------------------------------------------------------------
// Put on a Canvas that has a CanvasScaler to make it follow the "UI Scale" setting. Works with both
// "Scale With Screen Size" (shrinks the reference resolution so everything gets bigger) and
// "Constant Pixel Size" (multiplies the scale factor).
// =============================================================
[RequireComponent(typeof(CanvasScaler))]
public class UIScaleApplier : MonoBehaviour
{
    private CanvasScaler scaler;
    private Vector2 baseReference;
    private float baseFactor;

    private void Awake()
    {
        scaler = GetComponent<CanvasScaler>();
        baseReference = scaler.referenceResolution;
        baseFactor = scaler.scaleFactor;
    }

    private void OnEnable()
    {
        SettingsManager.Changed += Apply;
        Apply();
    }

    private void OnDisable() => SettingsManager.Changed -= Apply;

    private void Apply()
    {
        if (scaler == null) return;
        float s = Mathf.Max(0.5f, SettingsManager.UIScale);

        if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            scaler.referenceResolution = baseReference / s;
        else if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
            scaler.scaleFactor = baseFactor * s;
    }
}
