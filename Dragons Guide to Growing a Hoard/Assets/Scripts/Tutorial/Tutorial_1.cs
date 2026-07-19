using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Tutorial_1 : MonoBehaviour
{
    [Header("Scene Name")]
    #if UNITY_EDITOR
    [SerializeField] private SceneAsset sceneToLoad;
    #endif
    [SerializeField] private string sceneName = "";

    [Header("UI References - Drag from Canvas")]
    [SerializeField] private GameObject tutorialPanel; // The parent panel of all tutorial UI
    [SerializeField] private Toggle Tasks1;
    [SerializeField] private Toggle Tasks2;
    [SerializeField] private Toggle Tasks3;
    [SerializeField] private Toggle Tasks4;
    [SerializeField] private Toggle Tasks5;
    [SerializeField] private Toggle Tasks6;
    [SerializeField] private TextMeshProUGUI TaskLabel;

    // Tutorial state (non-static)
    public bool inTut = true;
    private bool removePlant;
    private bool addPotToInventory;
    private bool hasMoved;
    private bool hasAddedSoil;
    private bool hasPlanted;
    private bool hasAddedWater;
    
    private string[] listOfInstructions = {
        "use WASD to move around",
        "approach the Plant and press 'E' to harvest it",
        "use your wings by double tapping 'Space Bar' to move the plant on top of the table to plant in an ideal location",
        "Once on the table press F to place a pot on the table"
    };

    private int currentTaskIndex = 0;
    private float delay = 0f;

    // Singleton pattern for easy access from other scripts
    public static Tutorial_1 Instance { get; private set; }

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

        // Validate references
        ValidateReferences();
    }

    void Start()
    {
        ResetTutorial();
    }

    private void ValidateReferences()
    {
        if (tutorialPanel == null)
            Debug.LogError("Tutorial Panel is not assigned in Inspector!");

        if (TaskLabel == null)
            Debug.LogError("TaskLabel is not assigned in Inspector!");

        if (Tasks1 == null) Debug.LogError("Tasks1 not assigned!");
        if (Tasks2 == null) Debug.LogError("Tasks2 not assigned!");
        if (Tasks3 == null) Debug.LogError("Tasks3 not assigned!");
        if (Tasks4 == null) Debug.LogError("Tasks4 not assigned!");
        if (Tasks5 == null) Debug.LogError("Tasks5 not assigned!");
        if (Tasks6 == null) Debug.LogError("Tasks6 not assigned!");
    }

    private void ResetTutorial()
    {
        removePlant = false;
        addPotToInventory = false;
        hasMoved = false;
        hasAddedSoil = false;
        hasPlanted = false;
        hasAddedWater = false;
        currentTaskIndex = 0;
        delay = 0f;
        inTut = true;

        // Reset toggles
        if (Tasks1 != null) Tasks1.isOn = false;
        if (Tasks2 != null) Tasks2.isOn = false;
        if (Tasks3 != null) Tasks3.isOn = false;
        if (Tasks4 != null) Tasks4.isOn = false;
        if (Tasks5 != null) Tasks5.isOn = false;
        if (Tasks6 != null) Tasks6.isOn = false;

        // Set initial instruction
        if (TaskLabel != null)
            TaskLabel.text = listOfInstructions[0];

        // Show the tutorial
        ShowUI();
    }

    // Non-static methods for task completion
    public void RemovedPlant()
    {
        if (removePlant) return;
        removePlant = true;
        if (Tasks1 != null)
        {
            Tasks1.isOn = true;
        }
        //CheckAndAdvanceTask();
    }

    public void AddedPotToInventory()
    {
        if (addPotToInventory) return;
        addPotToInventory = true;
        if (Tasks2 != null)
        {
            Tasks2.isOn = true;
        }
        //CheckAndAdvanceTask();
    }

    public void MovePot()
    {
        if (hasMoved) return;
        hasMoved = true;
        if (Tasks3 != null)
        {
            Tasks3.isOn = true;
        }
        //CheckAndAdvanceTask();
    }

    public void AddedSoil()
    {
        if (hasAddedSoil) return;
        hasAddedSoil = true;
        if (Tasks4 != null)
        {
            Tasks4.isOn = true;
        }
        //CheckAndAdvanceTask();
    }

    public void PlantedSeed()
    {
        if (hasPlanted) return;
        hasPlanted = true;
        if (Tasks5 != null)
        {
            Tasks5.isOn = true;
        }
        //CheckAndAdvanceTask();
    }

    public void AddedWater()
    {
        if (hasAddedWater) return;
        hasAddedWater = true;
        if (Tasks6 != null)
        {
            Tasks6.isOn = true;
        }
        //CheckAndAdvanceTask();
    }

    private void CheckAndAdvanceTask()
    {

        if ( currentTaskIndex < listOfInstructions.Length - 1)
        {
            currentTaskIndex++;
            if (TaskLabel != null)
            {
                TaskLabel.text = listOfInstructions[currentTaskIndex];
            }
        }
        else if (tutorialStageComplete())
        {
            // All tasks complete!
            inTut = false;
            if (TaskLabel != null)
                TaskLabel.text = "Tutorial Complete!";

            // Hide UI after delay and load scene
            StartCoroutine(HideUIDelayed(5f));
        }
    }

    private IEnumerator HideUIDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideUI();
        LoadNewScene();
    }

    // Public methods for other scripts to call (instance methods)
    public void OnMove()
    {
        if (currentTaskIndex == 0)
            CheckAndAdvanceTask();
    }

    public void OnPickUpPot()
    {
        if (currentTaskIndex == 1)
            CheckAndAdvanceTask();
    }

    public void FlyOnTable()
    {
        if (currentTaskIndex == 2)
            CheckAndAdvanceTask();
    }

    public void SetGridOnTable()
    {
        if (currentTaskIndex == 3)
            CheckAndAdvanceTask();
    }

    public bool tutorialStageComplete()
    {
        return removePlant && addPotToInventory && hasMoved && 
               hasAddedSoil && hasPlanted && hasAddedWater;
    }

    // UI Visibility Methods - THIS IS WHAT YOU ASKED FOR
    public void ShowUI()
    {
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

    private void LoadNewScene()
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.Log($"Loading scene: {sceneName}");
                SceneManager.LoadScene(sceneName);
            }
            else
            {
                Debug.LogError($"Scene '{sceneName}' is not in Build Settings!");
                Debug.LogError("Please add it via: File → Build Settings → Add Open Scenes");
            }
        }
        else
        {
            Debug.LogError("Scene name is not set!");
        }
    }
}