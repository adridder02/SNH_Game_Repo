// =============================================================
// AbilityItemInstance.cs
// -------------------------------------------------------------
// A stack of one AbilityItemData in the player's ability inventory.
// Deliberately simple (no grid position like InventoryItemInstance
// has for plants) — ability items are a flat stacking list for now,
// same treatment PlayerInventory gives its "Available" overflow.
// =============================================================
[System.Serializable]
public class AbilityItemInstance
{
    public AbilityItemData data;
    public int count;

    public AbilityItemInstance(AbilityItemData data, int count)
    {
        this.data = data;
        this.count = count;
    }
}
