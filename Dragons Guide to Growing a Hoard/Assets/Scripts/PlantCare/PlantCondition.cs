using UnityEngine;

// =============================================================
// PlantCondition.cs
// -------------------------------------------------------------
// A plant's "saved state" — travels with it across pot removal -> inventory -> replanting
// (PotContents.RemovePlant/AddPlant, PlayerInventory.AddPlantToInventory,
// InventoryItemInstance.condition), and is also what a harvest node's designer-set starting
// condition uses (CollectablePlant.startingCondition).
//
//   - isPermanentlyDead: a ONE-WAY flag. Once true, PlantState locks CalculateState() to always
//     return Dead, no matter how good soil/light/water get afterward — see
//     PlantState.IsPermanentlyDead and its permanentDeathDelay field (how long a plant has to sit
//     in the LIVE Dead state before this flag gets set).
//   - startingHealth01: cosmetic only — what the health bar/state shows the MOMENT a plant is
//     (re)planted, before PlantState's normal live recalculation (soil/light/water, every Update())
//     takes over on the very next tick. NOT a lasting guarantee the plant stays at this health —
//     replanting into a much better/worse pot immediately starts drifting from it, same as any
//     other plant. This is the "looks half-dead right when you plant it" piece, not a frozen value.
// =============================================================
[System.Serializable]
public class PlantCondition
{
    public bool isPermanentlyDead;

    [Range(0f, 1f)]
    public float startingHealth01 = 1f;

    /// <summary>A fresh, fully healthy plant — the default for anything that doesn't explicitly
    /// carry a saved/preset condition (e.g. a plant instantiated directly in the scene rather than
    /// through the pickup/plant pipeline).</summary>
    public static PlantCondition Healthy => new PlantCondition { isPermanentlyDead = false, startingHealth01 = 1f };
}
