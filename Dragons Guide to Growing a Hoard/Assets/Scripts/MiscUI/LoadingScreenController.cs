using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// =============================================================
// LoadingScreenController.cs
// -------------------------------------------------------------
// Put this on the ROOT Canvas GameObject that holds your loading-screen UI
// (the teacups image + the "loading..." Animator you just built). Drag that
// Canvas into a small empty bootstrap scene (or just into your very first
// scene) so it exists once at startup - Awake() marks it DontDestroyOnLoad
// and singleton-guards against a duplicate ever creeping into a later scene.
//
// Anywhere else in the project that wants to change scenes calls:
//     LoadingScreenController.Instance.LoadScene("YourSceneName");
// instead of SceneManager.LoadScene/LoadSceneAsync directly - that's the
// only wiring change needed elsewhere.
//
// The loading UI (canvasRoot) starts DISABLED - it only shows itself while
// an actual load is in progress, then hides again automatically.
// =============================================================
public class LoadingScreenController : MonoBehaviour
{
    public static LoadingScreenController Instance { get; private set; }

    [Header("References")]
    [Tooltip("The root GameObject of your loading UI (teacups + loading text). " +
             "Usually this same GameObject, or a child of it - whatever should be hidden " +
             "except while actually loading.")]
    [SerializeField] private GameObject canvasRoot;

    [Header("Timing")]
    [Tooltip("Loading screen stays up at least this long even if the scene loads instantly, " +
             "so the animation actually gets seen rather than flashing for one frame.")]
    [SerializeField] private float minimumDisplayTime = 1.5f;

    private bool isLoading = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Duplicate landed in a newly-loaded scene - this one already persists from
            // wherever it was first created, so the new copy is redundant.
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    /// <summary>Call this instead of SceneManager.LoadScene/LoadSceneAsync from anywhere in the
    /// project. Shows the loading UI, loads the target scene in the background, and hides the UI
    /// again once it's ready (respecting minimumDisplayTime so it's never just a single-frame flash).</summary>
    public void LoadScene(string sceneName)
    {
        if (isLoading)
        {
            Debug.LogWarning($"[LoadingScreenController] Already loading a scene - ignoring request for '{sceneName}'.");
            return;
        }

        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        isLoading = true;

        if (canvasRoot != null)
            canvasRoot.SetActive(true); // this is what kicks the Animator/Loading clip into playing

        float startTime = Time.unscaledTime;

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        // Hold at 90% (Unity's async load caps progress there until activation) rather than
        // letting the new scene pop in the instant it's ready - we want minimumDisplayTime to
        // apply regardless of how fast the load itself was.
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
            yield return null;

        // Now technically ready - wait out whatever's left of minimumDisplayTime before
        // actually switching, so the loading animation always gets at least that long on screen.
        float elapsed = Time.unscaledTime - startTime;
        float remaining = minimumDisplayTime - elapsed;
        if (remaining > 0f)
            yield return new WaitForSecondsRealtime(remaining);

        op.allowSceneActivation = true;

        // One more frame so the new scene's Awake/Start calls have actually run before we hide
        // the loading UI and hand control back.
        yield return null;

        if (canvasRoot != null)
            canvasRoot.SetActive(false);

        isLoading = false;
    }
}
