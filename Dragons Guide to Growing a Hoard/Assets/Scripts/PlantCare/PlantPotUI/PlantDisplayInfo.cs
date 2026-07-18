using UnityEngine;

// =============================================================
// PlantDisplayInfo.cs
// -------------------------------------------------------------
// Plant prefabs are 3D models with no UI sprite of their own — the
// name/images the player sees in the inventory (InventoryItemInstance.
// displayName / icon / displayImage) only exist while the plant is still
// an item. Once PotMenuUIController plants it (see ChoosePlant()), that
// info gets copied onto this small tag component on the planted
// GameObject, so both the pot's Main panel AND PotContents.RemovePlant()
// (when the plant goes back to the inventory) can still show/restore the
// right name and pictures without needing PotContents.cs to know
// anything about the inventory system itself.
// =============================================================
public class PlantDisplayInfo : MonoBehaviour
{
    public string displayName;
    public Sprite icon;
    public Sprite displayImage;
}