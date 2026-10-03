using System.Collections;
using UnityEngine;

// =============================================================
// FadingOverlay.cs
// -------------------------------------------------------------
// Small reusable wrapper for a UI element that should fade in/out instead of instantly popping
// on/off (SetActive). Auto-adds a CanvasGroup if the GameObject doesn't already have one.
//
// Call SetVisible(bool) instead of gameObject.SetActive(bool) directly. The GameObject itself is
// only deactivated once the fade-out finishes (alpha reaches 0) — active (and interpolating
// alpha) throughout the fade itself, so it doesn't block raycasts or show stale content while
// actually invisible.
//
// SETUP: put this on the same GameObject as the Image (or its root, if it's a group of things
// that should fade together) — no other wiring needed, everything else is automatic.
// =============================================================
public class FadingOverlay : MonoBehaviour
{
    [Tooltip("Seconds to fade fully in or out.")]
    [SerializeField] private float fadeDuration = 0.4f;

    private CanvasGroup group;
    private Coroutine activeFade;
    private bool targetVisible;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();

        // Match whatever state this started in (Inspector) rather than assuming — if it was left
        // active in the scene, treat that as "already visible" instead of snapping it invisible.
        targetVisible = gameObject.activeSelf && group.alpha > 0.01f;
        group.alpha = targetVisible ? 1f : 0f;
        gameObject.SetActive(targetVisible);
    }

    public void SetVisible(bool visible)
    {
        if (visible == targetVisible) return;
        targetVisible = visible;

        if (activeFade != null)
            StopCoroutine(activeFade);

        if (visible)
            gameObject.SetActive(true); // must be active before the coroutine can run at all

        activeFade = StartCoroutine(FadeRoutine(visible));
    }

    private IEnumerator FadeRoutine(bool visible)
    {
        float start = group.alpha;
        float end = visible ? 1f : 0f;
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(start, end, Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }

        group.alpha = end;
        activeFade = null;

        if (!visible)
            gameObject.SetActive(false); // only deactivate AFTER it's fully faded out, not before
    }
}
