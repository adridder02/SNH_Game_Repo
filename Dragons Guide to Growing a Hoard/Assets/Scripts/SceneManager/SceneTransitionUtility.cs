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
// =============================================================
public static class SceneTransitionUtility
{
    /// <summary>Loads a scene by NAME (not build index — avoids breaking if scenes get reordered/
    /// added in Build Settings, which is exactly what happened to LevelLoader's old buildIndex + 1
    /// math once a 3rd scene was added). Optionally plays transitionAnimator's "Start" trigger and
    /// waits transitionTime first; pass transitionAnimator = null to load instantly with no fade.
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
            SceneManager.LoadScene(sceneName);
    }

    private static IEnumerator LoadWithTransition(string sceneName, Animator transitionAnimator, float transitionTime)
    {
        transitionAnimator.SetTrigger("Start");
        yield return new WaitForSeconds(transitionTime);
        SceneManager.LoadScene(sceneName);
    }
}
