using System.IO;
using UnityEditor;
using UnityEngine;

// =============================================================
// NeglectSaveTools.cs  (Editor only)
// -------------------------------------------------------------
// Tools > Neglect menu for starting a test run from zero without hunting through folders.
//   Reset Editor Progress   deletes the Editor's save file AND clears the Editor's PlayerPrefs
//                           (tutorial flags, journal, mission progress...). Also resets the settings.
//   Delete Editor Save Only deletes just the save file.
//   Open Save Folder        reveals where save files live.
// Builds are completely separate: they use their own save file name and their own PlayerPrefs.
// =============================================================
public static class NeglectSaveTools
{
    [MenuItem("Tools/Neglect/Reset Editor Progress (save + PlayerPrefs)")]
    private static void ResetAll()
    {
        if (!EditorUtility.DisplayDialog("Reset editor progress",
                "Deletes the Editor's save file and ALL Editor PlayerPrefs (tutorial flags, journal, settings). " +
                "Builds are not affected.", "Reset", "Cancel")) return;

        DeleteFiles();
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[NeglectSaveTools] Editor save + PlayerPrefs cleared. Next play starts from zero.");
    }

    [MenuItem("Tools/Neglect/Delete Editor Save Only")]
    private static void DeleteSaveOnly()
    {
        DeleteFiles();
        Debug.Log("[NeglectSaveTools] Editor save file deleted.");
    }

    [MenuItem("Tools/Neglect/Open Save Folder")]
    private static void OpenFolder() => EditorUtility.RevealInFinder(Application.persistentDataPath);

    private static void DeleteFiles()
    {
        string f = SaveManager.FilePath;
        foreach (string p in new[] { f, f + ".bak", f + ".tmp" })
            if (File.Exists(p)) File.Delete(p);
    }
}
