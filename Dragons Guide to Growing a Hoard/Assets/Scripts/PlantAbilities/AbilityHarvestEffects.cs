using UnityEngine;

// =============================================================
// AbilityHarvestEffects.cs
// -------------------------------------------------------------
// Dispatch point for AbilityKind.OneOff items — these apply their
// effect the INSTANT they're harvested (see PotContents.HarvestPlant)
// and never occupy a PlayerAbilityInventory slot. None of the 16
// currently-designed plants use this kind yet (everything either
// stacks as a Consumable or gets Placed), but the hook exists so a
// future "harvest this and permanently unlock X" plant doesn't need
// a new inventory concept — just a new case here.
// =============================================================
public static class AbilityHarvestEffects
{
    public static void ApplyOneOff(AbilityItemData data, GameObject player)
    {
        if (data == null) return;

        switch (data.effectId)
        {
            // No OneOff effects defined yet — add cases here as new
            // "instant permanent unlock" abilities are designed.
            default:
                Debug.LogWarning($"[AbilityHarvestEffects] '{data.displayName}' is marked OneOff but has no " +
                                  $"handler for effectId '{data.effectId}'. Nothing happened.");
                break;
        }
    }
}
