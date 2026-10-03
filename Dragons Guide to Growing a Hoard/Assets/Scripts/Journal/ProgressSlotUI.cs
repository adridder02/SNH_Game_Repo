using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================
// ProgressSlotUI.cs
// -------------------------------------------------------------
// One icon in a Progress-page room grid. Same "hidden template,
// cloned at runtime" pattern as JournalSlotUI — this is simpler
// again, though: no click-to-open-detail here, this page is just a
// visual "how much of this room have I found" display. Unlike the
// Plants page grid, EVERY species in the room gets a slot, discovered
// or not; undiscovered ones just show the disabled icon instead of
// being skipped.
//
// PREFAB SETUP (same pattern as JournalSlotUI/InventorySlotUI):
//   1. Build one slot GameObject in a room's icon row with an Image
//      for the icon and this component attached.
//   2. Optionally add a TMP_Text under it for the "???"/name label
//      shown in the mockup and assign it to nameLabel. Leave empty
//      if you don't want a label under the icon.
//   3. Assign that GameObject to that room's slotTemplate on
//      ProgressPageUIController. It gets hidden at startup and cloned
//      per species, same as the journal grid.
//
// GOLD STATE: Initialize() now picks Gold whenever the species has hit
// its completion milestone (PlantJournalManager.IsCompleted), sourced
// from PlantProgress reaching IsComplete for the first time. SetState()
// stays public so anything else that needs to force a state change later
// still can.
// =============================================================
public enum ProgressIconState
{
    Disabled,
    Active,
    Gold
}

[RequireComponent(typeof(RectTransform))]
public class ProgressSlotUI : MonoBehaviour
{
    [Header("Wire these up on the slot")]
    [SerializeField] private Image icon;
    [Tooltip("Optional. Shows the species name once discovered, or '???' while it's still locked " +
             "(matches the mockup). Leave empty if this room's slots don't need a label.")]
    [SerializeField] private TMP_Text nameLabel;

    public PlantSpeciesData Species { get; private set; }
    public ProgressIconState State { get; private set; }

    /// <param name="unlocked">Whether the player has discovered this species yet.</param>
    /// <param name="completed">Whether the species has reached its gold/fully-ripened milestone at least
    /// once (see PlantJournalManager.IsCompleted). Takes priority over unlocked — a completed species is
    /// always also discovered, so this alone decides Gold vs Active vs Disabled.</param>
    public void Initialize(PlantSpeciesData species, bool unlocked, bool completed)
    {
        Species = species;
        SetState(completed ? ProgressIconState.Gold : (unlocked ? ProgressIconState.Active : ProgressIconState.Disabled));
    }

    /// <summary>Swaps the icon (and label) to match the given state. Public so a future progression
    /// system can call SetState(ProgressIconState.Gold) directly once that's ready — no other changes
    /// needed here when that day comes.</summary>
    public void SetState(ProgressIconState state)
    {
        State = state;

        if (icon != null && Species != null)
        {
            Sprite sprite = state switch
            {
                ProgressIconState.Active => Species.activeIcon,
                ProgressIconState.Gold => Species.goldIcon,
                _ => Species.disabledIcon
            };
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        if (nameLabel != null)
            nameLabel.text = state == ProgressIconState.Disabled ? "???" : Species?.displayName;
    }
}