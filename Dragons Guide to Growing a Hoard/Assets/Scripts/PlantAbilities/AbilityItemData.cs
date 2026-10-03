using UnityEngine;

// =============================================================
// AbilityItemData.cs
// -------------------------------------------------------------
// SINGLE SOURCE OF TRUTH for every "harvested ability item" a plant
// can yield — Pollen Puffs, Sparkmint Leaves, Waterbells, Glowcap
// Spores, etc. One asset per item, all data-driven (mirrors the
// PlantSpeciesData / PlantSizeRegistry pattern already used across
// this project) so adding a new ability is "create an asset + pick
// an AbilityEffectId", not "write a new ScriptableObject subclass".
//
// THREE KINDS (per the design brief):
//   OneOff      — granted once, applies a permanent/toggleable passive
//                 the moment it's picked up. No inventory footprint.
//   Consumable  — sits in the ability inventory (PlayerAbilityInventory)
//                 as a stack. Player selects it, then targets something
//                 (a pot, the soil, themselves, or nothing) to use it.
//   Placeable   — sits in the ability inventory as a stack. Player
//                 selects it, then places an instance of it on the
//                 grid (via AbilityPlacementSystem) exactly like a pot.
//
// EFFECT DISPATCH:
//   AbilityEffectId is the "which piece of gameplay code actually runs"
//   selector. AbilityConsumableEffects.cs / the individual placeable
//   MonoBehaviours (Placeables/*.cs) switch on it. Numeric tuning
//   (radius, duration, amount, etc.) lives on this asset in the
//   generically-named fields below so most new abilities need ZERO
//   new code — just a new enum entry + an asset.
//
// SETUP:
//   Assets → Create → Greenhouse → Ability Item
// =============================================================
public enum AbilityKind
{
    OneOff,
    Consumable,
    Placeable
}

// Selects which behaviour actually runs for this item. Add an entry here
// whenever a genuinely new mechanic is needed; reuse an existing one +
// different tuning numbers whenever possible.
public enum AbilityEffectId
{
    None,

    // --- Consumables ---
    PollenCloud,          // Pollen Puff — reveals a pot's most-needed stat via colour
    ExpandInventory,       // Bubble Blossom — unlocks one more inventory slot
    SoilMiasmaWard,        // Verdant Algae — soil temporarily immune to miasma decay
    DragonGlow,             // Glowcap — player glows for a duration
    WaterIndicatorTag,      // Dewdrop — attaches a live water-level gizmo readout to a pot

    // --- Placeables ---
    SparkmintLeafFence,     // Sparkmint — closed-circuit miasma ward
    WaterbellSprinkler,     // Waterbell — auto-maintains water level on adjacent plants
}

[CreateAssetMenu(fileName = "AbilityItem", menuName = "Greenhouse/Ability Item")]
public class AbilityItemData : ScriptableObject
{
    [Header("Identity")]
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Kind / Effect")]
    public AbilityKind kind = AbilityKind.Consumable;
    public AbilityEffectId effectId = AbilityEffectId.None;

    [Tooltip("ON for key items that only do something when used on a specific world object other " +
             "than a pot (e.g. the tree's crystal-heart items) — keeps them off the hotbar (see " +
             "AbilityHotbarSystem.CanAssign) the same way RequiresPotTarget keeps pot-targeted " +
             "Consumables off it, since activating them there would just silently do nothing. " +
             "They still sit normally in PlayerAbilityInventory; whatever script drives that other " +
             "object (e.g. a tree interaction script) reads/consumes them directly.")]
    public bool requiresTreeTarget = false;

    [Header("Stacking")]
    [Tooltip("How many of this item a single harvest grants. Sparkmint gives 10 leaves per plant, most others give 1.")]
    public int harvestGrantAmount = 1;

    [Tooltip("Max this item can stack to in the ability inventory. 0 = unlimited.")]
    public int maxStack = 0;

    [Header("Use Cooldown")]
    [Tooltip("Minimum seconds between activations of this specific item via the hotbar (see " +
             "AbilityHotbarSystem.ActivateSlot). Stops a player from chain-spamming a stacked " +
             "Consumable via rapid clicking/mashing — stock count alone doesn't prevent that, since " +
             "mouse clicks aren't naturally rate-limited the way a single key press is. 0 = no cooldown.")]
    public float useCooldown = 0.35f;

    [Header("Placement (Placeable kind only)")]
    [Tooltip("Grid footprint used by AbilityPlacementSystem — same footprint concept as PlantSizeRegistry/PotData.")]
    public Vector2Int footprint = Vector2Int.one;

    [Tooltip("Prefab spawned on the grid when placed. Leave empty for now — AbilityPlaceable components " +
             "draw their own OnDrawGizmos placeholder, so an empty GameObject with the right component is fine.")]
    public GameObject placedPrefab;

    [Header("Generic Tuning")]
    [Tooltip("General-purpose float used differently per effect — see the effect's own comment for what it means " +
             "(e.g. duration in seconds, drain multiplier, radius). Keeps most new abilities data-only.")]
    public float amountA = 0f;

    [Tooltip("Second general-purpose float, same idea as amountA.")]
    public float amountB = 0f;

    [Tooltip("Third general-purpose float, same idea as amountA.")]
    public float amountC = 0f;
}