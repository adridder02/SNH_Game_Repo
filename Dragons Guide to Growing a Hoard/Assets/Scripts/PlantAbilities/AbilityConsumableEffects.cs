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
                    PotNeedIndicator.AttachTo(targetPot, duration, data.placedPrefab);
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
                    TimedMiasmaWard.ApplyTo(targetPot.Plant, duration);
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
                    PotWaterGizmo.AttachTo(targetPot);
                    return true;
                }

            default:
                Debug.LogWarning($"[AbilityConsumableEffects] '{data.displayName}' has no handler for " +
                                  $"effectId '{data.effectId}'.");
                return false;
        }
    }
}