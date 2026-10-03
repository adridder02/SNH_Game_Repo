using UnityEngine;

// =============================================================
// AbilityConsumableEffects.cs
// -------------------------------------------------------------
// Dispatch point for AbilityKind.Consumable items. Called from
// PotMenuUIController's Abilities panel (see PotMenuUIController.cs
// edit) once the player has picked an item and (if it needs one) a
// target pot. Consuming from PlayerAbilityInventory happens in the
// CALLER, only after TryApply returns true — so a failed apply (e.g.
// no plant in the target pot) never costs the player the item.
// =============================================================
public static class AbilityConsumableEffects
{
    /// <summary>Whether this effect needs a target pot to be selected before it can be used.
    /// Read by PotMenuUIController's Abilities panel to decide whether an item is usable
    /// right now (targeted effects require currentPot.HasPlant).</summary>
    public static bool RequiresPotTarget(AbilityEffectId effectId)
    {
        switch (effectId)
        {
            case AbilityEffectId.PollenCloud:
            case AbilityEffectId.SoilMiasmaWard:
            case AbilityEffectId.WaterIndicatorTag:
                return true;
            default:
                return false;
        }
    }

    public static bool TryApply(AbilityItemData data, GameObject player, PotContents targetPot)
    {
        if (data == null) return false;

        switch (data.effectId)
        {
            case AbilityEffectId.PollenCloud:
                {
                    if (targetPot == null || targetPot.Plant == null) return false;
                    float duration = data.amountA > 0f ? data.amountA : 20f;
                    PotNeedIndicator.AttachTo(targetPot, duration, data.placedPrefab, data);
                    return true;
                }

            case AbilityEffectId.ExpandInventory:
                {
                    if (player == null) return false;
                    PlayerInventory inv = player.GetComponent<PlayerInventory>();
                    if (inv == null) return false;

                    // amountB = optional max height cap (0 = uncapped). amountA reserved for a future
                    // "expand by more than one row at a time" tuning; unused for now (always +1).
                    int newHeight = inv.GridHeight + 1;
                    if (data.amountB > 0f && newHeight > (int)data.amountB) return false;

                    inv.ExpandGrid(inv.GridWidth, newHeight);
                    Debug.Log($"[AbilityConsumableEffects] Inventory expanded to {inv.GridWidth}x{newHeight}.");
                    return true;
                }

            case AbilityEffectId.SoilMiasmaWard:
                {
                    if (targetPot == null || targetPot.Plant == null) return false;
                    float duration = data.amountA > 0f ? data.amountA : 60f;

                    // Swaps the soil to its algae-variant material for the duration, reverting
                    // automatically when the ward ends (naturally or otherwise) via OnEnded —
                    // PotContents.SetAlgaeActive is idempotent, so this is safe even if called
                    // redundantly.
                    targetPot.SetAlgaeActive(true);
                    TimedMiasmaWard.ApplyTo(targetPot.Plant, duration, data, () => targetPot.SetAlgaeActive(false));
                    return true;
                }

            case AbilityEffectId.DragonGlow:
                {
                    if (player == null) return false;
                    float duration = data.amountA > 0f ? data.amountA : 30f;
                    DragonGlowEffect.ApplyTo(player, duration, new Color(0.6f, 1f, 0.7f));
                    return true;
                }

            case AbilityEffectId.WaterIndicatorTag:
                {
                    if (targetPot == null) return false;
                    PotWaterGizmo.AttachTo(targetPot, data);
                    return true;
                }

            default:
                Debug.LogWarning($"[AbilityConsumableEffects] '{data.displayName}' has no handler for " +
                                  $"effectId '{data.effectId}'.");
                return false;
        }
    }

    /// <summary>Read by PotMenuUIController to show a single icon (same sprite Inventory shows for
    /// that item) representing whichever pot-targeted consumable is CURRENTLY active on this pot —
    /// mirrors mainSoilIcon's role for soil. Checks all three pot-targeted effect types; if more
    /// than one happens to be active at once, returns the first found in this priority order
    /// (Water, Pollen, Miasma Ward) rather than showing several at once. Returns null if none are
    /// active, or if the active one's source AbilityItemData wasn't captured (shouldn't happen for
    /// anything applied through TryApply above, but a pot could theoretically have one of these
    /// components added some other way).</summary>
    public static Sprite GetActiveConsumableIcon(PotContents pot)
    {
        if (pot == null) return null;

        PotWaterGizmo water = pot.GetComponent<PotWaterGizmo>();
        if (water != null && water.sourceData != null) return water.sourceData.icon;

        PotNeedIndicator pollen = pot.GetComponentInChildren<PotNeedIndicator>();
        if (pollen != null && pollen.sourceData != null) return pollen.sourceData.icon;

        TimedMiasmaWard ward = pot.Plant != null ? pot.Plant.GetComponentInChildren<TimedMiasmaWard>() : null;
        if (ward != null && ward.sourceData != null) return ward.sourceData.icon;

        return null;
    }
}