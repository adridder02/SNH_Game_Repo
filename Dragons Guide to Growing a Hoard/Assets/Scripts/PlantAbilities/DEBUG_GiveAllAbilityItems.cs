using System.Collections.Generic;
using UnityEngine;

// =============================================================
// DEBUG_GiveAllAbilityItems.cs — TESTING ONLY, remove/disable before ship
// -------------------------------------------------------------
// Stocks the player's PlayerAbilityInventory with one harvest's worth
// (AbilityItemData.harvestGrantAmount) of every ability item on spawn,
// so every plant's ability can be tested immediately without first
// growing the actual plant to completion and harvesting it.
//
// Covers all 7 currently-defined AbilityItemData assets:
//   Bubble of Holding (Bubble Blossom), Dew Drop (Dewdropper),
//   Glowcap Spore (Glowcap), Puff of Pollen (Pollen Puff),
//   Spark Leaf (SparkMint), Verdant Algae, Waterbell.
// Drag each asset from Data/Abilities into allItems in the Inspector —
// left empty on purpose rather than hard-coded, so this keeps working
// if items are renamed/added/removed later.
//
// This is a companion to DEBUG_AbilityPlaceTester.cs, not a replacement —
// that one also wires up '1'/'2' keys to instantly begin PLACING a
// Sparkmint Leaf / Waterbell. Use both together, or just this one if you
// only need the items sitting in the inventory/hotbar/pot-menu panels to
// click through by hand.
//
// SETUP:
//   1. Put this on the Player alongside PlayerAbilityInventory.
//   2. Drag all 7 AbilityItemData assets into allItems.
//   3. Play — check the Inventory's Abilities panel / a pot's Abilities
//      panel (for the pot-targeted ones) to confirm each shows up.
// =============================================================
public class DEBUG_GiveAllAbilityItems : MonoBehaviour
{
    [Tooltip("Auto-found on this GameObject if left empty.")]
    [SerializeField] private PlayerAbilityInventory abilityInventory;

    [Tooltip("Drag every AbilityItemData asset (Data/Abilities/*.asset) in here.")]
    [SerializeField] private List<AbilityItemData> allItems = new List<AbilityItemData>();

    private void Start()
    {
        if (abilityInventory == null)
            abilityInventory = GetComponent<PlayerAbilityInventory>();

        if (abilityInventory == null)
        {
            Debug.LogWarning("[DEBUG_GiveAllAbilityItems] No PlayerAbilityInventory found on this GameObject — nothing granted.", this);
            return;
        }

        if (allItems.Count == 0)
        {
            Debug.LogWarning("[DEBUG_GiveAllAbilityItems] allItems is empty — drag every AbilityItemData " +
                              "asset from Data/Abilities into the Inspector list.", this);
            return;
        }

        foreach (AbilityItemData item in allItems)
        {
            if (item == null) continue;
            int amount = Mathf.Max(1, item.harvestGrantAmount);
            abilityInventory.Add(item, amount);
        }

        Debug.Log($"[DEBUG_GiveAllAbilityItems] Granted {allItems.Count} ability item type(s) for testing.");
    }
}
