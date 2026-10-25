using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

// =============================================================
// AudioMixerRouter.cs
// -------------------------------------------------------------
// Auto-boots (nothing to add to a scene). Finds every AudioSource whose Output is EMPTY — including
// ones created in code (PlacementSystem's sfx/ambient sources, PlayerWaterSource's loop) — and routes
// it to a mixer group so the Music / SFX / Ambience sliders work on it:
//
//   Music     name or clip name contains music/song/theme/bgm, OR a looping clip of 30s or more
//   Ambience  name/clip contains ambient/ambience/wind/rain/birds/drip/forest/hum/breeze,
//             OR any other looping clip
//   Sfx       everything else, and anything on the player (footsteps, landing, water refill loop)
//
// A source whose Output you already assigned by hand is never touched. Needs the mixer from
// Resources/MainAudioMixer with groups NAMED exactly: Music, Sfx, Ambience (children of Master).
// Missing mixer/group = quietly does nothing for that source.
// =============================================================
public class AudioMixerRouter : MonoBehaviour
{
    private const string MixerResource = "MainAudioMixer";
    private const float ScanInterval = 1.5f;
    private const float MusicMinLength = 30f;

    private static readonly string[] MusicWords = { "music", "song", "theme", "bgm", "score" };
    private static readonly string[] AmbientWords = { "ambient", "ambience", "wind", "rain", "bird", "drip", "forest", "hum", "breeze", "crickets" };

    private AudioMixerGroup music, sfx, ambience, master;
    private bool ready;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("AudioMixerRouter");
        DontDestroyOnLoad(go);
        go.AddComponent<AudioMixerRouter>();
    }

    private void Awake()
    {
        AudioMixer mixer = Resources.Load<AudioMixer>(MixerResource);
        if (mixer == null) return;

        music = Group(mixer, "Music");
        sfx = Group(mixer, "Sfx");
        ambience = Group(mixer, "Ambience");
        master = Group(mixer, "Master");
        ready = true;

        SceneManager.sceneLoaded += (s, m) => StartCoroutine(RouteSoon());
        StartCoroutine(ScanLoop());
    }

    private static AudioMixerGroup Group(AudioMixer mixer, string name)
    {
        AudioMixerGroup[] found = mixer.FindMatchingGroups(name);
        foreach (AudioMixerGroup g in found) if (g.name == name) return g;
        return null;
    }

    private IEnumerator RouteSoon()
    {
        yield return null;   // let Awake/Start create their sources first
        yield return null;
        RouteAll();
    }

    private IEnumerator ScanLoop()
    {
        var wait = new WaitForSecondsRealtime(ScanInterval);
        while (true)
        {
            RouteAll();
            yield return wait;
        }
    }

    private void RouteAll()
    {
        if (!ready) return;
        foreach (AudioSource src in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (src == null || src.outputAudioMixerGroup != null) continue;
            AudioMixerGroup g = Pick(src);
            if (g != null) src.outputAudioMixerGroup = g;
        }
    }

    private AudioMixerGroup Pick(AudioSource src)
    {
        if (src.GetComponentInParent<PlayerController>() != null) return sfx ?? master;

        string n = (src.gameObject.name + " " + (src.clip != null ? src.clip.name : "")).ToLowerInvariant();
        if (Has(n, MusicWords)) return music ?? master;
        if (Has(n, AmbientWords)) return ambience ?? master;

        if (src.loop)
        {
            if (src.clip != null && src.clip.length >= MusicMinLength) return music ?? master;
            return ambience ?? master;
        }
        return sfx ?? master;
    }

    private static bool Has(string s, string[] words)
    {
        foreach (string w in words) if (s.Contains(w)) return true;
        return false;
    }
}
