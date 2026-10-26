using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// =============================================================
// DEBUG_GiveAllPlants.cs - TESTING ONLY, remove/disable before ship
// -------------------------------------------------------------
// Puts one (or `copiesEach`) of every plant prefab in the player's inventory when the scene starts,
// so every plant can be placed and tested straight away. Journal species are marked discovered too
// (that's what adding a plant normally does). First-pickup popups are skipped.
//
// SETUP:
//   1. Put this on the Player (next to PlayerInventory).
//   2. Right-click the component header > "Find All Plant Prefabs In Project" (editor only) to fill the list,
//      or drag the prefabs in yourself. It picks up every prefab that has a PlantState.
//   3. Play. Press the hotkey (default F9) to grant them again mid-game.
//   Inventory full? Extras land in the "Available" list like normal; a warning prints if any couldn't be added.
// =============================================================
public class DEBUG_GiveAllPlants : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private List<GameObject> plantPrefabs = new List<GameObject>();
    [Min(1)] [SerializeField] private int copiesEach = 1;
    [SerializeField] private bool grantOnStart = true;
    [Tooltip("Press to grant another full set (old Input class; leave None to disable).")]
    [SerializeField] private KeyCode grantKey = KeyCode.F9;

    private void Start()
    {
        if (grantOnStart) Grant();
    }

    private void Update()
    {
        if (grantKey == KeyCode.None) return;
        try { if (Input.GetKeyDown(grantKey)) Grant(); }
        catch (System.InvalidOperationException) { /* project uses only the new Input System */ }
    }

    [ContextMenu("Grant Now")]
    public void Grant()
    {
        if (inventory == null) inventory = GetComponent<PlayerInventory>() ?? FindFirstObjectByType<PlayerInventory>();
        if (inventory == null) { Debug.LogWarning("[DEBUG_GiveAllPlants] No PlayerInventory found.", this); return; }

        int given = 0, failed = 0;
        foreach (GameObject prefab in plantPrefabs)
        {
            if (prefab == null) continue;
            PlantState state = prefab.GetComponent<PlantState>();
            PlantSpeciesData sp = state != null ? state.journalSpecies : null;
            for (int i = 0; i < copiesEach; i++)
            {
                bool ok = inventory.AddPlantToInventory(prefab,
                    sp != null ? sp.journalIcon : null,
                    sp != null ? sp.InventoryImage : null,
                    sp != null ? sp.displayName : prefab.name,
                    null,
                    false);
                if (ok) given++; else failed++;
            }
        }
        Debug.Log($"[DEBUG_GiveAllPlants] Added {given} plant(s)" + (failed > 0 ? $", {failed} could not be added." : "."));
    }

#if UNITY_EDITOR
    [ContextMenu("Find All Plant Prefabs In Project")]
    private void FindAll()
    {
        plantPrefabs.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (go != null && go.GetComponent<PlantState>() != null) plantPrefabs.Add(go);
        }
        EditorUtility.SetDirty(this);
        Debug.Log($"[DEBUG_GiveAllPlants] Found {plantPrefabs.Count} prefab(s) with a PlantState - remove any that are harvest-node/variant duplicates.");
    }
#endif
}
