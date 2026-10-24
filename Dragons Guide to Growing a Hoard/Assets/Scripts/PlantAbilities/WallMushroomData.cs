using UnityEngine;

// =============================================================
// WallMushroomData.cs
// -------------------------------------------------------------
// One asset per Clovenwick variant (small wall-crawlers vs the big
// weight-bearing ones). Mirrors PotData's role for WallPlacementSystem
// the same way PotData does for PlacementSystem — deliberately its
// own asset type rather than folded into AbilityItemData, since these
// aren't harvested-ability items (per the brief: "doesn't have
// abilities per say") — they're a straightforward placement catalogue,
// same category as pots.
//
// SETUP: Assets → Create → Greenhouse → Wall Mushroom
// =============================================================
[CreateAssetMenu(fileName = "WallMushroom", menuName = "Greenhouse/Wall Mushroom")]
public class WallMushroomData : ScriptableObject
{
    public string displayName;
    public Vector2Int size = Vector2Int.one;
    public GameObject prefab;
    public GameObject previewPrefab;

    [Tooltip("Icon shown in the wall-mushroom selector HUD (MainUIController) while this is the " +
             "active type in Wall Placing mode.")]
    public Sprite icon;

    [Tooltip("Roughly how much weight this can support standing on it — 'the big ones can hold an " +
             "average basset hound or a puppy St. Bernard'. Not simulated yet; exposed so a future " +
             "creature-weight system has something to check against.")]
    public float maxSupportedWeightKg = 15f;

    [Header("Inventory item (what the player spends to place one)")]
    [Tooltip("The ability-inventory item this mount is placed FROM - the mature Clovenwick 'Shelf' " +
             "consumable you get by harvesting a fully grown Clovenwick. Placing one uses up one of " +
             "them; removing a placed one gives it back (see Refund On Remove). LEAVE EMPTY = this " +
             "type can't be placed on a wall at all (only mature, weight-bearing Clovenwicks go on " +
             "walls - the growing plants can only be planted in pots).")]
    public AbilityItemData requiredItem;

    [Tooltip("ON = taking a placed one back off the wall returns the item to the player's inventory.")]
    public bool refundOnRemove = true;

    [Header("Placement offset")]
    [Tooltip("Pushes this mount OUT from the wall along the wall's facing direction (world units), " +
             "so it can sit a bit further from the wall than the grid origin puts it. Negative pulls " +
             "it into the wall. Stacks with the Placement Offset on each WallSurface.")]
    public float wallOffset = 0f;

    [Tooltip("Fine nudge within the wall's plane: X = along the wall (towards +X / +Z), Y = up. World units. " +
             "Handy when the prefab's pivot isn't at the centre of the model.")]
    public Vector2 planeNudge = Vector2.zero;
}
