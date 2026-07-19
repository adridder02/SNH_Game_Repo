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
public class MissionProgressManager : MonoBehaviour
{
    public static MissionProgressManager Instance { get; private set; }

    /// <summary>Fired whenever any task's completion state changes, so the Guide UI can refresh whatever mission is currently open.</summary>
    public event Action OnProgressChanged;

    // missionId -> set of completed taskIds
    private readonly Dictionary<string, HashSet<string>> completedTasks = new Dictionary<string, HashSet<string>>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // Remove this if MissionProgressManager is placed once per-scene instead of persisted globally.
            DontDestroyOnLoad(gameObject);
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
            OnProgressChanged?.Invoke();
    }

    /// <summary>Marks a task incomplete again — mainly useful for repeatable missions/testing.</summary>
    public void ResetTask(string missionId, string taskId)
    {
        if (completedTasks.TryGetValue(missionId, out HashSet<string> set) && set.Remove(taskId))
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
}
