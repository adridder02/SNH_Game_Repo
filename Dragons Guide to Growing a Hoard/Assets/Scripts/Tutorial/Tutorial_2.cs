using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Tutorial_2 : MonoBehaviour
{
    [Header("UI References - Drag from Canvas")]
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private Toggle Tasks1;
    [SerializeField] private Toggle Tasks2;
    [SerializeField] private Toggle Tasks3;
    [SerializeField] private Toggle Tasks4;
    [SerializeField] private TextMeshProUGUI Counter;
    [SerializeField] private TextMeshProUGUI TipBits;
    [SerializeField] private Slider miasmaMeter; // Added missing reference

    // Singleton
    public static Tutorial_2 Instance { get; private set; }

    // Tutorial state
    public bool inTut = true;
    private bool inv;
    private bool spBool;
    private bool mpBool;
    private bool lpBool;
    private bool decreaseMiasma;
    private float miasmaStart = 100f;
    private int countPlants = 0;
    private int couresellCounter = 0;
    private float timerSwitch = 0f;
    private float timerMiasma = 0f;
    private float TimerDelay = 10f;

    private string[] strtipBit = {
        "Follow the arrow and collect all interactive plants glowing yellow",
        "Harvested plants can be viewed in Inventory, press 'I' to open Inventory.",
        "Open the binder, to see all collected plants and explored plants.",
        "Ensure that the miasma bar doesn't affect the plants. Plant strategically to avoid effects."
    };

    void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        ValidateReferences();
    }

    void Start()
    {
        ResetTutorial();
    }

    void Update()
    {
        // Only run update logic if tutorial is active
        if (!inTut) return;

        // Tip rotation timer
        timerSwitch += Time.deltaTime;
        if (timerSwitch >= TimerDelay)
        {
            timerSwitch = 0f;
            NextBinder();
        }

        // Miasma timer
        timerMiasma += Time.deltaTime;
        if (timerMiasma >= 1f)
        {
            MiasmaControl();
            timerMiasma = 0f;
        }
    }

    private void ValidateReferences()
    {
        if (tutorialPanel == null)
            Debug.LogError("Tutorial Panel is not assigned in Inspector!");

        if (TipBits == null)
            Debug.LogError("TipBits is not assigned in Inspector!");

        if (Counter == null)
            Debug.LogError("Counter is not assigned in Inspector!");

        if (miasmaMeter == null)
            Debug.LogError("Miasma Meter is not assigned in Inspector!");

        if (Tasks1 == null) Debug.LogError("Tasks1 not assigned!");
        if (Tasks2 == null) Debug.LogError("Tasks2 not assigned!");
        if (Tasks3 == null) Debug.LogError("Tasks3 not assigned!");
        if (Tasks4 == null) Debug.LogError("Tasks4 not assigned!");
    }

    private void ResetTutorial()
    {
        inv = false;
        spBool = false;
        mpBool = false;
        lpBool = false;
        decreaseMiasma = false;
        miasmaStart = 100f;
        countPlants = 0;
        couresellCounter = 0;
        timerSwitch = 0f;
        timerMiasma = 0f;
        inTut = true;

        // Reset toggles
        if (Tasks1 != null) Tasks1.isOn = false;
        if (Tasks2 != null) Tasks2.isOn = false;
        if (Tasks3 != null) Tasks3.isOn = false;
        if (Tasks4 != null) Tasks4.isOn = false;

        // Reset counter
        if (Counter != null)
            Counter.text = "0";

        // Reset miasma meter
        if (miasmaMeter != null)
            miasmaMeter.value = 100f;

        // Set initial tip
        if (TipBits != null)
            TipBits.text = strtipBit[0];

        // Show the tutorial
        ShowUI();
    }

    private void NextBinder()
    {
        couresellCounter++;
        if (couresellCounter >= strtipBit.Length)
            couresellCounter = 0;

        if (TipBits != null)
            TipBits.text = strtipBit[couresellCounter];
    }

    // Public methods for other scripts
    public void AddPlantCounter()
    {
        if (Instance == null || !Instance.inTut) return;

        countPlants++;
        if (Counter != null)
            Counter.text = countPlants.ToString();

        // Check if miasma should decrease
        if (countPlants > 2 && miasmaStart > 66f)
        {
            decreaseMiasma = true;
        }
        else if (countPlants > 5 && miasmaStart > 33f)
        {
            decreaseMiasma = true;
        }
        else if (countPlants > 8 && miasmaStart > 0f && miasmaStart < 33f)
        {
            decreaseMiasma = true;
        }
    }

    public void RemovePlantCounter()
    {
        if (Instance == null || !Instance.inTut) return;

        countPlants--;
        if (Counter != null)
            Counter.text = countPlants.ToString();
    }

    public void OpenedInventory()
    {
        if (Instance == null || !Instance.inTut) return;
        if (inv) return;

        inv = true;
        if (Tasks1 != null)
            Tasks1.isOn = true;
    }

    public void PlantedSp()
    {
        if (Instance == null || !Instance.inTut) return;
        if (spBool) return;

        spBool = true;
        if (Tasks2 != null)
            Tasks2.isOn = true;
    }

    public void PlantedMp()
    {
        if (Instance == null || !Instance.inTut) return;
        if (mpBool) return;

        mpBool = true;
        if (Tasks3 != null)
            Tasks3.isOn = true;
    }

    public void PlantedLp()
    {
        if (Instance == null || !Instance.inTut) return;
        if (lpBool) return;

        lpBool = true;
        if (Tasks4 != null)
            Tasks4.isOn = true;
    }

    private void MiasmaControl()
    {
        if (!decreaseMiasma) return;

        miasmaStart -= 1f;

        // Update miasma meter
        if (miasmaMeter != null)
            miasmaMeter.value = miasmaStart;

        // Stop decreasing at certain thresholds
        if (miasmaStart >= 33f && miasmaStart <= 66f)
        {
            decreaseMiasma = false;
        }
        else if (miasmaStart <= 33f && miasmaStart > 0f)
        {
            decreaseMiasma = false;
        }
        else if (miasmaStart <= 0f)
        {
            decreaseMiasma = false;
            if (miasmaMeter != null)
                miasmaMeter.value = 0f;
            return;
        }
    }

    // UI Visibility Methods
    public void ShowUI()
    {
        if (Instance == null) return;

        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(true);
            Debug.Log("Tutorial UI Shown");
        }
        else
        {
            Debug.LogError("Tutorial Panel reference is null!");
        }
    }

    public void HideUI()
    {
        if (Instance == null) return;

        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
            Debug.Log("Tutorial UI Hidden");
        }
        else
        {
            Debug.LogError("Tutorial Panel reference is null!");
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}