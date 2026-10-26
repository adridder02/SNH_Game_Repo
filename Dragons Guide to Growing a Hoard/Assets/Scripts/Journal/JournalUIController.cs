using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
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
    [Tooltip("The main HUD controller — used to hide the HUD (bars/hotbar/icons) while the journal is " +
             "open and show it again on close. Auto-found in the scene if left empty.")]
    [SerializeField] private MainUIController mainUI;

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
    [Tooltip("The Lore bookmark. Opens the SAME page as the Guide bookmark, showing the Lore list instead " +
             "(see GuideUIController.SetMode). Set a Selected sprite/colour on it like the other nav buttons.")]
    [SerializeField] private Button loreNavButton;
    [Tooltip("The GuideUIController on the guide page. Auto-found on guidePage if left empty.")]
    [SerializeField] private GuideUIController guideController;
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

    [Header("Detail Panel - Alternate Layout (optional)")]
    [Tooltip("Used INSTEAD of the matching default element above when the species has 'Use Alternate Journal " +
             "Layout' ticked (e.g. the image sitting beside the first paragraph rather than between the two). " +
             "Leave any of these empty to keep using the default element for that part.")]
    [SerializeField] private Image detailImageAlt;
    [SerializeField] private TMP_Text detailDescriptionAlt;
    [SerializeField] private TMP_Text detailDescriptionSecondaryAlt;

    [Tooltip("Shown in place of a description block that isn't unlocked yet (Tier 2/3's gradual " +
             "unlock — see GetUnlockedDescriptions) instead of leaving it blank, so the layout " +
             "doesn't collapse to nothing while the player's still working toward unlocking it.")]
    [SerializeField] private string lockedPlaceholderText = "???";

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

    [Header("Care Row (player fills these in)")]
    [Tooltip("Each dropdown starts on '?' and offers every value. Picks are saved per species.")]
    [SerializeField] private TMP_Dropdown waterDropdown;
    [SerializeField] private TMP_Dropdown lightDropdown;
    [SerializeField] private TMP_Dropdown soilDropdown;
    [Tooltip("Optional. ONE button that toggles the open species' answers: click to reveal the correct answers " +
             "and lock the dropdowns, click again to unlock and get your own picks back.")]
    [SerializeField] private Button fillAnswersButton;
    [Tooltip("Sprite shown on the button while the answers are revealed / locked.")]
    [SerializeField] private Sprite lockedSprite;
    [Tooltip("Sprite shown on the button while the answers are hidden / unlocked.")]
    [SerializeField] private Sprite unlockedSprite;
    [Tooltip("The Image the lock / unlock sprite is put on. Leave empty to use the button's own image.")]
    [SerializeField] private Image fillAnswersIcon;
    [Tooltip("Optional. Shown (e.g. a padlock icon) while the open species' answers are locked.")]
    [SerializeField] private GameObject answersLockedIndicator;
    [Tooltip("Text for the unanswered first option.")]
    [SerializeField] private string unansweredLabel = "?";

    private static readonly SoilKind[] SoilChoices = { SoilKind.Clay, SoilKind.Loam, SoilKind.Sandy, SoilKind.Water };
    private bool refreshingCare;

    private RectTransform slotTemplateRect;
    private readonly Dictionary<RectTransform, List<JournalSlotUI>> spawnedSlots = new Dictionary<RectTransform, List<JournalSlotUI>>();
    private PlantSpeciesData selectedSpecies;
    private bool isJournalOpen = false;

    public bool IsJournalOpen => isJournalOpen;

    void Awake()
    {
        if (journalManager == null)
            journalManager = PlantJournalManager.Instance != null ? PlantJournalManager.Instance : FindAnyObjectByType<PlantJournalManager>();

        if (mainUI == null)
            mainUI = FindAnyObjectByType<MainUIController>();

        if (progressPageController == null && progressPage != null)
            progressPageController = progressPage.GetComponent<ProgressPageUIController>();

        if (guideController == null && guidePage != null)
            guideController = guidePage.GetComponentInChildren<GuideUIController>(true);

        if (slotTemplate != null)
        {
            slotTemplateRect = slotTemplate.GetComponent<RectTransform>();
            slotTemplate.gameObject.SetActive(false); // hide the live scene object used as a clone source
        }

        if (journalBackButton != null)
            journalBackButton.onClick.AddListener(() =>
            {
                if (TutorialSequenceController.Instance != null && TutorialSequenceController.Instance.IsBackBlocked) return;
                ToggleJournal();
            });

        SetupCareDropdown(waterDropdown, new List<string> { "Low", "Medium", "High" });
        SetupCareDropdown(lightDropdown, new List<string> { "Low", "Medium", "High" });
        SetupCareDropdown(soilDropdown, SoilChoices.Select(k => k.ToString()).ToList());
        waterDropdown?.onValueChanged.AddListener(_ => OnCareDropdownChanged());
        lightDropdown?.onValueChanged.AddListener(_ => OnCareDropdownChanged());
        soilDropdown?.onValueChanged.AddListener(_ => OnCareDropdownChanged());
        fillAnswersButton?.onClick.AddListener(() => { if (HudLockedFor(JournalAction.CareAnswers)) return; OnFillAnswersClicked(); });

        // Tutorial hooks on the three bookmark tabs — matches steps reading 'Open the "Plants"/
        // "Progress"/"Guide" bookmark'. Fired from the click listener itself rather than from inside
        // ShowPage() (which also runs on journal-open auto-reset-to-Plants and the room-shortcut
        // buttons below), so these only fire on an actual, deliberate tab click.
        plantsNavButton?.onClick.AddListener(() =>
        {
            if (BlockedExcept("opened_plants_bookmark", JournalAction.Bookmarks)) return;
            GameAudio.Play(SoundId.OpenBookmarks);
            ShowPage(plantsPage, plantsNavButton);
            TutorialSequenceController.Instance?.NotifyExternalTrigger("opened_plants_bookmark");
        });
        progressNavButton?.onClick.AddListener(() =>
        {
            if (BlockedExcept("opened_progress_bookmark", JournalAction.Bookmarks)) return;
            GameAudio.Play(SoundId.OpenBookmarks);
            ShowPage(progressPage, progressNavButton);
            TutorialSequenceController.Instance?.NotifyExternalTrigger("opened_progress_bookmark");
        });
        guideNavButton?.onClick.AddListener(() =>
        {
            if (BlockedExcept("opened_guide_bookmark", JournalAction.Bookmarks)) return;
            GameAudio.Play(SoundId.OpenBookmarks);
            guideController?.SetMode(GuideUIController.GuideMode.Guide);
            ShowPage(guidePage, guideNavButton);
            TutorialSequenceController.Instance?.NotifyExternalTrigger("opened_guide_bookmark");
        });
        loreNavButton?.onClick.AddListener(() =>
        {
            if (BlockedExcept("opened_lore_bookmark", JournalAction.Bookmarks)) return;
            GameAudio.Play(SoundId.OpenBookmarks);
            guideController?.SetMode(GuideUIController.GuideMode.Lore);
            ShowPage(guidePage, loreNavButton);
            TutorialSequenceController.Instance?.NotifyExternalTrigger("opened_lore_bookmark");
        });
        settingsNavButton?.onClick.AddListener(() => { if (HudLockedFor(JournalAction.Settings)) return; ShowPage(settingsPage, settingsNavButton); });

        // Room shortcuts: same as clicking Progress, plus jump straight to that room.
        mainRoomNavButton?.onClick.AddListener(() => { if (HudLockedFor(JournalAction.RoomShortcuts)) return; GoToRoomShortcut(RoomType.Main); });
        eastRoomNavButton?.onClick.AddListener(() => { if (HudLockedFor(JournalAction.RoomShortcuts)) return; GoToRoomShortcut(RoomType.East); });
        westRoomNavButton?.onClick.AddListener(() => { if (HudLockedFor(JournalAction.RoomShortcuts)) return; GoToRoomShortcut(RoomType.West); });

        previousSpeciesButton?.onClick.AddListener(() => { if (HudLockedFor(JournalAction.PageArrows)) return; StepSpecies(-1); });
        nextSpeciesButton?.onClick.AddListener(() => { if (HudLockedFor(JournalAction.PageArrows)) return; StepSpecies(1); });

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

    private System.Func<bool> condJournalOpen, condPlantsPage, condPlantDetail;

    void OnEnable()
    {
        condJournalOpen = () => isJournalOpen;
        condPlantsPage = () => isJournalOpen && plantsPage != null && plantsPage.activeInHierarchy;
        condPlantDetail = () => isJournalOpen && detailPanel != null && detailPanel.activeInHierarchy;
        TutorialSequenceController.RegisterCondition("journal_opened", condJournalOpen);
        TutorialSequenceController.RegisterCondition("opened_plants_bookmark", condPlantsPage);
        TutorialSequenceController.RegisterCondition("opened_plant_detail", condPlantDetail);

        if (journalManager != null)
            journalManager.OnJournalChanged += RefreshUI;

        RefreshUI();
    }

    void OnDisable()
    {
        TutorialSequenceController.UnregisterCondition("journal_opened", condJournalOpen);
        TutorialSequenceController.UnregisterCondition("opened_plants_bookmark", condPlantsPage);
        TutorialSequenceController.UnregisterCondition("opened_plant_detail", condPlantDetail);

        if (journalManager != null)
            journalManager.OnJournalChanged -= RefreshUI;
    }

    // ------------------------------------------------------------
    // TUTORIAL LOCKING — while a Portable step has "Lock Other Input" on, everything in the journal
    // except what that step is teaching is inert. The bookmarks / plant slots check for their own
    // trigger id (so the step waiting on "opened_plants_bookmark" still lets that one tab through);
    // everything else (settings, room shortcuts, prev/next, dropdowns, the fill-answers button, the J
    // key) is blocked outright.
    // ------------------------------------------------------------
    private static bool HudLocked =>
        TutorialSequenceController.Instance != null && TutorialSequenceController.Instance.IsHudBlocked;

    private static bool BlockedExcept(string triggerId) =>
        TutorialSequenceController.Instance != null &&
        !TutorialSequenceController.Instance.IsActionAllowedForTrigger(triggerId);

    /// <summary>Locked by the tutorial, unless the current step ticked this journal action in Allowed Journal.</summary>
    private static bool HudLockedFor(JournalAction action) =>
        HudLocked && !TutorialSequenceController.Instance.IsJournalActionAllowed(action);

    /// <summary>Same as BlockedExcept(triggerId), but ALSO lets it through when the step ticked this journal action.</summary>
    private static bool BlockedExcept(string triggerId, JournalAction action) =>
        BlockedExcept(triggerId) && !TutorialSequenceController.Instance.IsJournalActionAllowed(action);

    /// <summary>Called by JournalSlotUI before opening a species page from a grid click.</summary>
    public bool CanClickSlot() => !BlockedExcept("opened_plant_detail", JournalAction.PlantSlots);

    private bool lastHudLocked;

    void Update()
    {
        // Dropdowns can't be "cancelled" after the fact, so they're simply made non-interactable while the
        // tutorial has input locked, and restored (respecting the real per-species lock) when it lifts.
        bool hudLocked = HudLockedFor(JournalAction.CareAnswers);
        if (hudLocked != lastHudLocked)
        {
            lastHudLocked = hudLocked;
            if (selectedSpecies != null) RefreshCareRow(selectedSpecies);
        }

        // See the class comment re: Escape — that's handled centrally elsewhere,
        // same as Inventory. J is polled directly here (not through a
        // PlayerControls action) so this works without any Input Actions asset changes.
        if (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
        {
            TutorialSequenceController tsc = TutorialSequenceController.Instance;
            if (tsc != null)
            {
                // Opening is only allowed for the step waiting on "journal_opened"; closing follows the same
                // rule as the back button.
                if (!isJournalOpen && !tsc.IsActionAllowedForTrigger("journal_opened")) return;
                if (isJournalOpen && tsc.IsBackBlocked) return;
            }
            ToggleJournal();
        }
    }

    // ------------------------------------------------------------
    // OPEN / CLOSE
    // ------------------------------------------------------------
    [Header("Open / Close Events")]
    [Tooltip("Fires every time the journal opens - however it was opened (J key, inventory link, button).")]
    public UnityEvent onJournalOpened;
    [Tooltip("Fires every time the journal closes - back button, Esc, J key, anything. Put 'deactivate this object' " +
             "style actions HERE instead of on the back button's On Click, which only fires when the button is clicked.")]
    public UnityEvent onJournalClosed;

    public void ToggleJournal()
    {
        isJournalOpen = !isJournalOpen;
        SetJournalVisible(isJournalOpen);
        mainUI?.SetHudHidden(isJournalOpen, this);

        if (isJournalOpen)
        {
            MenuLayerManager.NotifyOpened(this, CloseJournal);
            onJournalOpened?.Invoke();
            GameAudio.Play(SoundId.OpenJournal);

            GameInputModeManager.Instance?.SetMenuUIMode();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ThirdPersonCameraController.CameraLocked = true;

            RefreshUI();

            // Tutorial hook — matches a step reading "Press [J] to open your journal".
            TutorialSequenceController.Instance?.NotifyExternalTrigger("journal_opened");
        }
        else
        {
            MenuLayerManager.NotifyClosed(this);
            GameAudio.Play(SoundId.OpenJournal); // book closing - same book sound as opening
            onJournalClosed?.Invoke();
            GameInputModeManager.Instance?.SetGameplayMode();

            // Tutorial hook — matches a step reading "Press the back button to return to the
            // game" (this used to be an Escape-based step; both the back button AND Escape route
            // through here via CloseJournal(), so either one satisfies it).
            TutorialSequenceController.Instance?.NotifyExternalTrigger("closed_journal_back_button");

            // Autosave on closing the journal (does nothing outside the main scene or if autosave is off).
            SaveManager.Autosave("journal");
        }
    }
    /// <summary>Opens the journal on the Lore bookmark with this entry selected (used when the player picks
    /// a lore item up in the world). The normal back button / Escape / J close it again.</summary>
    public void OpenLoreEntry(MissionData entry)
    {
        if (!isJournalOpen) ToggleJournal();

        guideController?.OpenLoreEntry(entry);
        ShowPage(guidePage, loreNavButton);
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
        {
            ShowPage(plantsPage, plantsNavButton);
            SelectFirstDiscoveredSpecies();
        }
    }

    /// <summary>Auto-opens the first discovered species' detail page the instant the journal opens, so
    /// the Plants page never starts on an empty right-hand page — mirrors GuideUIController's own
    /// auto-select-first-mission behavior. Checks Sunny, then Dark, then Water, in that order (same
    /// order PopulateRow draws the rows in); does nothing if nothing's been discovered yet.</summary>
    private void SelectFirstDiscoveredSpecies()
    {
        if (database == null) return;

        List<PlantSpeciesData> candidates = GetNavigableSpecies(PlantType.Sunny);
        if (candidates.Count == 0) candidates = GetNavigableSpecies(PlantType.Dark);
        if (candidates.Count == 0) candidates = GetNavigableSpecies(PlantType.Water);

        if (candidates.Count > 0)
            ShowSpeciesDetail(candidates[0]);
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
        SetNavButtonActive(loreNavButton, navButton == loreNavButton);
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

        TutorialTargets.Clear("journal_plant");
        TutorialTargets.Clear("journal_plant_completed");
        TutorialTargets.Clear("journal_passive_plant");
        passivePlantTargetSet = false;
        PopulateRow(sunnyRow, PlantType.Sunny);
        PopulateRow(darkRow, PlantType.Dark);
        PopulateRow(waterRow, PlantType.Water);

        // Prefer the first completed plant for the tutorial pointer; otherwise keep the first discovered.
        RectTransform done = TutorialTargets.Get("journal_plant_completed");
        if (done != null) TutorialTargets.Set("journal_plant", done, overwrite: true);
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

            // Tutorial pointer targets (resolved at the end of RefreshUI).
            RectTransform sr = slot.GetComponent<RectTransform>();
            if (journalManager != null && journalManager.IsCompleted(species))
                TutorialTargets.Set("journal_plant_completed", sr);
            TutorialTargets.Set("journal_plant", sr);

            // First discovered species with a passive ability - what a "passive ability" tutorial prompt
            // outlines (Follow Target Key = journal_passive_plant), with its name for {Passive Plant Name}.
            if (!passivePlantTargetSet && species.hasPassiveAbility)
            {
                passivePlantTargetSet = true;
                TutorialTargets.Set("journal_passive_plant", sr);
                TutorialTargets.SetText("passive_plant_name", species.displayName);
            }
        }
    }

    private bool passivePlantTargetSet;

    // ------------------------------------------------------------
    // DETAIL PAGE — called by JournalSlotUI.OnPointerClick
    // ------------------------------------------------------------
    public void ShowSpeciesDetail(PlantSpeciesData species)
    {
        if (species == null) return;
        selectedSpecies = species;
        TutorialSequenceController.Instance?.NotifyExternalTrigger("opened_plant_detail");

        if (detailPanel != null) detailPanel.SetActive(true);

        bool alt = species.useAlternateJournalLayout;
        Image imageTarget = PickLayout(detailImage, detailImageAlt, alt);
        TMP_Text descTarget = PickLayout(detailDescription, detailDescriptionAlt, alt);
        TMP_Text secondTarget = PickLayout(detailDescriptionSecondary, detailDescriptionSecondaryAlt, alt);

        if (imageTarget != null)
        {
            // journalImage is the larger detail illustration — falls back to the
            // small grid icon so the page isn't blank if none was assigned.
            Sprite img = species.journalImage != null ? species.journalImage : species.journalIcon;
            imageTarget.sprite = img;
            imageTarget.enabled = img != null;
        }

        if (detailName != null) detailName.text = species.displayName;
        if (detailTier != null) detailTier.text = $"Tier {species.tier}";

        (string firstText, string secondText) = GetUnlockedDescriptions(species);
        if (descTarget != null) descTarget.text = firstText;
        if (secondTarget != null) secondTarget.text = secondText;

        RefreshDifficultyDots(species.difficulty);
        RefreshCareRow(species);
        RefreshSpeciesNavButtons();
    }

    /// <summary>Picks the default or alternate element for one part of the detail page and shows only
    /// that one. If the alternate isn't assigned (or the default isn't), the other one is used as-is.</summary>
    private static T PickLayout<T>(T def, T alt, bool useAlt) where T : Component
    {
        T chosen = (useAlt && alt != null) ? alt : def;
        if (def != null) def.gameObject.SetActive(chosen == def);
        if (alt != null) alt.gameObject.SetActive(chosen == alt);
        return chosen;
    }

    /// <summary>Gradual description unlock, by tier:
    ///   - Tier 1 (and crystals — requiresRoomUnlock species aren't part of this at all, so they
    ///     just get the simplest/no-gating treatment): both blocks unlock immediately on pickup
    ///     (PlantJournalManager.IsDiscovered).
    ///   - Tier 2: first block on pickup; second block only once EVERY Tier 1 species has hit its
    ///     gold/fully-ripened milestone at least once — PlantJournalManager.IsCompleted, via
    ///     AllOfTierCompleted(1). NOT the same as "picked up" — a Tier 1 plant harvested before it
    ///     was fully ripened doesn't count here even though it's discovered.
    ///   - Tier 3: first block only once every Tier 1 AND Tier 2 species has been completed this
    ///     way ("all the other tiers"); second block only once THIS species itself has been
    ///     completed (harvested at 100% at least once — the same event that unlocks its own Gold
    ///     icon on the Progress page).
    /// A locked block shows lockedPlaceholderText instead of going blank, so the layout doesn't
    /// collapse to nothing while the player's waiting on it.
    /// </summary>
    private (string first, string second) GetUnlockedDescriptions(PlantSpeciesData species)
    {
        bool discovered = journalManager != null && journalManager.IsDiscovered(species);

        // Not discovered at all yet — nothing to show regardless of tier (ShowSpeciesDetail is only
        // reachable for discovered species via JournalSlotUI anyway, but stay defensive).
        if (!discovered)
            return (lockedPlaceholderText, lockedPlaceholderText);

        if (species.requiresRoomUnlock || species.tier <= 1)
            return (species.description, species.descriptionSecondary);

        if (species.tier == 2)
        {
            bool secondUnlocked = journalManager != null && journalManager.AllOfTierCompleted(1);
            return (species.description, secondUnlocked ? species.descriptionSecondary : lockedPlaceholderText);
        }

        // Tier 3 and anything above.
        bool firstUnlocked = journalManager != null &&
            journalManager.AllOfTierCompleted(1) && journalManager.AllOfTierCompleted(2);
        // "The player has gotten it completed" — this species' own gold/fully-ripened milestone,
        // same event that unlocks its Gold icon on the Progress page.
        bool secondUnlockedT3 = journalManager != null && journalManager.IsCompleted(species);

        return (
            firstUnlocked ? species.description : lockedPlaceholderText,
            secondUnlockedT3 ? species.descriptionSecondary : lockedPlaceholderText
        );
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

    private void SetupCareDropdown(TMP_Dropdown dropdown, List<string> options)
    {
        if (dropdown == null) return;
        var all = new List<string> { unansweredLabel };
        all.AddRange(options);
        dropdown.ClearOptions();
        dropdown.AddOptions(all);
    }

    private void RefreshCareRow(PlantSpeciesData species)
    {
        refreshingCare = true;
        PlantJournalManager.CareGuess g = journalManager != null ? journalManager.GetGuess(species) : null;
        bool locked = g != null && g.locked;
        bool tutorialLocked = HudLockedFor(JournalAction.CareAnswers);
        bool uiLocked = locked || tutorialLocked; // answers locked for good OR the tutorial has input locked right now

        // Dropdown index 0 is the "?" option, so a stored value v maps to v + 1 (soil maps through SoilChoices).
        ApplyCare(waterDropdown, g != null && g.water >= 0 ? g.water + 1 : 0, uiLocked);
        ApplyCare(lightDropdown, g != null && g.light >= 0 ? g.light + 1 : 0, uiLocked);
        int soilIdx = g != null && g.soil >= 0 ? System.Array.IndexOf(SoilChoices, (SoilKind)g.soil) + 1 : 0;
        ApplyCare(soilDropdown, Mathf.Max(0, soilIdx), uiLocked);

        // The toggle stays clickable while the answers are locked (that's how you unlock them) - only the
        // tutorial can disable it.
        if (fillAnswersButton != null) fillAnswersButton.interactable = !tutorialLocked;
        Image toggleIcon = fillAnswersIcon != null ? fillAnswersIcon
            : fillAnswersButton != null ? fillAnswersButton.image : null;
        if (toggleIcon != null)
        {
            Sprite s = locked ? lockedSprite : unlockedSprite;
            if (s != null) toggleIcon.sprite = s;
        }
        if (answersLockedIndicator != null) answersLockedIndicator.SetActive(locked);
        refreshingCare = false;
    }

    private static void ApplyCare(TMP_Dropdown dropdown, int index, bool locked)
    {
        if (dropdown == null) return;
        dropdown.SetValueWithoutNotify(index);
        dropdown.RefreshShownValue();
        dropdown.interactable = !locked;
    }

    private void OnCareDropdownChanged()
    {
        if (refreshingCare || selectedSpecies == null || journalManager == null) return;

        int water = waterDropdown != null ? waterDropdown.value - 1 : -1;
        int light = lightDropdown != null ? lightDropdown.value - 1 : -1;
        int soil = -1;
        if (soilDropdown != null && soilDropdown.value > 0)
            soil = (int)SoilChoices[soilDropdown.value - 1];
        journalManager.SetGuess(selectedSpecies, water, light, soil);
    }

    private void OnFillAnswersClicked()
    {
        if (selectedSpecies == null || journalManager == null) return;
        PlantJournalManager.CareGuess g = journalManager.GetGuess(selectedSpecies);
        if (g != null && g.locked) journalManager.HideAnswers(selectedSpecies);
        else journalManager.RevealAnswers(selectedSpecies);
        RefreshCareRow(selectedSpecies);
    }
}