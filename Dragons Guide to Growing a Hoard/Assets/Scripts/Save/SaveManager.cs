using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// =============================================================
// SaveManager.cs
// -------------------------------------------------------------
// Auto-boots (no scene setup needed) and survives scene loads.
//
// ONE save slot: <persistentDataPath>/neglect_save.json (+ .bak of the previous save).
//   • Continue      -> loads the saved scene, then restores the world into it.
//   • New Game      -> deletes the save + the progress PlayerPrefs, loads the tutorial scene.
//   • Save / autosave only ever happen in the MAIN scene (SaveRegistry.mainSceneName).
//
// AUTOSAVE (all respect the Autosave toggle in Settings; the Save button always saves):
//   entering the main scene, closing the journal, quit-to-title, quitting the app,
//   and every N minutes (Settings -> Autosave interval).
// =============================================================
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    public static event Action<string> Saved;     // reason
    public static event Action Restored;          // fired after a Continue finished restoring the world
    public static event Action SaveStateChanged;  // save created / deleted (UI can refresh Continue buttons)

    public static bool IsRestoring { get; private set; }

    private SaveFile pending;       // set by Continue(), consumed when its scene finishes loading
    private float nextTimer;

    private static string FilePath => Path.Combine(Application.persistentDataPath, "neglect_save.json");
    private static string BackupPath => FilePath + ".bak";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (Instance != null) return;
        var go = new GameObject("SaveManager");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<SaveManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ResetTimer();
    }

    private void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ------------------------------------------------------------
    // QUERIES
    // ------------------------------------------------------------
    public static bool HasSave => File.Exists(FilePath);

    private static SaveRegistry Registry => SaveRegistry.Instance;

    public static bool InMainScene
        => Registry != null && SceneManager.GetActiveScene().name == Registry.mainSceneName;

    /// <summary>Human-readable "Saved 25 Oct 2026, 14:03" for the UI, or "" when there's no save.</summary>
    public static string DescribeSave()
    {
        SaveFile f = Read();
        if (f == null) return "";
        if (DateTime.TryParse(f.savedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime t))
            return "Saved " + t.ToLocalTime().ToString("d MMM yyyy, HH:mm");
        return "Saved game";
    }

    // ------------------------------------------------------------
    // SAVE
    // ------------------------------------------------------------
    /// <summary>Manual / explicit save. Returns false if there's nothing to save right now (not in the main scene).</summary>
    public static bool Save(string reason = "manual")
    {
        if (Registry == null)
        {
            Debug.LogWarning("[SaveManager] No SaveRegistry found at Resources/SaveRegistry — create one (Create > Neglect > Save Registry).");
            return false;
        }
        if (IsRestoring || !InMainScene) return false;

        try
        {
            SaveFile file = SaveWorld.Capture(reason);
            string json = JsonUtility.ToJson(file, true);

            Directory.CreateDirectory(Application.persistentDataPath);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(FilePath)) File.Copy(FilePath, BackupPath, true);
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(tmp, FilePath);

            Debug.Log($"[SaveManager] Saved ({reason}) -> {FilePath}");
            Saved?.Invoke(reason);
            SaveStateChanged?.Invoke();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveManager] Save failed: " + e);
            return false;
        }
    }

    /// <summary>Autosave: same as Save, but silently skipped when the player turned autosave off.</summary>
    public static void Autosave(string reason)
    {
        if (!SettingsManager.AutosaveEnabled) return;
        Save("autosave:" + reason);
        if (Instance != null) Instance.ResetTimer();
    }

    private void ResetTimer()
    {
        nextTimer = Time.unscaledTime + Mathf.Max(1, SettingsManager.AutosaveMinutes) * 60f;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextTimer) return;
        ResetTimer();
        if (InMainScene && !IsRestoring && SettingsManager.AutosaveEnabled)
            Save("autosave:timer");
    }

    private void OnApplicationQuit()
    {
        try { if (InMainScene && !IsRestoring && SettingsManager.AutosaveEnabled) Save("autosave:quit"); }
        catch { /* objects may already be tearing down */ }
    }

    // ------------------------------------------------------------
    // LOAD / NEW GAME
    // ------------------------------------------------------------
    private static SaveFile Read()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            return JsonUtility.FromJson<SaveFile>(File.ReadAllText(FilePath));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveManager] Couldn't read save: " + e.Message);
            return null;
        }
    }

    /// <summary>Continue / Load: loads the saved scene and restores the world into it. `runner` hosts the
    /// transition coroutine (any active MonoBehaviour); animator/time are the optional fade.</summary>
    public static bool Continue(MonoBehaviour runner, Animator transitionAnimator = null, float transitionTime = 0f)
    {
        SaveFile file = Read();
        if (file == null || Instance == null || string.IsNullOrEmpty(file.scene)) return false;

        SaveWorld.ApplyPrefs(file);
        Instance.pending = file;
        SceneTransitionUtility.LoadScene(runner != null ? runner : Instance, file.scene, transitionAnimator, transitionTime);
        return true;
    }

    /// <summary>Wipes the save and all progress state, then loads the tutorial scene.</summary>
    public static void NewGame(MonoBehaviour runner, Animator transitionAnimator = null, float transitionTime = 0f)
    {
        WipeEverything();
        if (Registry == null) { Debug.LogWarning("[SaveManager] No SaveRegistry — can't find the tutorial scene name."); return; }
        SceneTransitionUtility.LoadScene(runner != null ? runner : Instance, Registry.tutorialSceneName, transitionAnimator, transitionTime);
    }

    /// <summary>Autosaves, then returns to the title screen.</summary>
    public static void QuitToTitle(MonoBehaviour runner)
    {
        Autosave("quit-to-title");
        if (Instance != null) Instance.pending = null;
        Time.timeScale = 1f;
        if (Registry != null)
            SceneTransitionUtility.LoadScene(runner != null ? runner : Instance, Registry.titleSceneName, null, 0f);
    }

    public static void DeleteSave()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            if (File.Exists(BackupPath)) File.Delete(BackupPath);
        }
        catch (Exception e) { Debug.LogWarning("[SaveManager] Delete failed: " + e.Message); }
        SaveStateChanged?.Invoke();
    }

    private static void WipeEverything()
    {
        // Keys remembered in the old save (tutorial flags discovered at the time) + registry keys + built-ins.
        SaveFile old = Read();
        if (old != null)
            foreach (string k in old.knownPrefKeys) PlayerPrefs.DeleteKey(k);

        SaveRegistry r = Registry;
        if (r != null)
        {
            foreach (string k in r.intPrefKeys) PlayerPrefs.DeleteKey(k);
            foreach (string k in r.stringPrefKeys) PlayerPrefs.DeleteKey(k);
            foreach (string k in r.floatPrefKeys) PlayerPrefs.DeleteKey(k);
        }
        foreach (string k in SaveRegistry.BuiltInStringKeys) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();

        // In-memory singletons that already loaded their prefs this session.
        MissionProgressManager.Instance?.ClearAllProgress();
        PlantJournalManager.Instance?.ResetProgress();
        NewItemTracker.Instance?.ResetProgress();
        if (Instance != null) Instance.pending = null;
        DeleteSave();
    }

    // ------------------------------------------------------------
    // SCENE HOOKS
    // ------------------------------------------------------------
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pending != null && scene.name == pending.scene)
        {
            SaveFile file = pending;
            pending = null;
            StartCoroutine(RestoreRoutine(file));
        }
        else if (Registry != null && scene.name == Registry.mainSceneName)
        {
            StartCoroutine(AutosaveOnEnter());
        }
        ResetTimer();
    }

    private IEnumerator AutosaveOnEnter()
    {
        // Let every Start() run and the tutorial's own setup settle first.
        for (int i = 0; i < 5; i++) yield return null;
        if (!IsRestoring) Autosave("enter-main");
    }

    private IEnumerator RestoreRoutine(SaveFile file)
    {
        IsRestoring = true;
        // Two frames: every Awake/Start in the freshly loaded scene must have run (grids built, etc).
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();

        try { SaveWorld.Restore(file); }
        catch (Exception e) { Debug.LogError("[SaveManager] Restore failed part-way: " + e); }

        yield return null;
        IsRestoring = false;
        Restored?.Invoke();
        ResetTimer();
        Debug.Log("[SaveManager] World restored.");
    }
}
