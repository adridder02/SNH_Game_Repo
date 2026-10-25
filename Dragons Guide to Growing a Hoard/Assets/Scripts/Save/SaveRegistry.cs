using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// =============================================================
// SaveRegistry.cs
// -------------------------------------------------------------
// ONE asset at  Assets/Resources/SaveRegistry.asset  (Create > Neglect > Save Registry).
//
// Saves can't store references to prefabs / ScriptableObjects, so they store NAMES and this
// asset turns the names back into the real thing on load. Right-click the asset header ->
// "Auto-fill From Project" and it finds every plant prefab (anything with a PlantState),
// AbilityItemData, PotData and WallMushroomData in the project for you. Re-run it whenever you
// add new ones. Asset/prefab names must be UNIQUE within each list.
//
// It also holds the three scene names and the extra PlayerPrefs keys to carry in the save.
// =============================================================
[CreateAssetMenu(menuName = "Neglect/Save Registry", fileName = "SaveRegistry")]
public class SaveRegistry : ScriptableObject
{
    [Header("Scenes (exact names from Build Settings)")]
    public string titleSceneName = "Title";
    public string tutorialSceneName = "Tutorial";
    [Tooltip("The scene the player actually plays in. Saving only happens here; Continue loads it.")]
    public string mainSceneName = "Main";

    [Header("Lookup tables (use Auto-fill From Project)")]
    public List<GameObject> plantPrefabs = new List<GameObject>();
    public List<AbilityItemData> abilityItems = new List<AbilityItemData>();
    public List<PotData> pots = new List<PotData>();
    public List<WallMushroomData> wallMushrooms = new List<WallMushroomData>();

    [Header("Extra PlayerPrefs keys to keep in the save")]
    [Tooltip("The journal, new-item badges and tutorial 'completion flag' keys found in the scene are included " +
             "automatically. Add anything else here (int keys / string keys / float keys).")]
    public List<string> intPrefKeys = new List<string>();
    public List<string> stringPrefKeys = new List<string>();
    public List<string> floatPrefKeys = new List<string>();

    // Keys the game already uses (see PlantJournalManager / NewItemTracker).
    public static readonly string[] BuiltInStringKeys = { "PlantJournal_Discovered", "PlantJournal_Guesses", "NewItemTracker_State", "FirstPickup_Seen", "PlantCompleted_Seen" };

    private static SaveRegistry instance;
    public static SaveRegistry Instance
    {
        get
        {
            if (instance == null) instance = Resources.Load<SaveRegistry>("SaveRegistry");
            return instance;
        }
    }

    public GameObject FindPlant(string n) => Find(plantPrefabs, n);
    public AbilityItemData FindAbility(string n) => Find(abilityItems, n);
    public PotData FindPot(string n) => Find(pots, n);
    public WallMushroomData FindMushroom(string n) => Find(wallMushrooms, n);

    private static T Find<T>(List<T> list, string n) where T : Object
    {
        if (string.IsNullOrEmpty(n)) return null;
        foreach (T t in list) if (t != null && t.name == n) return t;
        Debug.LogWarning($"[SaveRegistry] '{n}' isn't in the registry ({typeof(T).Name} list) — run 'Auto-fill From Project' on the SaveRegistry asset.");
        return null;
    }

#if UNITY_EDITOR
    [ContextMenu("Auto-fill From Project")]
    private void AutoFill()
    {
        plantPrefabs.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (go != null && go.GetComponentInChildren<PlantState>(true) != null) plantPrefabs.Add(go);
        }
        Fill(abilityItems);
        Fill(pots);
        Fill(wallMushrooms);
        CheckDuplicates(plantPrefabs); CheckDuplicates(abilityItems); CheckDuplicates(pots); CheckDuplicates(wallMushrooms);
        EditorUtility.SetDirty(this);
        Debug.Log($"[SaveRegistry] Filled: {plantPrefabs.Count} plant prefabs, {abilityItems.Count} ability items, {pots.Count} pots, {wallMushrooms.Count} wall mushrooms.");
    }

    private static void Fill<T>(List<T> list) where T : Object
    {
        list.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            T a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (a != null) list.Add(a);
        }
    }

    private static void CheckDuplicates<T>(List<T> list) where T : Object
    {
        var seen = new HashSet<string>();
        foreach (T t in list)
            if (t != null && !seen.Add(t.name))
                Debug.LogWarning($"[SaveRegistry] Two {typeof(T).Name} assets share the name '{t.name}' — saves can't tell them apart. Rename one.");
    }
#endif
}
