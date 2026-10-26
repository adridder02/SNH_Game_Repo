using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =============================================================
// UiSoundAutoWire.cs
// -------------------------------------------------------------
// Boots itself - nothing to add to a scene. Every couple of seconds (and on each scene load) it finds all
// Buttons and Toggles that don't have a click sound yet and hooks one up, picked from the object's name:
//     contains "bookmark"            -> Open Bookmarks
//     contains "next"/"prev"/"flip"  -> Flip Page
//     contains "tab" (not "table")   -> Switch Tab          (Toggles always use Switch Tab)
//     anything else                  -> Menu Open/Close click
// The sound waits until the end of the frame and is skipped if something more specific already played, so
// menus that make their own sound (journal, inventory slots...) don't double up.
// =============================================================
public class UiSoundAutoWire : MonoBehaviour
{
    private readonly System.Collections.Generic.HashSet<int> hooked = new System.Collections.Generic.HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (FindAnyObjectByType<UiSoundAutoWire>() != null) return;
        var go = new GameObject("UiSoundAutoWire");
        DontDestroyOnLoad(go);
        go.AddComponent<UiSoundAutoWire>();
    }

    private float timer;

    void OnEnable() { SceneManager.sceneLoaded += OnLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnLoaded; }
    private void OnLoaded(Scene s, LoadSceneMode m) { timer = 0.3f; }

    void Update()
    {
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f) return;
        timer = 2f;
        Scan();
    }

    private void Scan()
    {
        foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            if (!hooked.Add(b.GetInstanceID())) continue;
            SoundId id = Classify(b.transform);
            Button captured = b;
            b.onClick.AddListener(() => { if (captured.interactable) StartCoroutine(Deferred(id)); });
        }

        foreach (Toggle t in FindObjectsByType<Toggle>(FindObjectsInactive.Include))
        {
            if (!hooked.Add(t.GetInstanceID())) continue;
            t.onValueChanged.AddListener(_ => StartCoroutine(Deferred(SoundId.SwitchTab)));
        }
    }

    private static SoundId Classify(Transform t)
    {
        string n = (t.name + " " + (t.parent != null ? t.parent.name : "")).ToLowerInvariant();
        if (n.Contains("bookmark")) return SoundId.OpenBookmarks;
        if (n.Contains("next") || n.Contains("prev") || n.Contains("flip")) return SoundId.FlipPage;
        if (n.Contains("tab") && !n.Contains("table")) return SoundId.SwitchTab;
        return SoundId.MenuOpenClose;
    }

    private IEnumerator Deferred(SoundId id)
    {
        float before = GameAudio.LastPlayTime;
        yield return new WaitForEndOfFrame();
        // Something else already made a UI sound for this click? Then stay quiet.
        if (GameAudio.LastPlayTime > before + 0.0001f) yield break;
        GameAudio.Play(id);
    }
}
