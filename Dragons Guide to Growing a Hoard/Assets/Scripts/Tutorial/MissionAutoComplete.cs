using UnityEngine;

// =============================================================
// MissionAutoComplete.cs
// -------------------------------------------------------------
// Put this in a scene (e.g. the main scene) to mark missions as fully complete the moment the scene
// starts — for missions the player did somewhere they can't be seen, like the tutorial scene's
// Introduction mission.
//
//   missions      — every mission to mark complete (all its tasks).
//   onlyIfPrefsKey — optional. Leave BLANK to always complete them. Or enter a PlayerPrefs flag
//                    (e.g. "Tutorial_MainHalfCompleted") to only do it once that flag is set.
// =============================================================
public class MissionAutoComplete : MonoBehaviour
{
    [SerializeField] private MissionData[] missions;
    [Tooltip("Blank = always complete on scene start. Otherwise only when this PlayerPrefs flag == 1.")]
    [SerializeField] private string onlyIfPrefsKey = "";

    void Start()
    {
        if (!string.IsNullOrEmpty(onlyIfPrefsKey) && PlayerPrefs.GetInt(onlyIfPrefsKey, 0) != 1)
        {
            Debug.Log($"[MissionAutoComplete] Skipped: PlayerPrefs flag '{onlyIfPrefsKey}' isn't set. " +
                      "Clear 'Only If Prefs Key' on this component to always complete.", this);
            return;
        }

        MissionProgressManager pm = MissionProgressManager.Instance != null
            ? MissionProgressManager.Instance
            : FindAnyObjectByType<MissionProgressManager>();
        if (pm == null)
        {
            Debug.LogWarning("[MissionAutoComplete] No MissionProgressManager found in the scene.", this);
            return;
        }

        if (missions == null || missions.Length == 0)
            Debug.LogWarning("[MissionAutoComplete] The Missions list is empty.", this);

        foreach (MissionData m in missions)
        {
            if (m == null) { Debug.LogWarning("[MissionAutoComplete] A slot in Missions is empty.", this); continue; }

            foreach (MissionTaskEntry task in m.tasks)
                pm.CompleteTask(m, task);

            Debug.Log($"[MissionAutoComplete] Completed '{m.missionTitle}' ({m.tasks.Count} tasks) -> " +
                      $"IsMissionComplete = {pm.IsMissionComplete(m)}", this);
        }
    }
}
