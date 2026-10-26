using UnityEngine;
using UnityEngine.SceneManagement;

// =============================================================
// StartingInventoryGrant.cs
// -------------------------------------------------------------
// Gives the player a starting Pollen Puff plant and a Pollen Puff consumable
// when they spawn into the Main scene.
//
// SETUP:
//   1. Put this on the Player alongside PlayerInventory + PlayerAbilityInventory.
//   2. Plant Prefab    -> the Pollen Puff plant prefab (the same one a harvest node gives).
//      The icon / image / name are read from the prefab's PlantState.journalSpecies,
//      so nothing else needs filling in.
//   3. Consumable Item -> the Pollen Puff AbilityItemData asset (Puff of Pollen).
//   4. Amounts default to 1 each. Scene Name guard defaults to "Main" (leave empty = any scene).
// =============================================================
public class StartingInventoryGrant : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerAbilityInventory abilityInventory;

    [Header("Only grant in this scene (empty = any)")]
    [SerializeField] private string sceneName = "Main";

    [Header("Pollen Puff plant")]
    [SerializeField] private GameObject plantPrefab;
    [Min(0)] [SerializeField] private int plantCount = 1;

    [Header("Pollen Puff consumable")]
    [SerializeField] private AbilityItemData consumableItem;
    [Min(0)] [SerializeField] private int consumableCount = 1;

    [Header("Onboarding")]
    [Tooltip("On (recommended in the tutorial scene): the starter items do NOT count as the player's first pickup, " +
             "so the inventory icon / hotbar stay hidden until the tutorial (or a real pickup) reveals them.")]
    [SerializeField] private bool keepOnboardingUIHidden = true;

    private void Start()
    {
        if (!string.IsNullOrEmpty(sceneName) && SceneManager.GetActiveScene().name != sceneName) return;

        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        if (abilityInventory == null) abilityInventory = GetComponent<PlayerAbilityInventory>();

        if (plantPrefab != null && inventory != null)
        {
            PlantState state = plantPrefab.GetComponent<PlantState>();
            PlantSpeciesData sp = state != null ? state.journalSpecies : null;
            for (int i = 0; i < plantCount; i++)
                inventory.AddPlantToInventory(plantPrefab,
                    sp != null ? sp.journalIcon : null,
                    sp != null ? sp.InventoryImage : null,
                    sp != null ? sp.displayName : null,
                    null,
                    !keepOnboardingUIHidden);
        }
        else if (plantCount > 0)
            Debug.LogWarning("[StartingInventoryGrant] Plant prefab or PlayerInventory missing - no plant granted.", this);

        if (consumableItem != null && abilityInventory != null && consumableCount > 0)
            abilityInventory.Add(consumableItem, consumableCount, !keepOnboardingUIHidden);
        else if (consumableCount > 0)
            Debug.LogWarning("[StartingInventoryGrant] Consumable item or PlayerAbilityInventory missing - nothing granted.", this);
    }
}
