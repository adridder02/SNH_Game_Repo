using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class PotMenuUIController : MonoBehaviour
{
    // ------------------------------------------------------------
    // GLOBAL
    // ------------------------------------------------------------
    [Header("Global")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private Button menuCloseButton;

    // ------------------------------------------------------------
    // MAIN PANEL
    // ------------------------------------------------------------
    [Header("Main Panel")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private Image mainPlantImage;
    [SerializeField] private TextMeshProUGUI plantNameText;
    [Tooltip("Shows this pot's current soil type (the little pot-with-dirt icon, top-left of the Main panel). " +
             "Distinct from choosePlantCurrentSoilIcon below — that one lives on the Choose Plant panel. Both " +
             "read the same currentPot.CurrentSoil, they just refresh in different panels.")]
    [SerializeField] private Image mainSoilIcon;
    [SerializeField] private Button soilActionButton;
    [SerializeField] private TextMeshProUGUI soilActionButtonLabel;
    [SerializeField] private Button choosePlantButton;
    [SerializeField] private TextMeshProUGUI choosePlantButtonLabel;
    [SerializeField] private Button waterButton;

    [Header("Main Panel — Status Bars")]
    [Tooltip("Despite the old name, this now shows the plant's GROWTH PROGRESS (PlantProgress.Progress01) — " +
             "how ripe it is — not its instantaneous happiness score. Set this bar's ImageFillBar to " +
             "useFillGradient with a red→yellow→green fillColorGradient so colour communicates progress too.")]
    [SerializeField] private ImageFillBar qualityBar;
    [SerializeField] private ImageFillBar waterLevelBar;

    // ------------------------------------------------------------
    // CHOOSE SOIL PANEL
    // ------------------------------------------------------------
    [Header("Choose Soil Panel")]
    [SerializeField] private GameObject chooseSoilPanel;
    [SerializeField] private Button claySoilButton;
    [SerializeField] private Button loamSoilButton;
    [SerializeField] private Button sandySoilButton;
    [SerializeField] private Button waterSoilButton;
    [SerializeField] private Button selectSoilButton;
    [SerializeField] private Button chooseSoilBackButton;

    // ------------------------------------------------------------
    // CHOOSE PLANT PANEL
    // ------------------------------------------------------------
    [Header("Choose Plant Panel")]
    [SerializeField] private GameObject choosePlantPanel;
    [SerializeField] private Transform choosePlantOptionContainer;
    [SerializeField] private Button choosePlantOptionTemplate;
    [SerializeField] private TextMeshProUGUI choosePlantEmptyText;
    [SerializeField] private Image choosePlantCurrentSoilIcon;
    [SerializeField] private Button choosePlantConfirmButton;
    [SerializeField] private Button choosePlantBackButton;

    [Header("Choose Plant Panel — Horizontal Scroll")]
    [Tooltip("Items are laid out in a single row starting from choosePlantOptionTemplate's own position " +
             "and scroll sideways instead of wrapping into extra rows (which is what was overflowing the " +
             "panel). Assign the ScrollRect that wraps choosePlantOptionContainer (set it to Horizontal " +
             "only, Content = choosePlantOptionContainer) — auto-found on the container's parents if left " +
             "empty. Content Size Fitter isn't needed; this script resizes the content rect itself each " +
             "refresh based on how many items there are.")]
    [SerializeField] private ScrollRect choosePlantScrollRect;
    [SerializeField] private float choosePlantGapX = 8f;

    // ------------------------------------------------------------
    // ABILITIES PANEL
    // ------------------------------------------------------------
    [Header("Main Panel — Abilities Button")]
    [Tooltip("Opens the Abilities panel. Wire this up next to soilActionButton/choosePlantButton/waterButton.")]
    [SerializeField] private Button useAbilityButton;

    [Header("Main Panel — Pollen Puff Collection")]
    [Tooltip("Only shown/interactable when the current pot's plant has a PollenPuffProducer with " +
             "ReadyCount > 0 — collects into PlayerAbilityInventory. Independent of the Harvest button; " +
             "collecting puffballs does not remove the plant.")]
    [SerializeField] private Button collectPuffballsButton;
    [SerializeField] private TextMeshProUGUI collectPuffballsButtonLabel;

    [Header("Abilities Panel")]
    [SerializeField] private GameObject abilitiesPanel;
    [SerializeField] private Transform abilityOptionContainer;
    [SerializeField] private Button abilityOptionTemplate;
    [SerializeField] private TextMeshProUGUI abilityEmptyText;
    [SerializeField] private Button abilityBackButton;
    [Tooltip("Applies whichever ability item is currently selected (highlighted) in this panel — " +
             "same select-then-confirm pattern as choosePlantConfirmButton below. Starts non-" +
             "interactable and only enables once the player has actually clicked an item.")]
    [SerializeField] private Button abilityContinueButton;

    [Header("Abilities Panel — Grid Wrapping")]
    [SerializeField] private int abilityColumns = 5;
    [SerializeField] private float abilityGapX = 8f;
    [SerializeField] private float abilityGapY = 8f;

    [Header("Abilities Panel — References")]
    [Tooltip("Auto-found on the player (alongside PlayerInventory) if left empty.")]
    [SerializeField] private PlayerAbilityInventory abilityInventory;
    [Tooltip("Auto-found in the scene if left empty. Needed so Placeable items can hand off to grid placement.")]
    [SerializeField] private AbilityPlacementSystem abilityPlacementSystem;

    // ------------------------------------------------------------
    // SOIL ICONS
    // ------------------------------------------------------------
    [Header("Soil Icons")]
    [SerializeField] private Sprite claySoilIcon;
    [SerializeField] private Sprite loamSoilIcon;
    [SerializeField] private Sprite sandySoilIcon;
    [SerializeField] private Sprite waterSoilIcon;

    // ------------------------------------------------------------
    // Runtime state
    // ------------------------------------------------------------
    private PotContents currentPot;
    private PlayerInventory playerInventory;
    private PotInteraction ownerInteraction;
    private SoilKind? pendingSoil;
    private InventoryItemInstance pendingPlant;
    private AbilityItemInstance pendingAbility;

    public bool IsOpen => menuRoot != null && menuRoot.activeSelf;

    public bool IsSubmenuOpen =>
        (chooseSoilPanel != null && chooseSoilPanel.activeSelf) ||
        (choosePlantPanel != null && choosePlantPanel.activeSelf) ||
        (abilitiesPanel != null && abilitiesPanel.activeSelf);

    public void CloseSubmenuOrMenu()
    {
        if (IsSubmenuOpen)
        {
            ShowMain();
            return;
        }

        ownerInteraction?.CloseMenu();
    }

    void Awake()
    {
        claySoilButton?.onClick.AddListener(() => SelectSoilOption(SoilKind.Clay));
        loamSoilButton?.onClick.AddListener(() => SelectSoilOption(SoilKind.Loam));
        sandySoilButton?.onClick.AddListener(() => SelectSoilOption(SoilKind.Sandy));
        waterSoilButton?.onClick.AddListener(() => SelectSoilOption(SoilKind.Water));
        selectSoilButton?.onClick.AddListener(ConfirmSoilSelection);
        chooseSoilBackButton?.onClick.AddListener(ShowMain);

        choosePlantConfirmButton?.onClick.AddListener(ConfirmPlantSelection);
        choosePlantBackButton?.onClick.AddListener(ShowMain);

        soilActionButton?.onClick.AddListener(ShowChooseSoil);
        choosePlantButton?.onClick.AddListener(OnPlantActionButtonClicked);
        waterButton?.onClick.AddListener(OnWaterButtonClicked);

        useAbilityButton?.onClick.AddListener(ShowAbilities);
        abilityBackButton?.onClick.AddListener(ShowMain);
        abilityContinueButton?.onClick.AddListener(ConfirmAbilitySelection);
        collectPuffballsButton?.onClick.AddListener(OnCollectPuffballsClicked);

        menuCloseButton?.onClick.AddListener(() => ownerInteraction?.CloseMenu());

        // Template stays as a design-time reference in the Editor for sizing/position, but should
        // never actually be visible itself — only its clones. If it was left active in the scene
        // (common while laying out the panel), this guarantees it's hidden regardless, including
        // in the empty-inventory case where previously nothing hid it.
        if (choosePlantOptionTemplate != null)
            choosePlantOptionTemplate.gameObject.SetActive(false);

        if (choosePlantScrollRect == null && choosePlantOptionContainer != null)
            choosePlantScrollRect = choosePlantOptionContainer.GetComponentInParent<ScrollRect>();

        if (menuRoot != null) menuRoot.SetActive(false);
    }

    void Update()
    {
        if (currentPot != null && mainPanel != null && mainPanel.activeSelf)
        {
            RefreshMainStatusBars();
            RefreshPuffballButton();
        }

        if (choosePlantPanel != null && choosePlantPanel.activeSelf)
            RefreshChoosePlantSoilIcon();
    }

    public void Open(PotContents pot, PlayerInventory inventory, PotInteraction interaction)
    {
        currentPot = pot;
        playerInventory = inventory;
        ownerInteraction = interaction;

        if (abilityInventory == null && inventory != null)
            abilityInventory = inventory.GetComponent<PlayerAbilityInventory>();
        if (abilityPlacementSystem == null)
            abilityPlacementSystem = FindObjectOfType<AbilityPlacementSystem>();

        if (menuRoot != null) menuRoot.SetActive(true);

        GameInputModeManager.Instance?.SetMenuUIMode();  // Unified menu mode

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ThirdPersonCameraController.CameraLocked = true;

        if (mainPanel != null) mainPanel.SetActive(true);
        RefreshMainPanel();

        if (!pot.HasPlant)
            ShowChooseSoil();
        else
            ShowMain();
    }

    public void Close()
    {
        if (menuRoot != null) menuRoot.SetActive(false);

        GameInputModeManager.Instance?.SetGameplayMode();  // Return to normal

        currentPot = null;
        pendingSoil = null;
        pendingPlant = null;
        pendingAbility = null;
    }

    // ------------------------------------------------------------
    // PANEL SWITCHING
    // ------------------------------------------------------------
    private void SetActivePanel(GameObject overlayPanel)
    {
        if (mainPanel != null) mainPanel.SetActive(true);
        if (chooseSoilPanel != null) chooseSoilPanel.SetActive(overlayPanel == chooseSoilPanel);
        if (choosePlantPanel != null) choosePlantPanel.SetActive(overlayPanel == choosePlantPanel);
        if (abilitiesPanel != null) abilitiesPanel.SetActive(overlayPanel == abilitiesPanel);
    }

    private void ShowMain()
    {
        SetActivePanel(null);
        RefreshMainPanel();
    }

    private void ShowChooseSoil()
    {
        pendingSoil = (currentPot != null && currentPot.HasSoil) ? currentPot.CurrentSoil : (SoilKind?)null;
        SetActivePanel(chooseSoilPanel);
        RefreshSoilSelectionHighlight();
        RefreshMainPanel();
    }

    private void ShowChoosePlant()
    {
        SetActivePanel(choosePlantPanel);
        RefreshChoosePlantPanel();
        RefreshMainPanel();
    }

    private void ShowAbilities()
    {
        SetActivePanel(abilitiesPanel);
        RefreshAbilitiesPanel();
        RefreshMainPanel();
    }

    // ------------------------------------------------------------
    // MAIN PANEL (unchanged logic)
    // ------------------------------------------------------------
    private void RefreshMainPanel()
    {
        bool hasSoil = currentPot != null && currentPot.HasSoil;
        bool hasPlant = currentPot != null && currentPot.HasPlant;

        PlantDisplayInfo info = (hasPlant && currentPot.Plant != null)
            ? currentPot.Plant.GetComponent<PlantDisplayInfo>() : null;

        PlantSpeciesData species = (hasPlant && currentPot.Plant != null) ? currentPot.Plant.journalSpecies : null;

        if (mainPlantImage != null)
        {
            Sprite sprite = info != null && info.displayImage != null ? info.displayImage
                : species != null ? (species.journalImage != null ? species.journalImage : species.journalIcon) : null;
            mainPlantImage.sprite = sprite;
            mainPlantImage.enabled = sprite != null;
        }

        if (plantNameText != null)
        {
            plantNameText.text = !hasPlant ? ""
                : info != null && !string.IsNullOrEmpty(info.displayName) ? info.displayName
                : species != null && !string.IsNullOrEmpty(species.displayName) ? species.displayName
                : currentPot.Plant.name.Replace("(Clone)", "").Trim();
        }

        if (mainSoilIcon != null)
        {
            Sprite soilSprite = hasSoil ? GetSoilIcon(currentPot.CurrentSoil) : null;
            mainSoilIcon.sprite = soilSprite;
            mainSoilIcon.enabled = soilSprite != null;
        }

        if (soilActionButtonLabel != null)
            soilActionButtonLabel.text = hasSoil ? "Change Soil" : "Choose Soil";

        if (soilActionButton != null) soilActionButton.interactable = true;

        // Once the plant's growth progress is Complete, the same button that used to remove
        // the plant back into the inventory now reads "Harvest" instead — see
        // PotContents.HarvestPlant() for where that's actually handled. Everything else about
        // the button (still hasPlant-gated, still calls the same click handler) is unchanged.
        bool isHarvestable = CurrentPlantProgress != null && CurrentPlantProgress.IsComplete;

        if (choosePlantButtonLabel != null)
            choosePlantButtonLabel.text = !hasPlant ? "Choose Plant" : isHarvestable ? "Harvest" : "Remove Plant";

        // If the pot already has something planted, this button is "Harvest"/"Remove Plant" —
        // always show it regardless of what's in the player's inventory, since that's about
        // taking the CURRENT plant back out, not choosing a new one. If the pot is empty, only
        // show "Choose Plant" at all when the player actually owns at least one plant to put in
        // it — with nothing to choose from, the button has nothing useful to do, so hide it
        // rather than leave it sitting there greyed out.
        bool ownsAnyPlant = playerInventory != null && playerInventory.GetAllItems().Count > 0;
        bool showChoosePlant = hasPlant || (hasSoil && ownsAnyPlant);
        if (choosePlantButton != null)
        {
            choosePlantButton.gameObject.SetActive(showChoosePlant);
            choosePlantButton.interactable = hasPlant ? true : hasSoil;
        }

        if (waterButton != null)
            waterButton.interactable = hasSoil && hasPlant;

        // Same idea — no pot-targeted consumable in the player's inventory at all means this
        // button has nothing to open, so hide it instead of leaving a dead button on-screen.
        if (useAbilityButton != null)
            useAbilityButton.gameObject.SetActive(HasAnyPotTargetedAbilityStack());

        RefreshPuffballButton();

        RefreshMainStatusBars();
    }

    /// <summary>The current pot's planted PollenPuffProducer, or null. Same cheap-lookup treatment as
    /// CurrentPlantProgress below.</summary>
    private PollenPuffProducer CurrentPuffProducer =>
        (currentPot != null && currentPot.HasPlant && currentPot.Plant != null)
            ? currentPot.Plant.GetComponent<PollenPuffProducer>()
            : null;

    private void RefreshPuffballButton()
    {
        PollenPuffProducer producer = CurrentPuffProducer;
        bool show = producer != null;

        if (collectPuffballsButton != null)
        {
            collectPuffballsButton.gameObject.SetActive(show);
            collectPuffballsButton.interactable = show && producer.ReadyCount > 0;
        }

        if (show && collectPuffballsButtonLabel != null)
            collectPuffballsButtonLabel.text = $"Collect Puffballs ({producer.ReadyCount}/{producer.maxPuffballs})";
    }

    private void OnCollectPuffballsClicked()
    {
        PollenPuffProducer producer = CurrentPuffProducer;
        if (producer == null) return;

        int collected = producer.Collect(abilityInventory);
        if (collected > 0)
            RefreshMainPanel();
    }

    private void RefreshMainStatusBars()
    {
        if (qualityBar != null)
            qualityBar.SetNormalized(CurrentPlantProgress != null ? CurrentPlantProgress.Progress01 : 0f);

        if (waterLevelBar != null && playerInventory != null)
            waterLevelBar.SetValue(playerInventory.getWaterPool(), playerInventory.getMaxWaterPool());
    }

    /// <summary>The current pot's planted PlantProgress, or null if there's no pot/plant. Cheap lookup — called
    /// a couple of times per Update while the Main panel is open, not hot-path elsewhere.</summary>
    private PlantProgress CurrentPlantProgress =>
        (currentPot != null && currentPot.HasPlant && currentPot.Plant != null)
            ? currentPot.Plant.GetComponent<PlantProgress>()
            : null;

    private void OnPlantActionButtonClicked()
    {
        if (currentPot == null) return;

        if (currentPot.HasPlant)
        {
            // Dummy for now: harvest and manual-remove both just return the plant to the
            // inventory via the same path. HarvestPlant() is a distinct call so a real
            // "turn into a different item" system can hook in later without touching this UI.
            bool isHarvestable = CurrentPlantProgress != null && CurrentPlantProgress.IsComplete;

            if (isHarvestable)
                currentPot.HarvestPlant(playerInventory);
            else
                currentPot.RemovePlant(playerInventory);

            ownerInteraction?.RefreshOutlineFor(currentPot);
            RefreshMainPanel();
        }
        else
        {
            ShowChoosePlant();
        }
    }

    private void OnWaterButtonClicked()
    {
        if (currentPot == null) return;
        ownerInteraction?.WaterPot(currentPot);
        RefreshMainStatusBars();
    }

    // ------------------------------------------------------------
    // CHOOSE SOIL / PLANT (rest unchanged)
    // ------------------------------------------------------------
    private void SelectSoilOption(SoilKind kind)
    {
        pendingSoil = kind;
        RefreshSoilSelectionHighlight();
    }

    private void RefreshSoilSelectionHighlight()
    {
        Button active = GetSoilButton(pendingSoil);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(active != null ? active.gameObject : null);

        if (selectSoilButton != null)
            selectSoilButton.interactable = pendingSoil != null;
    }

    private Button GetSoilButton(SoilKind? kind)
    {
        if (kind == SoilKind.Clay) return claySoilButton;
        if (kind == SoilKind.Loam) return loamSoilButton;
        if (kind == SoilKind.Sandy) return sandySoilButton;
        if (kind == SoilKind.Water) return waterSoilButton;
        return null;
    }

    private Sprite GetSoilIcon(SoilKind? kind)
    {
        if (kind == SoilKind.Clay) return claySoilIcon;
        if (kind == SoilKind.Loam) return loamSoilIcon;
        if (kind == SoilKind.Sandy) return sandySoilIcon;
        if (kind == SoilKind.Water) return waterSoilIcon;
        return null;
    }

    private void ConfirmSoilSelection()
    {
        if (pendingSoil == null || currentPot == null) return;

        currentPot.SetSoil(pendingSoil.Value);
        RefreshChoosePlantSoilIcon();

        if (!currentPot.HasPlant)
            ShowChoosePlant();
        else
            ShowMain();
    }

    private void RefreshChoosePlantSoilIcon()
    {
        if (choosePlantCurrentSoilIcon == null) return;
        Sprite icon = (currentPot != null && currentPot.HasSoil) ? GetSoilIcon(currentPot.CurrentSoil) : null;
        choosePlantCurrentSoilIcon.sprite = icon;
        choosePlantCurrentSoilIcon.enabled = icon != null;
    }

    private void RefreshChoosePlantPanel()
    {
        RefreshChoosePlantSoilIcon();
        pendingPlant = null;
        if (choosePlantConfirmButton != null)
            choosePlantConfirmButton.interactable = false;

        if (choosePlantOptionContainer != null)
        {
            for (int i = choosePlantOptionContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = choosePlantOptionContainer.GetChild(i);
                if (choosePlantOptionTemplate != null && child == choosePlantOptionTemplate.transform) continue;
                Destroy(child.gameObject);
            }
        }

        // Belt-and-suspenders: RefreshChoosePlantPanel can run more than once per panel-open
        // (ShowChoosePlant calls it, and other refreshes may too), so make sure the template
        // never ends up visible no matter how many times this runs.
        if (choosePlantOptionTemplate != null)
            choosePlantOptionTemplate.gameObject.SetActive(false);

        List<InventoryItemInstance> owned = playerInventory != null
            ? playerInventory.GetAllItems() : new List<InventoryItemInstance>();

        bool anyShown = false;
        int index = 0;

        if (choosePlantOptionTemplate != null && choosePlantOptionContainer != null)
        {
            RectTransform templateRect = choosePlantOptionTemplate.GetComponent<RectTransform>();

            foreach (InventoryItemInstance item in owned)
            {
                if (item?.plantPrefab == null) continue;

                bool fits = currentPot != null && (currentPot.IsStatic || item.size == currentPot.PotSize);

                Button optionButton = Instantiate(choosePlantOptionTemplate, choosePlantOptionContainer);
                optionButton.gameObject.SetActive(true);

                // Single horizontal row — items scroll sideways via choosePlantScrollRect instead
                // of wrapping into extra rows, which is what was overflowing the panel before.
                RectTransform optionRect = optionButton.GetComponent<RectTransform>();
                optionRect.anchoredPosition = templateRect.anchoredPosition +
                    new Vector2(index * (templateRect.sizeDelta.x + choosePlantGapX), 0f);
                index++;

                PotPlantOptionUI optionUI = optionButton.GetComponent<PotPlantOptionUI>();
                InventoryItemInstance capturedItem = item;
                Button capturedButton = optionButton;

                if (optionUI != null)
                {
                    optionUI.Initialize(capturedItem, fits, () => SelectPlantOption(capturedItem, capturedButton));
                }
                else
                {
                    optionButton.interactable = fits;
                    optionButton.onClick.AddListener(() => SelectPlantOption(capturedItem, capturedButton));
                }

                anyShown = true;
            }

            // Resize the content rect to fit everything just laid out, so the ScrollRect knows how
            // far it's actually allowed to scroll — without this it stays whatever size it was left
            // at in the Editor and either can't reach the last item or scrolls into empty space.
            RectTransform contentRect = choosePlantOptionContainer as RectTransform;
            if (contentRect != null && index > 0)
            {
                float totalWidth = templateRect.anchoredPosition.x + index * (templateRect.sizeDelta.x + choosePlantGapX);
                contentRect.sizeDelta = new Vector2(totalWidth, contentRect.sizeDelta.y);
            }
        }

        // Always reopen scrolled all the way to the start rather than remembering wherever the
        // player last scrolled to.
        if (choosePlantScrollRect != null)
            choosePlantScrollRect.horizontalNormalizedPosition = 0f;

        if (choosePlantEmptyText != null)
            choosePlantEmptyText.gameObject.SetActive(!anyShown);
    }

    private void SelectPlantOption(InventoryItemInstance item, Button optionButton)
    {
        pendingPlant = item;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(optionButton != null ? optionButton.gameObject : null);

        if (choosePlantConfirmButton != null)
            choosePlantConfirmButton.interactable = pendingPlant != null;
    }

    private void ConfirmPlantSelection()
    {
        if (pendingPlant == null) return;
        ChoosePlant(pendingPlant);
        pendingPlant = null;
    }

    private void ChoosePlant(InventoryItemInstance item)
    {
        if (currentPot == null || item?.plantPrefab == null) return;

        bool success = currentPot.AddPlant(item.plantPrefab);
        if (!success) return;

        if (currentPot.Plant != null)
        {
            PlantDisplayInfo info = currentPot.Plant.GetComponent<PlantDisplayInfo>();
            if (info == null) info = currentPot.Plant.gameObject.AddComponent<PlantDisplayInfo>();
            info.displayName = item.displayName;
            info.displayImage = item.displayImage;
            info.icon = item.icon;
        }

        playerInventory.RemoveFirstPlant(item.plantPrefab);
        ownerInteraction?.RefreshOutlineFor(currentPot);
        ShowMain();
    }

    // ------------------------------------------------------------
    // ABILITIES PANEL
    // ------------------------------------------------------------
    private void RefreshAbilitiesPanel()
    {
        pendingAbility = null;
        if (abilityContinueButton != null)
            abilityContinueButton.interactable = false;

        if (abilityOptionContainer != null)
        {
            for (int i = abilityOptionContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = abilityOptionContainer.GetChild(i);
                if (abilityOptionTemplate != null && child == abilityOptionTemplate.transform) continue;
                Destroy(child.gameObject);
            }
        }

        var stacks = abilityInventory != null ? abilityInventory.Stacks : null;
        bool anyShown = false;

        if (abilityOptionTemplate != null && abilityOptionContainer != null && stacks != null)
        {
            RectTransform templateRect = abilityOptionTemplate.GetComponent<RectTransform>();
            int index = 0;

            foreach (AbilityItemInstance stack in stacks)
            {
                if (stack?.data == null || stack.count <= 0) continue;

                // Placeables and untargeted Consumables (ExpandInventory, DragonGlow) moved to the
                // main inventory's Abilities panel (InventoryUIController) + hotbar — they don't need
                // a pot as a target, so there's no reason to make the player open one to use them.
                // Only pot-targeted Consumables (Pollen Puff, Verdant Algae, Dewdrop) still belong here.
                if (!AbilityConsumableEffects.RequiresPotTarget(stack.data.effectId)) continue;

                bool usable = IsAbilityUsableNow(stack.data);

                Button optionButton = Instantiate(abilityOptionTemplate, abilityOptionContainer);
                optionButton.gameObject.SetActive(true);

                RectTransform optionRect = optionButton.GetComponent<RectTransform>();
                optionRect.anchoredPosition = ManualSlotLayout.GetPosition(templateRect, index, abilityColumns, abilityGapX, abilityGapY);
                index++;

                AbilityOptionUI optionUI = optionButton.GetComponent<AbilityOptionUI>();
                AbilityItemInstance capturedStack = stack;
                Button capturedButton = optionButton;

                if (optionUI != null)
                    optionUI.Initialize(capturedStack, usable, () => SelectAbilityOption(capturedStack, capturedButton));
                else
                    optionButton.interactable = usable;

                anyShown = true;
            }
        }

        if (abilityEmptyText != null)
            abilityEmptyText.gameObject.SetActive(!anyShown);
    }

    /// <summary>Whether the player owns at least one stack this panel would actually display —
    /// i.e. a pot-targeted Consumable, per the same filter RefreshAbilitiesPanel uses. Drives
    /// useAbilityButton's interactable state, so the button doesn't light up for Placeables/
    /// untargeted Consumables that live in the main inventory now and never appear here.</summary>
    private bool HasAnyPotTargetedAbilityStack()
    {
        if (abilityInventory == null) return false;

        foreach (AbilityItemInstance stack in abilityInventory.Stacks)
        {
            if (stack?.data == null || stack.count <= 0) continue;
            if (AbilityConsumableEffects.RequiresPotTarget(stack.data.effectId)) return true;
        }

        return false;
    }

    /// <summary>Whether this item can actually be used right now. Everything that reaches this panel
    /// is now, by the filter in RefreshAbilitiesPanel above, a pot-targeted Consumable — so this
    /// always comes down to whether the current pot has a plant.</summary>
    private bool IsAbilityUsableNow(AbilityItemData data)
    {
        if (data.kind == AbilityKind.Consumable && AbilityConsumableEffects.RequiresPotTarget(data.effectId))
            return currentPot != null && currentPot.HasPlant;

        return true;
    }

    /// <summary>Clicking an item in this panel just SELECTS/highlights it (same pattern as
    /// SelectPlantOption below) — it doesn't apply anything yet. Pressing abilityContinueButton
    /// (ConfirmAbilitySelection) is what actually uses it. This gives the player a chance to
    /// change their mind before spending an item, and matches the Choose Plant flow's shape.</summary>
    private void SelectAbilityOption(AbilityItemInstance stack, Button optionButton)
    {
        if (stack?.data == null) return;

        pendingAbility = stack;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(optionButton != null ? optionButton.gameObject : null);

        if (abilityContinueButton != null)
            abilityContinueButton.interactable = true;
    }

    /// <summary>Wired to abilityContinueButton — applies whichever item SelectAbilityOption most
    /// recently selected. This is where the actual effect happens (previously happened immediately
    /// on click, before the Continue step existed).</summary>
    private void ConfirmAbilitySelection()
    {
        if (pendingAbility?.data == null || abilityInventory == null) return;
        AbilityItemData data = pendingAbility.data;

        if (data.kind == AbilityKind.Placeable)
        {
            // Placement consumes from the inventory itself once the player actually clicks a
            // valid cell (see AbilityPlacementSystem.TryPlace) — close the pot menu and hand off.
            // Defensive only: RefreshAbilitiesPanel's RequiresPotTarget filter means a Placeable
            // should never actually reach this panel in the first place.
            if (abilityPlacementSystem == null)
            {
                Debug.LogWarning("[PotMenuUIController] No AbilityPlacementSystem found — can't place " +
                                  $"'{data.displayName}'.");
                return;
            }

            abilityPlacementSystem.BeginPlacing(data);
            pendingAbility = null;
            ownerInteraction?.CloseMenu();
            return;
        }

        // Consumable: apply now, only spend the item if it actually worked.
        GameObject player = playerInventory != null ? playerInventory.gameObject : null;
        bool applied = AbilityConsumableEffects.TryApply(data, player, currentPot);

        if (applied)
        {
            abilityInventory.TryConsume(data, 1);
            pendingAbility = null;
            RefreshAbilitiesPanel();
            RefreshMainPanel();
        }
        else
        {
            Debug.Log($"[PotMenuUIController] '{data.displayName}' couldn't be used right now.");
        }
    }
}