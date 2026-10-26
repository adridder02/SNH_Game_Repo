using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// =============================================================
// PlayerAbilityInventory.cs
// -------------------------------------------------------------
// Attach to the Player, alongside PlayerInventory (PotContents.
// HarvestPlant looks it up via GetComponent on the same GameObject
// as the PlayerInventory it's already passed).
//
// Deliberately a SEPARATE component from PlayerInventory rather than
// bolted onto it — plants live in a positioned grid with drag/drop;
// ability items are a flat stacking list (Consumables + Placeables).
// OneOff items never touch this component at all — they apply their
// effect immediately at harvest time and are gone (see
// AbilityHarvestEffects.ApplyOneOff).
// =============================================================
public class PlayerAbilityInventory : MonoBehaviour
{
    [Header("Wall Placement")]
    [Tooltip("The placeable Clovenwick item (the Shelf). The first time the player receives one - picked up " +
             "fully grown, or harvested from a pot - wall placement unlocks and the 'picked_up_clovenwick' " +
             "tutorial trigger fires. Growing Clovenwicks (the plant itself) no longer trigger it.")]
    [SerializeField] private AbilityItemData wallPlacementItem;

    private readonly List<AbilityItemInstance> stacks = new List<AbilityItemInstance>();

    /// <summary>Fired whenever a stack is added/consumed, so ability-inventory UI can redraw.</summary>
    public event Action OnChanged;

    /// <summary>Fires exactly once — the first time the player ever receives a Consumable/Placeable
    /// ability item from harvesting a fully-grown plant out of a pot (PotContents.HarvestPlant, the
    /// only caller of Add() below). Lets onboarding-only UI that's specifically about USING harvested
    /// items (the hotbar rows, the pot menu's ability button) stay hidden until there's actually
    /// something to put in them — distinct from PlayerInventory.OnFirstPlantHarvested, which fires on
    /// ANY plant entering the plant inventory (harvest node, physical pickup, or pulled back out of a
    /// pot) and gates the inventory icon instead. See HasHarvestedFirstAbilityItem for the already-
    /// happened case (UI that initializes after this already fired once this session).</summary>
    public event Action OnFirstAbilityItemHarvested;

    /// <summary>True once OnFirstAbilityItemHarvested has fired. Check this on Start()/OnEnable() for
    /// UI that initializes after the first pot-harvest already happened this session — the event alone
    /// only reaches listeners that were already subscribed at the moment it fired.</summary>
    public bool HasHarvestedFirstAbilityItem { get; private set; }

    /// <summary>Fires once, the first time the player receives an item that can actually go on the hotbar
    /// (AbilityHotbarSystem.CanAssign). Items that only work from a pot's menu (e.g. the Pollen Puff) don't
    /// count, so the hotbar stays hidden until there's something usable to put on it.</summary>
    public event Action OnFirstHotbarItemHarvested;

    /// <summary>True once OnFirstHotbarItemHarvested has fired.</summary>
    public bool HasHarvestedFirstHotbarItem { get; private set; }

    /// <summary>Fired for every Add() — static so scene-level scripts (e.g. MissionObjectiveLinks) can listen
    /// without a reference to the player.</summary>
    public static event Action<AbilityItemData, int> ItemAdded;

    public IReadOnlyList<AbilityItemInstance> Stacks => stacks;

    // ---------------------------------------------------------------
    // ADD — called by PotContents.HarvestPlant() for Consumable/Placeable kinds.
    // ---------------------------------------------------------------
    public void Add(AbilityItemData data, int amount, bool countsAsFirstPickup = true)
    {
        if (data == null || amount <= 0) return;

        AbilityItemInstance existing = stacks.FirstOrDefault(s => s.data == data);
        if (existing == null)
        {
            existing = new AbilityItemInstance(data, 0);
            stacks.Add(existing);
        }

        existing.count += amount;

        if (data.maxStack > 0 && existing.count > data.maxStack)
            existing.count = data.maxStack;

        // Inventory "new item" badge (NewItemTracker) — safe to call every restock, not just the
        // first: MarkAcquiredInventory only actually flags anything the very first time this
        // data.name is ever seen. Uses the ScriptableObject asset's own .name as a stable id,
        // since AbilityItemData has no separate string id field.
        NewItemTracker.Instance?.MarkAcquiredInventory(data.name);

        Debug.Log($"[PlayerAbilityInventory] +{amount} {data.displayName} (now {existing.count}).");
        OnChanged?.Invoke();
        if (!PlayerInventory.SuppressAcquirePopups) ItemAdded?.Invoke(data, amount);

        if (wallPlacementItem != null && data == wallPlacementItem)
            GetComponent<PlayerInventory>()?.UnlockWallPlacement();

        // countsAsFirstPickup = false: starter items given by StartingInventoryGrant - they must not unlock the
        // hotbar / start the first-hotbar-item tutorial. Those wait for the real first pickup.
        if (!countsAsFirstPickup) return;

        if (!HasHarvestedFirstHotbarItem && AbilityHotbarSystem.CanAssign(data))
        {
            HasHarvestedFirstHotbarItem = true;
            OnFirstHotbarItemHarvested?.Invoke();
            TutorialSequenceController.RaiseStartTrigger("first_hotbar_item");
        }

        if (!HasHarvestedFirstAbilityItem)
        {
            HasHarvestedFirstAbilityItem = true;
            OnFirstAbilityItemHarvested?.Invoke();
        }
    }

    // ---------------------------------------------------------------
    // CONSUME — used before applying a Consumable/Placeable effect.
    // Returns false (and does nothing) if the player doesn't have enough.
    // ---------------------------------------------------------------
    public bool TryConsume(AbilityItemData data, int amount = 1)
    {
        if (data == null) return false;

        AbilityItemInstance existing = stacks.FirstOrDefault(s => s.data == data);
        if (existing == null || existing.count < amount) return false;

        existing.count -= amount;
        if (existing.count <= 0) stacks.Remove(existing);

        OnChanged?.Invoke();
        return true;
    }

    public int GetCount(AbilityItemData data)
    {
        AbilityItemInstance existing = stacks.FirstOrDefault(s => s.data == data);
        return existing?.count ?? 0;
    }

    // ------------------------------------------------------------
    // SAVE / LOAD (SaveWorld)
    // ------------------------------------------------------------
    /// <summary>Swaps in a saved set of stacks without firing pickup events (no tutorial triggers, no
    /// ItemAdded, no first-pickup events). Call NotifyChanged() afterwards so the UI redraws.</summary>
    public void ReplaceStacksSilently(List<AbilityItemInstance> newStacks, bool hadFirstAbilityItem, bool hadFirstHotbarItem)
    {
        stacks.Clear();
        stacks.AddRange(newStacks);
        HasHarvestedFirstAbilityItem = hadFirstAbilityItem;
        HasHarvestedFirstHotbarItem = hadFirstHotbarItem;
    }

    public void NotifyChanged() => OnChanged?.Invoke();
}