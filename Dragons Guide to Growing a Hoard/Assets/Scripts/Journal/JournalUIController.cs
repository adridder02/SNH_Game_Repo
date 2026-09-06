using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// =============================================================
// JournalUIController.cs
// -------------------------------------------------------------
// Builds the "Plants" journal page from PlantJournalDatabase +
// PlantJournalManager's discovery state, and shows the clicked
// species' info on the right-hand detail page.
//
// LAYOUT — matches InventoryUIController's grid, not a GridLayoutGroup:
//   slotTemplate is one real slot already placed in your hierarchy.
//   Its RectTransform IS the reference — its sizeDelta becomes every
//   generated slot's size, and its anchoredPosition becomes the
//   top-left starting point each row wraps from (see ManualSlotLayout).
//   Move/resize the template once in the Editor and every species
//   slot follows — there's no separate "cell size" field to keep in
//   sync by hand. Every row RectTransform (sunnyRow/darkRow/waterRow)
//   must use anchor/pivot = top-left (0,1), same convention as the
//   inventory grid.
//
// OPEN/CLOSE — mirrors InventoryUIController:
//   • J key toggles it (polled directly via the new Input System's
//     Keyboard, same pattern PotInteraction uses for E/Q — this
//     doesn't require adding a "Journal" action to a PlayerControls
//     asset. If you'd rather wire it through the same InputAction
//     asset Inventory uses, swap Update()'s key poll for an action
//     callback the same way InventoryUIController does).
//   • journalBackButton closes it.
//   • Escape closes it too — but same as Inventory, that's NOT
//     handled here. InventoryUIController's comment explains why:
//     two scripts independently polling Escape is a race. Add a
//     check for this controller to whatever single script owns
//     Escape for your menu layer (ExitMenuController) — call
//     IsJournalOpen / CloseJournal() there the same way it already
//     calls IsInventoryOpen / CloseInventory().
//
// DETAIL PANEL: no separate "empty state" placeholder — detailPanel
// is just inactive until something's clicked, and goes back to
// inactive whenever the journal opens or closes.
// =============================================================
public class JournalUIController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private PlantJournalDatabase database;
    [Tooltip("Auto-found via PlantJournalManager.Instance if left empty.")]
    [SerializeField] private PlantJournalManager journalManager;

    [Header("Canvas References")]
    [Tooltip("The panel GameObject that gets shown/hidden when toggling the journal (your painted book panel).")]
    [SerializeField] private GameObject journalRoot;
    [Tooltip("Optional close/back button — same role as Inventory's backButton.")]
    [SerializeField] private Button journalBackButton;

    [Header("Top-Level Pages")]
    [Tooltip("The three top-level pages inside the journal. Plants is shown by default every time the " +
             "journal is opened.")]
    [SerializeField] private GameObject plantsPage;
    [SerializeField] private GameObject progressPage;
    [SerializeField] private GameObject guidePage;
    [SerializeField] private GameObject settingsPage;

    [Tooltip("Nav buttons for the four pages above — clicking one shows its matching page and highlights " +
             "itself as active via the button's own Selected sprite/color transition (same trick used for " +
             "the Choose Soil options and the inventory's filter bar — set a distinct Selected state on " +
             "each of these four buttons in the Editor).")]
    [SerializeField] private Button plantsNavButton;
    [SerializeField] private Button progressNavButton;
    [SerializeField] private Button guideNavButton;
    [SerializeField] private Button settingsNavButton;

    [Header("Progress — Room Shortcuts")]
    [Tooltip("The Progress page's own controller — needed so these shortcut buttons can jump straight " +
             "to a room instead of going through its Prev/Next flip sequence. Auto-found on progressPage " +
             "if left empty.")]
    [SerializeField] private ProgressPageUIController progressPageController;
    [Tooltip("Nested sign-post buttons (Main Room / East Wing / West Wing) that switch to the Progress " +
             "page AND jump straight to that room, same idea as plantsNavButton etc. above but one level " +
             "deeper. Prev/Next on the Progress page itself still work exactly as before — these are " +
             "just an extra, more direct way in.")]
    [SerializeField] private Button mainRoomNavButton;
    [SerializeField] private Button eastRoomNavButton;
    [SerializeField] private Button westRoomNavButton;

    [Header("Category Rows")]
    [Tooltip("Container for the Sunny row. Anchor/pivot must be top-left (0,1), same as the inventory grid.")]
    [SerializeField] private RectTransform sunnyRow;
    [SerializeField] private RectTransform darkRow;
    [SerializeField] private RectTransform waterRow;

    [Tooltip("An existing slot GameObject already placed in one of the rows above, with a JournalSlotUI " +
             "component. Hidden at startup and cloned for every species — its RectTransform is also the " +
             "size/position reference every generated slot uses (see ManualSlotLayout).")]
    [SerializeField] private JournalSlotUI slotTemplate;

    [Header("Grid Layout")]
    [Tooltip("Columns per row before wrapping (5 in the mockup).")]
    [SerializeField] private int columns = 5;
    [SerializeField] private float cellGapX = 8f;
    [SerializeField] private float cellGapY = 8f;

    [Header("Detail Panel (right page)")]
    [Tooltip("The whole right-page contents. Inactive by default — there is no separate empty state, " +
             "this is just hidden until a species is clicked.")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private Image detailImage;
    [SerializeField] private TMP_Text detailName;
    [SerializeField] private TMP_Text detailTier;
    [Tooltip("First description block — species.description.")]
    [SerializeField] private TMP_Text detailDescription;
    [Tooltip("Second description block — species.descriptionSecondary. Leave the text empty on the " +
             "species asset if you don't need a second section for that plant.")]
    [SerializeField] private TMP_Text detailDescriptionSecondary;

    [Header("Detail Panel — Prev/Next")]
    [Tooltip("Steps to the previous discovered species within the same category row. Disabled at the start of the row.")]
    [SerializeField] private Button previousSpeciesButton;
    [Tooltip("Steps to the next discovered species within the same category row. Disabled at the end of the row.")]
    [SerializeField] private Button nextSpeciesButton;

    [Header("Difficulty Dots")]
    [Tooltip("Assign all 4 dot Images in order, left to right.")]
    [SerializeField] private Image[] difficultyDots;
    [Tooltip("Optional. If both are assigned, dots swap sprite instead of just dimming.")]
    [SerializeField] private Sprite dotFilledSprite;
    [SerializeField] private Sprite dotEmptySprite;

    [Header("Care Row (display only)")]
    [SerializeField] private TMP_Dropdown waterDropdown;
    [SerializeField] private TMP_Dropdown lightDropdown;
    [SerializeField] private TMP_Dropdown soilDropdown;

    private RectTransform slotTemplateRect;
    private readonly Dictionary<RectTransform, List<JournalSlotUI>> spawnedSlots = new Dictionary<RectTransform, List<JournalSlotUI>>();
    private PlantSpeciesData selectedSpecies;
    private bool isJournalOpen = false;

    public bool IsJournalOpen => isJournalOpen;

    void Awake()
    {
        if (journalManager == null)
            journalManager = PlantJournalManager.Instance != null ? PlantJournalManager.Instance : FindObjectOfType<PlantJournalManager>();

        if (progressPageController == null && progressPage != null)
            progressPageController = progressPage.GetComponent<ProgressPageUIController>();

        if (slotTemplate != null)
        {
            slotTemplateRect = slotTemplate.GetComponent<RectTransform>();
            slotTemplate.gameObject.SetActive(false); // hide the live scene object used as a clone source
        }

        if (journalBackButton != null)
            journalBackButton.onClick.AddListener(ToggleJournal);

        plantsNavButton?.onClick.AddListener(() => ShowPage(plantsPage, plantsNavButton));
        progressNavButton?.onClick.AddListener(() => ShowPage(progressPage, progressNavButton));
        guideNavButton?.onClick.AddListener(() => ShowPage(guidePage, guideNavButton));
        settingsNavButton?.onClick.AddListener(() => ShowPage(settingsPage, settingsNavButton));

        // Room shortcuts: same as clicking Progress, plus jump straight to that room.
        mainRoomNavButton?.onClick.AddListener(() => GoToRoomShortcut(RoomType.Main));
        eastRoomNavButton?.onClick.AddListener(() => GoToRoomShortcut(RoomType.East));
        westRoomNavButton?.onClick.AddListener(() => GoToRoomShortcut(RoomType.West));

        previousSpeciesButton?.onClick.AddListener(() => StepSpecies(-1));
        nextSpeciesButton?.onClick.AddListener(() => StepSpecies(1));

        SetJournalVisible(false);
    }

    /// <summary>Switches to the Progress page (same as progressNavButton) and then jumps straight to
    /// the given room — called by the Main Room/East Wing/West Wing sign-post buttons. Switching the
    /// page first matters: if the Progress page's GameObject was inactive, activating it can trigger
    /// ProgressPageUIController's own OnEnable (which resets to the Main Hall) — calling GoToRoom
    /// afterward ensures the requested room wins regardless.</summary>
    private void GoToRoomShortcut(RoomType room)
    {
        ShowPage(progressPage, progressNavButton);
        progressPageController?.GoToRoom(room);
    }

    void OnEnable()
    {
        if (journalManager != null)
            journalManager.OnJournalChanged += RefreshUI;

        RefreshUI();
    }

    void OnDisable()
    {
        if (journalManager != null)
            journalManager.OnJournalChanged -= RefreshUI;
    }

    void Update()
    {
        // See the class comment re: Escape — that's handled centrally elsewhere,
        // same as Inventory. J is polled directly here (not through a
        // PlayerControls action) so this works without any Input Actions asset changes.
        if (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
            ToggleJournal();
    }

    // ------------------------------------------------------------
    // OPEN / CLOSE
    // ------------------------------------------------------------
    public void ToggleJournal()
    {
        isJournalOpen = !isJournalOpen;
        SetJournalVisible(isJournalOpen);

        if (isJournalOpen)
        {
            GameInputModeManager.Instance?.SetMenuUIMode();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ThirdPersonCameraController.CameraLocked = true;

            RefreshUI();
        }
        else
        {
            GameInputModeManager.Instance?.SetGameplayMode();
        }
    }
    /// <summary>Closes the journal if it's open. Does nothing if already closed. Call this from ExitMenuController on Escape.</summary>
    public void CloseJournal()
    {
        if (isJournalOpen) ToggleJournal();
    }

    private void SetJournalVisible(bool visible)
    {
        if (journalRoot != null) journalRoot.SetActive(visible);

        // No empty state to restore — just make sure the detail page isn't left
        // showing from a previous session.
        selectedSpecies = null;
        if (detailPanel != null) detailPanel.SetActive(false);

        // Plants is always where the journal opens back up to, regardless of
        // whichever page it was left on last time.
        if (visible)
            ShowPage(plantsPage, plantsNavButton);
    }

    // ------------------------------------------------------------
    // TOP-LEVEL PAGES — Plants / Progress / Settings
    // ------------------------------------------------------------
    private void ShowPage(GameObject page, Button navButton)
    {
        if (plantsPage != null) plantsPage.SetActive(page == plantsPage);
        if (progressPage != null) progressPage.SetActive(page == progressPage);
        if (guidePage != null) guidePage.SetActive(page == guidePage);
        if (settingsPage != null) settingsPage.SetActive(page == settingsPage);

        // Highlight the active nav button directly instead of relying on Unity's
        // EventSystem "Selected" state — that state is global and transient, so it
        // gets cleared the instant focus moves to anything else (a slot, a dropdown,
        // empty space), which is why the active tab was deactivating on any other
        // click. We reuse whatever Selected sprite/color you already set up per
        // button in the Editor, just applied manually and left in place until the
        // page changes again.
        SetNavButtonActive(plantsNavButton, navButton == plantsNavButton);
        SetNavButtonActive(progressNavButton, navButton == progressNavButton);
        SetNavButtonActive(guideNavButton, navButton == guideNavButton);
        SetNavButtonActive(settingsNavButton, navButton == settingsNavButton);
    }

    /// <summary>Forces a nav button's visual into (or out of) its configured Selected
    /// look, independent of EventSystem focus, so it stays highlighted for as long as
    /// its page is open — even after the user clicks elsewhere.</summary>
    private static void SetNavButtonActive(Button navButton, bool active)
    {
        if (navButton == null) return;

        switch (navButton.transition)
        {
            case Selectable.Transition.SpriteSwap:
                if (navButton.image != null)
                    navButton.image.overrideSprite = active ? navButton.spriteState.selectedSprite : null;
                break;

            case Selectable.Transition.ColorTint:
                if (navButton.targetGraphic != null)
                    navButton.targetGraphic.color = active ? navButton.colors.selectedColor : navButton.colors.normalColor;
                break;

            default:
                Debug.LogWarning($"[JournalUIController] {navButton.name} uses a Transition type " +
                    "(Animation/None) that SetNavButtonActive doesn't handle — switch it to Sprite Swap " +
                    "or Color Tint so the active nav highlight can be applied.");
                break;
        }
    }

    // ------------------------------------------------------------
    // GRID
    // ------------------------------------------------------------
    public void RefreshUI()
    {
        if (database == null || slotTemplate == null)
        {
            Debug.LogWarning("[JournalUIController] Missing database or slot template — check the Inspector.");
            return;
        }

        PopulateRow(sunnyRow, PlantType.Sunny);
        PopulateRow(darkRow, PlantType.Dark);
        PopulateRow(waterRow, PlantType.Water);
    }

    private void PopulateRow(RectTransform row, PlantType category)
    {
        if (row == null) return;

        if (!spawnedSlots.TryGetValue(row, out List<JournalSlotUI> existing))
        {
            existing = new List<JournalSlotUI>();
            spawnedSlots[row] = existing;
        }
        foreach (JournalSlotUI old in existing)
            if (old != null) Destroy(old.gameObject);
        existing.Clear();

        List<PlantSpeciesData> speciesInCategory = database.GetByCategory(category);

        // Only discovered species get a slot at all — undiscovered ones don't show
        // up as a locked/greyed placeholder, they simply aren't in the grid yet
        // until the player actually harvests one for the first time. slotIndex is
        // tracked separately from the loop index so the visible slots still pack
        // together with no gaps left behind by the ones that were skipped.
        int slotIndex = 0;

        for (int i = 0; i < speciesInCategory.Count; i++)
        {
            PlantSpeciesData species = speciesInCategory[i];
            bool unlocked = journalManager != null && journalManager.IsDiscovered(species);
            if (!unlocked) continue;

            JournalSlotUI slot = Instantiate(slotTemplate, row);
            slot.gameObject.SetActive(true);

            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.anchoredPosition = ManualSlotLayout.GetPosition(slotTemplateRect, slotIndex, columns, cellGapX, cellGapY);
            slotIndex++;

            slot.Initialize(species, true, this);
            existing.Add(slot);
        }
    }

    // ------------------------------------------------------------
    // DETAIL PAGE — called by JournalSlotUI.OnPointerClick
    // ------------------------------------------------------------
    public void ShowSpeciesDetail(PlantSpeciesData species)
    {
        if (species == null) return;
        selectedSpecies = species;

        if (detailPanel != null) detailPanel.SetActive(true);

        if (detailImage != null)
        {
            // journalImage is the larger detail illustration — falls back to the
            // small grid icon so the page isn't blank if none was assigned.
            Sprite img = species.journalImage != null ? species.journalImage : species.journalIcon;
            detailImage.sprite = img;
            detailImage.enabled = img != null;
        }

        if (detailName != null) detailName.text = species.displayName;
        if (detailTier != null) detailTier.text = $"Tier {species.tier}";
        if (detailDescription != null) detailDescription.text = species.description;
        if (detailDescriptionSecondary != null) detailDescriptionSecondary.text = species.descriptionSecondary;

        RefreshDifficultyDots(species.difficulty);
        RefreshCareRow(species);
        RefreshSpeciesNavButtons();
    }

    // ------------------------------------------------------------
    // PREV / NEXT — steps between discovered species in the same category row
    // ------------------------------------------------------------
    private void StepSpecies(int direction)
    {
        if (selectedSpecies == null || database == null) return;

        List<PlantSpeciesData> navigable = GetNavigableSpecies(selectedSpecies.category);
        int currentIndex = navigable.IndexOf(selectedSpecies);
        if (currentIndex < 0) return;

        int newIndex = currentIndex + direction;
        Debug.Log($"[StepSpecies] dir={direction} current={currentIndex} new={newIndex} count={navigable.Count} selected={selectedSpecies.displayName}");

        if (newIndex < 0 || newIndex >= navigable.Count) return; // at an end — buttons are disabled here anyway

        ShowSpeciesDetail(navigable[newIndex]);
    }

    private void RefreshSpeciesNavButtons()
    {
        if (selectedSpecies == null) return;

        List<PlantSpeciesData> navigable = GetNavigableSpecies(selectedSpecies.category);
        int currentIndex = navigable.IndexOf(selectedSpecies);

        bool hasPrevious = currentIndex > 0;
        bool hasNext = currentIndex >= 0 && currentIndex < navigable.Count - 1;

        // Hidden (not just disabled) at either end of the row, per design.
        if (previousSpeciesButton != null) previousSpeciesButton.gameObject.SetActive(hasPrevious);
        if (nextSpeciesButton != null) nextSpeciesButton.gameObject.SetActive(hasNext);
    }

    /// <summary>Species in this category the player has actually discovered, in the same order the grid uses — undiscovered ones have nothing to show, so they're skipped rather than stepped onto.</summary>
    private List<PlantSpeciesData> GetNavigableSpecies(PlantType category)
    {
        List<PlantSpeciesData> categoryList = database.GetByCategory(category);
        return categoryList.FindAll(s => journalManager != null && journalManager.IsDiscovered(s));
    }

    private void RefreshDifficultyDots(int difficulty)
    {
        if (difficultyDots == null) return;

        for (int i = 0; i < difficultyDots.Length; i++)
        {
            if (difficultyDots[i] == null) continue;
            bool filled = i < difficulty;

            if (dotFilledSprite != null && dotEmptySprite != null)
                difficultyDots[i].sprite = filled ? dotFilledSprite : dotEmptySprite;
            else
                difficultyDots[i].color = filled ? Color.white : new Color(1f, 1f, 1f, 0.25f);
        }
    }

    private void RefreshCareRow(PlantSpeciesData species)
    {
        SetDropdownValue(waterDropdown, species.waterRequirement.ToString());
        SetDropdownValue(lightDropdown, species.lightRequirement.ToString());
        SetDropdownValue(soilDropdown, species.preferredSoilDisplay.ToString());
    }

    private static void SetDropdownValue(TMP_Dropdown dropdown, string value)
    {
        if (dropdown == null) return;
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string> { value });
        dropdown.value = 0;
        dropdown.RefreshShownValue();
        dropdown.interactable = false;
    }
}