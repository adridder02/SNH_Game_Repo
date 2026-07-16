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

    [Header("Choose Plant Panel — Grid Wrapping")]
    [SerializeField] private int choosePlantColumns = 5;
    [SerializeField] private float choosePlantGapX = 8f;
    [SerializeField] private float choosePlantGapY = 8f;

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

    public bool IsOpen => menuRoot != null && menuRoot.activeSelf;

    public bool IsSubmenuOpen =>
        (chooseSoilPanel != null && chooseSoilPanel.activeSelf) ||
        (choosePlantPanel != null && choosePlantPanel.activeSelf);

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

        menuCloseButton?.onClick.AddListener(() => ownerInteraction?.CloseMenu());

        if (menuRoot != null) menuRoot.SetActive(false);
    }

    void Update()
    {
        if (currentPot != null && mainPanel != null && mainPanel.activeSelf)
            RefreshMainStatusBars();

        if (choosePlantPanel != null && choosePlantPanel.activeSelf)
            RefreshChoosePlantSoilIcon();
    }

    public void Open(PotContents pot, PlayerInventory inventory, PotInteraction interaction)
    {
        currentPot = pot;
        playerInventory = inventory;
        ownerInteraction = interaction;

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
    }

    // ------------------------------------------------------------
    // PANEL SWITCHING
    // ------------------------------------------------------------
    private void SetActivePanel(GameObject overlayPanel)
    {
        if (mainPanel != null) mainPanel.SetActive(true);
        if (chooseSoilPanel != null) chooseSoilPanel.SetActive(overlayPanel == chooseSoilPanel);
        if (choosePlantPanel != null) choosePlantPanel.SetActive(overlayPanel == choosePlantPanel);
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

        if (choosePlantButton != null)
            choosePlantButton.interactable = hasPlant ? true : hasSoil;

        if (waterButton != null)
            waterButton.interactable = hasSoil && hasPlant;

        RefreshMainStatusBars();
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

        List<InventoryItemInstance> owned = playerInventory != null
            ? playerInventory.GetAllItems() : new List<InventoryItemInstance>();

        bool anyShown = false;

        if (choosePlantOptionTemplate != null && choosePlantOptionContainer != null)
        {
            RectTransform templateRect = choosePlantOptionTemplate.GetComponent<RectTransform>();
            int index = 0;

            foreach (InventoryItemInstance item in owned)
            {
                if (item?.plantPrefab == null) continue;

                bool fits = currentPot != null && (currentPot.IsStatic || item.size == currentPot.PotSize);

                Button optionButton = Instantiate(choosePlantOptionTemplate, choosePlantOptionContainer);
                optionButton.gameObject.SetActive(true);

                RectTransform optionRect = optionButton.GetComponent<RectTransform>();
                optionRect.anchoredPosition = ManualSlotLayout.GetPosition(templateRect, index, choosePlantColumns, choosePlantGapX, choosePlantGapY);
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
        }

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
}