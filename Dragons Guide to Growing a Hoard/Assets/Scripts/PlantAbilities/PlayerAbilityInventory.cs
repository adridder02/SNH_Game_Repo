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

    public IReadOnlyList<AbilityItemInstance> Stacks => stacks;

    // ---------------------------------------------------------------
    // ADD — called by PotContents.HarvestPlant() for Consumable/Placeable kinds.
    // ---------------------------------------------------------------
    public void Add(AbilityItemData data, int amount)
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
}