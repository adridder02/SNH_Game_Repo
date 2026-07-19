using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement; // for scene loading
//using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Tutorial : MonoBehaviour
{
    [Header("SceneName")]
    #if UNITY_EDITOR
    [SerializeField] private SceneAsset sceneToLoad;
    #endif
    [SerializeField] private string sceneName = "";

    public static bool inTut = true;
    private static bool removePlant;
    private static bool addPotToInventory;
    private static bool hasMoved;
    private static bool hasAddedSoil;
    private static bool hasPlanted;
    private static bool hasAddedWater;
    private static  string[] listOfInstructions = {
        "use WASD to move around",
        "approach the Plant and press 'E' to harvest it",
        "use your wings by doule tapping 'Space Bar' to move the plant on top of the table to plant in an ideal location",
        "Once on the table press F to place a pot on the table"
    };


    private static Toggle Tasks1;
    private static Toggle Tasks2;
    private static Toggle Tasks3;
    private static Toggle Tasks4;
    private static Toggle Tasks5;
    private static Toggle Tasks6;
    private static Label TaskLabel;
    private static int currentTaskIndex = 0;
    private static float delay = 0f;
    void Start()
    {

        removePlant = false;
        addPotToInventory = false;
        hasMoved = false;
        hasAddedSoil = false;
        hasPlanted = false;
        hasAddedWater = false;
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        TaskLabel = root.Q<Label>("Dir");
        GroupBox tutBox = root.Q<GroupBox>("TaskList");
        Tasks1 = tutBox.Q<Toggle>("Task1");
        Tasks2 = tutBox.Q<Toggle>("Task2");
        Tasks3 = tutBox.Q<Toggle>("Task3");
        Tasks4 = tutBox.Q<Toggle>("Task4");
        Tasks5 = tutBox.Q<Toggle>("Task5");
        Tasks6 = tutBox.Q<Toggle>("Task6");
        
        TaskLabel.style.whiteSpace = WhiteSpace.Normal;  // Allows wrapping
        TaskLabel.style.flexWrap = Wrap.Wrap;            // Ensures wrapping
        TaskLabel.text = listOfInstructions[currentTaskIndex];
    }


    public  static void removedPlant(){//Done in PotContents.cs
        if(removePlant)
            return;

        removePlant = true;
        if(Tasks1 != null){
            Tasks1.value = true;
            Tasks1.MarkDirtyRepaint();
        }
    }
    public static  void addedPotToInventory(){//Done in PlacementSystem.cs
        if(addPotToInventory)
            return;
        addPotToInventory = true;
        if(Tasks2 != null){
            Tasks2.value = true;
            Tasks2.MarkDirtyRepaint();
        }
    }
    
    public static void movePot(){//Done in PlacementSystem.cs
        if(hasMoved)
            return;

        hasMoved = true;
        if(Tasks3 != null){
            Tasks3.value = true;
            Tasks3.MarkDirtyRepaint();
        }
    }
    public static void addedSoil(){//Done in PotContents.cs
        if(hasAddedSoil)
            return;

        hasAddedSoil = true;
        if(Tasks4 != null){
            Tasks4.value = true;
            Tasks4.MarkDirtyRepaint();
        }
    }
    public static void plantedSeed(){//Done in PotContents.cs
        if(hasPlanted)
            return;

        hasPlanted = true;
        if(Tasks5 != null){
            Tasks5.value = true;
            Tasks5.MarkDirtyRepaint();
        }
    }
    public static void addedWater(){//Done in PotContents.cs
        if(hasAddedWater)
            return;

        hasAddedWater = true;
        if(Tasks6 != null){
            Tasks6.value = true;
            Tasks6.MarkDirtyRepaint();
        }
    }
    private static void nextTask(){
        if(currentTaskIndex < listOfInstructions.Length - 1 && TaskLabel != null){
            currentTaskIndex++;
            TaskLabel.text = listOfInstructions[currentTaskIndex];
        }
        else{
            if(TaskLabel != null)
                TaskLabel.style.display = DisplayStyle.None;
        }
    }
    public static void onMove(){
        if(currentTaskIndex == 0)
            nextTask();
    }
    public static void onPickUpPot(){
        if(currentTaskIndex == 1)
            nextTask();
    }
    public static void FlyOnTable(){
        if(currentTaskIndex == 2)
            nextTask();
    }
    public static void setGridOnTable(){
        if(currentTaskIndex == 3)
            nextTask();
    }

    public static bool tutorialStageComplete(){
        return removePlant && addPotToInventory && hasMoved && hasAddedSoil && hasPlanted && hasAddedWater;
    }

    /*private void Update(){
        if(tutorialStageComplete()){
            Debug.Log("Tutorial Complete: " + delay);
            inTut = false;
            //gameObject.SetActive(false);
            delay += Time.deltaTime;
            if (delay >= 5f)
            {
                LoadNewScene();
            }
        } 
    }*/
    private void LoadNewScene()
    {
        // Use SceneManager for both Editor and builds
        if (!string.IsNullOrEmpty(sceneName))
        {
            // Check if scene is in Build Settings
            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.Log($"Loading scene: {sceneName}");
                SceneManager.LoadScene(sceneName);
                return;
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
