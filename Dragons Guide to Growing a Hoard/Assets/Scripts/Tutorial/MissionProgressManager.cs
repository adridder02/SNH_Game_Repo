using System;
using System.Collections.Generic;
using UnityEngine;

// =============================================================
// MissionProgressManager.cs
// -------------------------------------------------------------
// Single source of truth for "is this task done". Gameplay scripts
// call CompleteTask(...) directly (no Toggle, no per-script bools) —
// this replaces the removePlant/hasMoved/etc static bools and the
// Tasks1..6 Toggle wiring that used to live inside the tutorial
// scripts. GuideUIController reads back through IsTaskComplete /
// IsMissionComplete to decide which checkmarks to show.
//
// SETUP:
//   Put this on a persistent object (e.g. a GameManager) that
//   exists in whatever scene loads first, or add [DontDestroyOnLoad]
//   handling like below if it needs to survive scene loads.
// =============================================================
[DefaultExecutionOrder(-200)] // sets Instance before any UI looks for it
public class MissionProgressManager : MonoBehaviour
{
    public static MissionProgressManager Instance { get; private set; }

    /// <summary>Fired whenever any task's completion state changes, so the Guide UI can refresh whatever mission is currently open.</summary>
    public event Action OnProgressChanged;

    // missionId -> set of completed taskIds
    private readonly Dictionary<string, HashSet<string>> completedTasks = new Dictionary<string, HashSet<string>>();

    [Header("Saving")]
    [Tooltip("Saves mission progress + the tracked mission to PlayerPrefs so they survive quitting. OFF by " +
             "default (progress only lives for the current run, like before). Use MissionAutoComplete to " +
             "pre-complete missions in a scene instead.")]
    [SerializeField] private bool persistProgress = false;
    [SerializeField] private string saveKey = "MissionProgress_v1";

    // ---- lore ----
    private readonly HashSet<string> collectedLore = new HashSet<string>();
    private const string LoreSaveKey = "LoreCollected_v1";
    [Serializable] private class LoreSave { public List<string> ids = new List<string>(); }

    /// <summary>Marks a Lore entry as found (shows up in the journal's Lore bookmark). Always saved.</summary>
    public void CollectLore(MissionData lore)
    {
        if (lore == null) return;
        if (collectedLore.Add(lore.ResolvedId))
        {
            SaveLore();
            OnProgressChanged?.Invoke();
        }
    }

    public bool IsLoreCollected(MissionData lore) => lore != null && collectedLore.Contains(lore.ResolvedId);

    private void SaveLore()
    {
        PlayerPrefs.SetString(LoreSaveKey, JsonUtility.ToJson(new LoreSave { ids = new List<string>(collectedLore) }));
        PlayerPrefs.Save();
    }

    private void LoadLore()
    {
        if (!PlayerPrefs.HasKey(LoreSaveKey)) return;
        LoreSave data = JsonUtility.FromJson<LoreSave>(PlayerPrefs.GetString(LoreSaveKey));
        if (data?.ids == null) return;
        collectedLore.Clear();
        foreach (string id in data.ids) collectedLore.Add(id);
    }

    /// <summary>Id of the mission the player is tracking (shown on the Tutorial Objective banner). Empty = none.</summary>
    public string TrackedMissionId { get; private set; } = "";

    [Serializable] private class MissionSave { public string missionId; public List<string> taskIds = new List<string>(); }
    [Serializable] private class SaveData { public List<MissionSave> missions = new List<MissionSave>(); public string trackedMissionId; }

    void Awake()
    {
        if (!PersistentManager.Claim(this, Instance)) return;
        Instance = this;
        Load();
        LoadLore();
    }

    // ------------------------------------------------------------
    // WRITE — call these from gameplay code
    // ------------------------------------------------------------
    public void CompleteTask(MissionData mission, MissionTaskEntry task)
    {
        if (mission == null || task == null) return;
        CompleteTask(mission.ResolvedId, task.ResolvedId);
    }

    public void CompleteTask(string missionId, string taskId)
    {
        if (string.IsNullOrEmpty(missionId) || string.IsNullOrEmpty(taskId)) return;

        if (!completedTasks.TryGetValue(missionId, out HashSet<string> set))
        {
            set = new HashSet<string>();
            completedTasks[missionId] = set;
        }

        if (set.Add(taskId)) // only fire the event if this actually changed something
        {
            GameAudio.Play(SoundId.TaskComplete);
            Save();
            OnProgressChanged?.Invoke();
        }
    }

    /// <summary>Marks a task incomplete again — mainly useful for repeatable missions/testing.</summary>
    public void ResetTask(string missionId, string taskId)
    {
        if (completedTasks.TryGetValue(missionId, out HashSet<string> set) && set.Remove(taskId))
        {
            Save();
            OnProgressChanged?.Invoke();
        }
    }

    // ------------------------------------------------------------
    // TRACKING — which mission the Tutorial Objective banner follows
    // ------------------------------------------------------------
    public bool IsTracked(MissionData mission)
        => mission != null && !string.IsNullOrEmpty(TrackedMissionId) && TrackedMissionId == mission.ResolvedId;

    public void TrackMission(MissionData mission)
    {
        if (mission == null || mission.IsLore || IsTracked(mission)) return;
        TrackedMissionId = mission.ResolvedId;
        Save();
        OnProgressChanged?.Invoke();
    }

    public void UntrackMission()
    {
        if (string.IsNullOrEmpty(TrackedMissionId)) return;
        TrackedMissionId = "";
        Save();
        OnProgressChanged?.Invoke();
    }

    public void ToggleTracked(MissionData mission)
    {
        if (IsTracked(mission)) UntrackMission();
        else TrackMission(mission);
    }

    // ------------------------------------------------------------
    // SAVE / LOAD (PlayerPrefs)
    // ------------------------------------------------------------
    private void Save()
    {
        if (!persistProgress) return;

        SaveData data = new SaveData { trackedMissionId = TrackedMissionId };
        foreach (KeyValuePair<string, HashSet<string>> kv in completedTasks)
            data.missions.Add(new MissionSave { missionId = kv.Key, taskIds = new List<string>(kv.Value) });

        PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    private void Load()
    {
        if (!persistProgress || !PlayerPrefs.HasKey(saveKey)) return;

        SaveData data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(saveKey));
        if (data == null) return;

        completedTasks.Clear();
        foreach (MissionSave m in data.missions)
            if (!string.IsNullOrEmpty(m.missionId))
                completedTasks[m.missionId] = new HashSet<string>(m.taskIds);

        TrackedMissionId = data.trackedMissionId ?? "";
    }

    // ------------------------------------------------------------
    // SAVE SLOT SNAPSHOT (SaveManager) — missions + tracked mission + collected lore as one JSON string
    // ------------------------------------------------------------
    [Serializable] private class FullSnapshot { public SaveData progress = new SaveData(); public LoreSave lore = new LoreSave(); }

    public string CaptureJson()
    {
        var snap = new FullSnapshot();
        snap.progress.trackedMissionId = TrackedMissionId;
        foreach (KeyValuePair<string, HashSet<string>> kv in completedTasks)
            snap.progress.missions.Add(new MissionSave { missionId = kv.Key, taskIds = new List<string>(kv.Value) });
        snap.lore.ids = new List<string>(collectedLore);
        return JsonUtility.ToJson(snap);
    }

    public void RestoreJson(string json)
    {
        FullSnapshot snap = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<FullSnapshot>(json);
        if (snap == null) return;

        completedTasks.Clear();
        foreach (MissionSave m in snap.progress.missions)
            if (!string.IsNullOrEmpty(m.missionId))
                completedTasks[m.missionId] = new HashSet<string>(m.taskIds);
        TrackedMissionId = snap.progress.trackedMissionId ?? "";

        collectedLore.Clear();
        foreach (string id in snap.lore.ids) collectedLore.Add(id);

        Save();
        SaveLore();
        OnProgressChanged?.Invoke();
    }

    /// <summary>Wipes all saved + in-memory mission progress and tracking (e.g. for a New Game button).</summary>
    [ContextMenu("Clear Saved Progress")]
    public void ClearAllProgress()
    {
        completedTasks.Clear();
        collectedLore.Clear();
        TrackedMissionId = "";
        PlayerPrefs.DeleteKey(LoreSaveKey);
        PlayerPrefs.DeleteKey(saveKey);
        PlayerPrefs.Save();
        OnProgressChanged?.Invoke();
    }

    // ------------------------------------------------------------
    // READ — call these from UI
    // ------------------------------------------------------------
    public bool IsTaskComplete(MissionData mission, MissionTaskEntry task)
        => mission != null && task != null && IsTaskComplete(mission.ResolvedId, task.ResolvedId);

    public bool IsTaskComplete(string missionId, string taskId)
        => completedTasks.TryGetValue(missionId, out HashSet<string> set) && set.Contains(taskId);

    /// <summary>True when every task in a LOWER group is complete — i.e. this task has been revealed.
    /// (Everything is unlocked if all tasks share group 0.)</summary>
    public bool IsTaskUnlocked(MissionData mission, MissionTaskEntry task)
    {
        if (mission == null || task == null) return false;
        foreach (MissionTaskEntry other in mission.tasks)
            if (other.group < task.group && !IsTaskComplete(mission.ResolvedId, other.ResolvedId))
                return false;
        return true;
    }

    public bool IsTaskUnlocked(MissionData mission, string taskId)
    {
        if (mission == null) return false;
        foreach (MissionTaskEntry t in mission.tasks)
            if (t.ResolvedId == taskId) return IsTaskUnlocked(mission, t);
        return false;
    }

    public bool IsMissionComplete(MissionData mission)
    {
        if (mission == null || mission.tasks.Count == 0) return false;

        foreach (MissionTaskEntry task in mission.tasks)
            if (!IsTaskComplete(mission.ResolvedId, task.ResolvedId))
                return false;

        return true;
    }

    /// <summary>True only if taskId is the FIRST not-yet-complete task in mission.tasks (in list order).
    /// Use this to gate completion calls for strictly-ordered checklists — e.g. the movement tutorial,
    /// where PlayerController reports actions as they happen in real time and an early/stray action
    /// (like an accidental landing before the tutorial reaches its "land" step) shouldn't get silently
    /// banked out of order. Returns false once every task is already complete.</summary>
    public bool IsNextTask(MissionData mission, string taskId)
    {
        if (mission == null || string.IsNullOrEmpty(taskId)) return false;

        foreach (MissionTaskEntry task in mission.tasks)
        {
            if (IsTaskComplete(mission.ResolvedId, task.ResolvedId)) continue;
            return task.ResolvedId == taskId;
        }

        return false;
    }

    /// <summary>Convenience wrapper: completes taskId only if IsNextTask says it's next in line, in one
    /// call. Use this from gameplay code for any strictly-ordered checklist mission instead of calling
    /// IsNextTask + CompleteTask separately at every call site.</summary>
    public void CompleteOrderedTask(MissionData mission, string taskId)
    {
        if (!IsNextTask(mission, taskId)) return;
        CompleteTask(mission.ResolvedId, taskId);
    }
}