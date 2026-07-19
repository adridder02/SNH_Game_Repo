#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =============================================================
// TutorialMissionAssetGenerator.cs
// -------------------------------------------------------------
// TIME-CRUNCH SHORTCUT for the Guide checklist. MissionData /
// MissionDatabase are ScriptableObject assets, so they can't be
// hardcoded into a script the way the tutorial popup steps were —
// but this generates them for you in one click instead of creating
// two MissionData assets and a MissionDatabase by hand and typing
// every task in through Create > Guide > Mission Data each time.
//
// This file MUST stay inside a folder literally named "Editor"
// anywhere under Assets/ (already true here) — that's how Unity
// knows to exclude it from player builds.
//
// USE:
//   Unity menu bar -> Tools -> Tutorial -> Generate Tutorial Mission
//   Assets. Creates (or overwrites, if run again):
//     Assets/Guide/GeneratedMissions/Mission_PlantBasics.asset
//     Assets/Guide/GeneratedMissions/Mission_WaterPlant.asset
//     Assets/Guide/GeneratedMissions/Mission_JournalBasics.asset
//     Assets/Guide/GeneratedMissions/TutorialMissionDatabase.asset
//   Move the output folder path below first if Assets/Guide isn't
//   where the rest of your Guide/Mission assets live.
//
// After generating: assign TutorialMissionDatabase to
// GuideUIController's `database` field, and (once you're past the
// crunch) wire individual tasks into each tutorial step's
// linkedMission/linkedTaskId, matched up by the description text
// below — gameplay code still needs to call
// MissionProgressManager.Instance.CompleteTask(mission, task) at the
// right moments for any of this to actually track completion; this
// tool only creates the data assets, not the completion calls.
// =============================================================
public static class TutorialMissionAssetGenerator
{
    private const string OutputFolder = "Assets/Guide/GeneratedMissions";

    [MenuItem("Tools/Tutorial/Generate Tutorial Mission Assets")]
    public static void Generate()
    {
        EnsureFolder(OutputFolder);

        MissionData plantBasics = CreateMission("Mission_PlantBasics", "Plant Basics", new[]
        {
            "Find a dead plant",
            "Press [E] to pick up the plant",
            "Press [I] to open your inventory",
            "Drag the Pollen Puff to the Available section",
        });

        MissionData waterPlant = CreateMission("Mission_WaterPlant", "Water Plant", new[]
        {
            "Go to the water source",
            "Sit in the water until your water bar is filled",
            "Press [F] to enter placing mode",
            "Press [Q] to water your plants",
            "Find the smallest pot",
            "Place a pot on a sunny square",
            "Press [E] to interact with the pot",
            "Select loam as the soil type",
            "Select the Pollen Puff to plant the plant",
        });

        MissionData journalBasics = CreateMission("Mission_JournalBasics", "Journal Basics", new[]
        {
            "Press [J] to open your journal",
            "Open the Progress bookmark",
            "Open the Plants bookmark",
            "Open the Guide bookmark",
            "Press the back button and return to the game",
        });

        string databasePath = $"{OutputFolder}/TutorialMissionDatabase.asset";
        MissionDatabase database = AssetDatabase.LoadAssetAtPath<MissionDatabase>(databasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<MissionDatabase>();
            AssetDatabase.CreateAsset(database, databasePath);
        }
        database.missions = new List<MissionData> { plantBasics, waterPlant, journalBasics };
        EditorUtility.SetDirty(database);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[TutorialMissionAssetGenerator] Generated Plant Basics + Water Plant + Journal Basics missions and " +
                  $"TutorialMissionDatabase in {OutputFolder}. Assign the database to GuideUIController.");

        Selection.activeObject = database;
    }

    private static MissionData CreateMission(string assetName, string title, string[] taskDescriptions)
    {
        string path = $"{OutputFolder}/{assetName}.asset";
        MissionData mission = AssetDatabase.LoadAssetAtPath<MissionData>(path);

        if (mission == null)
        {
            mission = ScriptableObject.CreateInstance<MissionData>();
            AssetDatabase.CreateAsset(mission, path);
        }

        mission.missionTitle = title;
        mission.tasks = new List<MissionTaskEntry>();
        foreach (string desc in taskDescriptions)
            mission.tasks.Add(new MissionTaskEntry { description = desc });

        EditorUtility.SetDirty(mission);
        return mission;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
