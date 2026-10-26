#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Menu:  Neglect > Audio > Create Sound Bank        (makes Assets/Resources/SoundBank.asset)
//        Neglect > Audio > Auto-Fill Sound Bank...  (pick the folder your Pixabay clips are in)
public static class SoundBankTools
{
    private const string BankPath = "Assets/Resources/SoundBank.asset";

    [MenuItem("Neglect/Audio/Create Sound Bank")]
    public static void Create()
    {
        SoundBank bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
        if (bank == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            bank = ScriptableObject.CreateInstance<SoundBank>();
            AssetDatabase.CreateAsset(bank, BankPath);
        }
        bank.EnsureDefaults();
        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
        Selection.activeObject = bank;
        EditorGUIUtility.PingObject(bank);
        Debug.Log("[SoundBank] Ready at " + BankPath + ". Next: Neglect > Audio > Auto-Fill Sound Bank...");
    }

    [MenuItem("Neglect/Audio/Auto-Fill Sound Bank...")]
    public static void AutoFill()
    {
        Create();
        SoundBank bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);

        string abs = EditorUtility.OpenFolderPanel("Folder containing your sound clips (searched recursively)", Application.dataPath, "");
        if (string.IsNullOrEmpty(abs)) return;
        if (!abs.Replace('\\', '/').StartsWith(Application.dataPath.Replace('\\', '/')))
        {
            EditorUtility.DisplayDialog("Auto-Fill", "Pick a folder inside this project's Assets folder.", "OK");
            return;
        }
        string rel = "Assets" + abs.Replace('\\', '/').Substring(Application.dataPath.Replace('\\', '/').Length);

        var clips = new System.Collections.Generic.List<AudioClip>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { rel }))
            clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)));

        var report = new StringBuilder("[SoundBank] Auto-fill from " + rel + " (" + clips.Count + " clips)\n");
        int filled = 0;

        foreach (SoundBank.Entry e in bank.sounds)
        {
            if (e.clip != null) { continue; }
            AudioClip match = Find(clips, e.filenameHints);
            if (match != null) { e.clip = match; filled++; report.AppendLine("  " + e.id + " <- " + match.name); }
            else report.AppendLine("  " + e.id + " : NOT FOUND (assign by hand)");
        }
        foreach (SoundBank.Step s in bank.footsteps)
        {
            if (s.clip != null) continue;
            AudioClip match = Find(clips, s.filenameHints);
            if (match != null) { s.clip = match; filled++; report.AppendLine("  Footstep " + s.surface + " <- " + match.name); }
            else report.AppendLine("  Footstep " + s.surface + " : NOT FOUND (assign by hand)");
        }

        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
        Debug.Log(report.ToString() + "Filled " + filled + " slots. Empty slots can be dragged in on the SoundBank asset.");
    }

    private static AudioClip Find(System.Collections.Generic.List<AudioClip> clips, string[] hints)
    {
        if (hints == null || hints.Length == 0) return null;
        foreach (AudioClip c in clips)
        {
            string n = c.name.ToLowerInvariant().Replace('_', '-').Replace(' ', '-');
            foreach (string h in hints)
                if (!string.IsNullOrEmpty(h) && n.Contains(h)) return c;
        }
        return null;
    }
}
#endif
