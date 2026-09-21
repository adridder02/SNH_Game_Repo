using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// =============================================================
// JournalSlotUI.cs
// -------------------------------------------------------------
// One slot in a journal category row. Deliberately simpler than
// InventorySlotUI — no drag-and-drop, just click-to-select — since
// journal entries never move around.
//
// PREFAB SETUP (same "hidden template, cloned at runtime" pattern
// as the inventory's gridSlotTemplate):
//   1. Build one slot GameObject in your category row with an Image
//      for the icon and this component attached.
//   2. Optionally add a second Image/GameObject as the "locked"
//      visual (greyed square, "?" icon, whatever matches your art)
//      and assign it to lockedOverlay.
//   3. Assign that GameObject to JournalUIController.slotTemplate.
//      It gets hidden at startup and cloned per species.
// =============================================================
[RequireComponent(typeof(RectTransform))]
public class JournalSlotUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Wire these up on the slot")]
    [SerializeField] private Image icon;
    [Tooltip("Optional. Shown instead of the icon while the species is undiscovered — a grey square, " +
             "a '?' sprite, whatever matches your book art. Leave empty if the icon itself already " +
             "handles the locked look (e.g. icon.enabled = false is enough).")]
    [SerializeField] private GameObject lockedOverlay;

    [Tooltip("Small 'new!' badge shown on this slot until the player opens this species' page for the " +
             "first time after discovering it. Optional — leave unassigned if a given slot prefab " +
             "doesn't need one.")]
    [SerializeField] private GameObject newBadge;

    public PlantSpeciesData Species { get; private set; }
    public bool IsUnlocked { get; private set; }

    private JournalUIController controller;

    void OnEnable()
    {
        if (NewItemTracker.Instance != null)
            NewItemTracker.Instance.OnChanged += RefreshNewBadge;
        RefreshNewBadge();
    }

    void OnDisable()
    {
        if (NewItemTracker.Instance != null)
            NewItemTracker.Instance.OnChanged -= RefreshNewBadge;
    }

    /// <param name="unlocked">Whether the player has discovered this species yet.</param>
    public void Initialize(PlantSpeciesData species, bool unlocked, JournalUIController owningController)
    {
        Species = species;
        IsUnlocked = unlocked;
        controller = owningController;

        if (icon != null)
        {
            Sprite sprite = unlocked && species != null ? species.journalIcon : null;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        if (lockedOverlay != null)
            lockedOverlay.SetActive(!unlocked);

        RefreshNewBadge();
    }

    private void RefreshNewBadge()
    {
        if (newBadge == null) return;

        bool isNew = IsUnlocked && Species != null && NewItemTracker.Instance != null &&
                     NewItemTracker.Instance.IsUnseenInJournal(Species.ResolvedId);

        newBadge.SetActive(isNew);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Locked slots don't open the detail page — nothing to show yet.
        if (!IsUnlocked || Species == null || controller == null) return;

        NewItemTracker.Instance?.MarkSeenInJournal(Species.ResolvedId);
        controller.ShowSpeciesDetail(Species);
    }
}