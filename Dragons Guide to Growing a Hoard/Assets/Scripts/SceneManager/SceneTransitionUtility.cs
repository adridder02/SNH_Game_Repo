using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// =============================================================
// SceneTransitionUtility.cs
// -------------------------------------------------------------
// Small shared helper so new scene-load call sites (TitleScreenController,
// TutorialAdvanceTrigger) don't each need their own copy of "play a fade
// Animator, wait, then load" — same pattern LevelLoader.cs already uses for
// its own mission-complete auto-advance flow. LevelLoader is left as-is
// (different, existing trigger condition); this is for new call sites that
// load a scene in response to something else, like a button or a trigger
// volume.
//
// The actual scene load now routes through LoadingScreenController (the
// teacup "loading..." animation) instead of calling SceneManager.LoadScene
// directly, so every existing call site (TitleScreenController's New Game
// button, TutorialAdvanceTrigger's tutorial-exit prompt) picks up the
// loading screen automatically with no changes needed on their end — they
// already call SceneTransitionUtility.LoadScene(...), which is the only
// thing that changed here. Falls back to a plain instant SceneManager.
// LoadScene if no LoadingScreenController exists yet (e.g. testing a scene
// standalone without the persistent loading-screen Canvas in it).
// =============================================================
public static class SceneTransitionUtility
{
    /// <summary>Loads a scene by NAME (not build index — avoids breaking if scenes get reordered/
    /// added in Build Settings, which is exactly what happened to LevelLoader's old buildIndex + 1
    /// math once a 3rd scene was added). Optionally plays transitionAnimator's "Start" trigger and
    /// waits transitionTime first (e.g. a quick fade-to-black) before handing off to the loading
    /// screen; pass transitionAnimator = null to skip straight to the loading screen with no fade.
    /// `runner` just needs to be any active MonoBehaviour to host the wait coroutine on.</summary>
    public static void LoadScene(MonoBehaviour runner, string sceneName, Animator transitionAnimator, float transitionTime)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[SceneTransitionUtility] No scene name set — nothing to load.");
            return;
        }

        if (transitionAnimator != null)
            runner.StartCoroutine(LoadWithTransition(sceneName, transitionAnimator, transitionTime));
        else
            LoadNow(sceneName);
    }

    private static IEnumerator LoadWithTransition(string sceneName, Animator transitionAnimator, float transitionTime)
    {
        transitionAnimator.SetTrigger("Start");
        yield return new WaitForSeconds(transitionTime);
        LoadNow(sceneName);
    }

    /// <summary>The actual load, after any optional fade has already played. Prefers
    /// LoadingScreenController (shows the teacup animation, loads async, respects its
    /// minimumDisplayTime) and only falls back to a raw instant load if that Instance
    /// doesn't exist in the current scene setup.</summary>
    private static void LoadNow(string sceneName)
    {
        if (LoadingScreenController.Instance != null)
            LoadingScreenController.Instance.LoadScene(sceneName);
        else
        {
            Debug.LogWarning("[SceneTransitionUtility] No LoadingScreenController.Instance found — " +
                              "loading instantly with no loading screen.");
            SceneManager.LoadScene(sceneName);
        }
    }
}