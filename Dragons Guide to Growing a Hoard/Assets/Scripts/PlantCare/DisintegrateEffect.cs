using System;
using System.Collections;
using UnityEngine;

// =============================================================
// DisintegrateEffect.cs
// -------------------------------------------------------------
// Attach this to a pickup-able object (a harvest node, or the plant
// GameObject inside a pot) whose renderer(s) use the dissolve Shader
// Graph material. Calling Play() animates the material's Dissolve
// Amount float from 0 (fully visible) to 1 (fully dissolved) over
// `duration` seconds, then invokes the optional callback — callers use
// that callback to do the actual Destroy()/SetActive(false) AFTER the
// effect has finished, instead of doing it instantly.
//
// IMPORTANT — VERIFY dissolveAmountProperty BEFORE RELYING ON THIS:
// It has to exactly match the shader property's REFERENCE name
// (Blackboard panel → click "Dissolve Amount" → Graph Inspector →
// Reference field), NOT the display name "Dissolve Amount" shown in the
// graph. "_DissolveAmount" below is Unity's typical auto-generated
// reference for a Vector1 named that, but it can be renamed by hand in
// the Blackboard, so double-check it actually matches. If it doesn't,
// this fails silently — no error, the material just never visibly
// changes — same class of mistake as the fog emission mismatch earlier
// in this project, so it's worth the 10 seconds to confirm.
//
// Uses a MaterialPropertyBlock per renderer rather than touching
// renderer.material directly, so it doesn't create per-instance material
// copies or break GPU instancing/batching — same approach used for the
// fog's emission color in MiasmaController.
// =============================================================
[DisallowMultipleComponent]
public class DisintegrateEffect : MonoBehaviour
{
    [Tooltip("Must match the Shader Graph property's REFERENCE name (Blackboard → Graph Inspector → " +
             "Reference), not its display name 'Dissolve Amount'. Verify this before relying on it — " +
             "see the class comment above.")]
    public string dissolveAmountProperty = "_DissolveAmount";

    [Tooltip("How long the dissolve takes to go from fully visible to fully gone.")]
    public float duration = 1f;

    [Tooltip("On: animates the property 0 → 1 (0 = fully visible, 1 = fully dissolved — the standard " +
             "convention for this kind of shader, and what this graph's Alpha Clip Threshold wiring " +
             "implies). Off: animates 1 → 0 instead. Flip this if Play mode shows the object doing the " +
             "opposite of what you expect.")]
    public bool zeroToOne = true;

    [Header("Base Texture (optional)")]
    [Tooltip("Leave empty to skip this entirely (old behavior — this component only touches the " +
             "Dissolve Amount property on whatever material is already assigned). Assign your dissolve " +
             "shader's material here to enable auto-texturing: at Play() time, each renderer's CURRENT " +
             "texture (.mainTexture — whatever it's already showing for normal gameplay) is captured, " +
             "the renderer is swapped to THIS shared material, and the captured texture is pushed into " +
             "baseTextureProperty below. One material, no per-plant setup — whatever a plant already " +
             "looks like is automatically what dissolves.")]
    public Material dissolveMaterial;

    [Tooltip("Must match your dissolve shader's Base Color texture property REFERENCE name (Blackboard " +
             "→ Graph Inspector → Reference), same verification caveat as dissolveAmountProperty. Only " +
             "used if dissolveMaterial above is assigned.")]
    public string baseTextureProperty = "_OriginalTexture";

    [Tooltip("Renderers to animate. Auto-populated from this object and its children on Awake if left " +
             "empty — only fill this in by hand if you need to exclude specific renderers (e.g. a UI " +
             "element that happens to be a child of the plant).")]
    public Renderer[] targetRenderers;

    private MaterialPropertyBlock propBlock;
    private Coroutine playingCoroutine;

    /// <summary>True from the moment Play() starts until its effect finishes (or a new Play() call
    /// restarts it) — use this to stop something from being re-interacted-with while it's mid-dissolve.</summary>
    public bool IsPlaying => playingCoroutine != null;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>();
    }

    /// <summary>Starts the dissolve. Safe to call more than once — a second call restarts it from the
    /// beginning rather than stacking coroutines. onComplete fires exactly once, after `duration`
    /// seconds, even if no renderers were found — callers can always rely on it to know when it's
    /// safe to Destroy/deactivate the object.</summary>
    public void Play(Action onComplete = null)
    {
        if (!gameObject.activeInHierarchy)
        {
            // Can't run a coroutine on an inactive object — just skip straight to the callback
            // rather than silently doing nothing, so callers' Destroy()/SetActive() still happens.
            onComplete?.Invoke();
            return;
        }

        CaptureAndApplyBaseTexture();

        if (playingCoroutine != null)
            StopCoroutine(playingCoroutine);

        playingCoroutine = StartCoroutine(PlayRoutine(onComplete));
    }

    // Runs once, right before the animation starts — the texture doesn't change over the
    // course of the dissolve, only Dissolve Amount does, so there's no need to redo this every
    // frame in ApplyValue() below.
    private void CaptureAndApplyBaseTexture()
    {
        if (dissolveMaterial == null || targetRenderers == null) return;

        foreach (Renderer r in targetRenderers)
        {
            if (r == null) continue;

            // Grab whatever texture this renderer is ALREADY using for its normal, everyday
            // material — .mainTexture is a generic Unity shorthand that resolves to whatever a
            // shader's primary texture is, so this works without knowing that original
            // material's exact property name.
            Texture originalTexture = r.sharedMaterial != null ? r.sharedMaterial.mainTexture : null;

            // Swap to the shared dissolve material. This assigns sharedMaterial (not .material),
            // so it does NOT create a per-instance material copy — the texture override below
            // lives entirely in the property block, same as Dissolve Amount.
            r.sharedMaterial = dissolveMaterial;

            if (originalTexture != null)
            {
                r.GetPropertyBlock(propBlock);
                propBlock.SetTexture(baseTextureProperty, originalTexture);
                r.SetPropertyBlock(propBlock);
            }
        }
    }

    private IEnumerator PlayRoutine(Action onComplete)
    {
        float start = zeroToOne ? 0f : 1f;
        float end = zeroToOne ? 1f : 0f;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float value = Mathf.Lerp(start, end, Mathf.Clamp01(t / duration));
            ApplyValue(value);
            yield return null;
        }

        ApplyValue(end);
        playingCoroutine = null;
        onComplete?.Invoke();
    }

    private void ApplyValue(float value)
    {
        if (targetRenderers == null) return;

        foreach (Renderer r in targetRenderers)
        {
            if (r == null) continue;

            r.GetPropertyBlock(propBlock);
            propBlock.SetFloat(dissolveAmountProperty, value);
            r.SetPropertyBlock(propBlock);
        }
    }
}