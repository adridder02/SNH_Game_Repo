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

        Debug.Log($"[PlayerAbilityInventory] +{amount} {data.displayName} (now {existing.count}).");
        OnChanged?.Invoke();
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
