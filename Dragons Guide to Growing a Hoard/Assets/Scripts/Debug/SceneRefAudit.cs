#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// =============================================================
// SceneRefAudit.cs  (TEMPORARY diagnostic - delete when the missing-reference problem is solved)
// -------------------------------------------------------------
// Boots itself. After every scene load it waits a couple of frames (and again 3 seconds later) and
// prints ONE console block listing:
//   - every root object in the loaded scene, and every root object sitting in "DontDestroyOnLoad"
//   - every component field that points at an object which has been DESTROYED ("Missing"), as
//     Scene/Object/Component.field -> what type it was
// Copy that console block (everything starting with [SceneRefAudit]) and send it over.
// =============================================================
public class SceneRefAudit : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("SceneRefAudit");
        DontDestroyOnLoad(go);
        go.AddComponent<SceneRefAudit>();
    }

    void OnEnable() { SceneManager.sceneLoaded += OnLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnLoaded; }

    private void OnLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(Run(scene));
    }

    private IEnumerator Run(Scene scene)
    {
        yield return null; yield return null;
        Report(scene, "2 frames after load");
        yield return new WaitForSecondsRealtime(3f);
        Report(scene, "3 s after load");
    }

    private void Report(Scene scene, string when)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[SceneRefAudit] scene '{scene.name}' ({when})");

        sb.AppendLine("  Scene roots:");
        if (scene.IsValid())
            foreach (GameObject g in scene.GetRootGameObjects()) sb.AppendLine("    - " + g.name);

        sb.AppendLine("  DontDestroyOnLoad roots (carried over from earlier scenes):");
        foreach (GameObject g in gameObject.scene.GetRootGameObjects())
        {
            if (g == gameObject) continue;
            var comps = g.GetComponents<Component>();
            var names = new StringBuilder();
            foreach (Component c in comps) if (c != null && !(c is Transform)) names.Append(c.GetType().Name).Append(' ');
            sb.AppendLine($"    - {g.name}  [{names.ToString().Trim()}]");
        }

        sb.AppendLine("  Missing references in this scene:");
        int count = 0;
        if (scene.IsValid())
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    for (Type t = mb.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                    {
                        foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        {
                            if (!typeof(Object).IsAssignableFrom(f.FieldType)) continue;
                            bool serialized = (f.IsPublic && !f.IsNotSerialized) || f.GetCustomAttribute<SerializeField>() != null;
                            if (!serialized) continue;
                            object raw;
                            try { raw = f.GetValue(mb); } catch { continue; }
                            if (raw == null) continue;                  // genuinely unassigned (None) - not our concern
                            if ((Object)raw != null) continue;          // alive
                            sb.AppendLine($"    - {Path(mb.transform)} / {t.Name}.{f.Name}  (was a {f.FieldType.Name})");
                            count++;
                        }
                    }
                }
        }
        if (count == 0) sb.AppendLine("    (none found)");

        Debug.Log(sb.ToString());
    }

    private static string Path(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
#endif
