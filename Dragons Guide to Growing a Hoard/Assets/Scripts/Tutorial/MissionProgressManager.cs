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

    /// <summary>Id of the mission the player is tracking (shown on the Tutorial Objective banner). Empty = none.</summary>
    public string TrackedMissionId { get; private set; } = "";

    [Serializable] private class MissionSave { public string missionId; public List<string> taskIds = new List<string>(); }
    [Serializable] private class SaveData { public List<MissionSave> missions = new List<MissionSave>(); public string trackedMissionId; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // Remove this if MissionProgressManager is placed once per-scene instead of persisted globally.
            DontDestroyOnLoad(gameObject);
            Load();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
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

    /// <summary>Wipes all saved + in-memory mission progress and tracking (e.g. for a New Game button).</summary>
    [ContextMenu("Clear Saved Progress")]
    public void ClearAllProgress()
    {
        completedTasks.Clear();
        TrackedMissionId = "";
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