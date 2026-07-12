using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement; // for scene loading
//using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Tutorial_2 : MonoBehaviour
{
    private static int numberOFPlants = 0;
    private static RadioButtonGroup radioButt;
    private static Label tipBit;
    private static Label counter;
    private static Toggle inventory;
    private static Toggle sp;
    private static Toggle mp;
    private static Toggle lp;

    private static string[] strtipBit = {
        "Follow the arrow and collect all interactive plants glowing red",
        "Press 'I' to open Inventory.",
        "Open the binder, to see all collected plants.",
        "Ensure that the miasma bar doesn't affect the plants"
    };
    private static float timerSwitch = 0f; 
    private static float TimerDelay = 10f; 
    private static int couresellCounter =0;

    private static int countPlants =0;

    void Start(){
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        //radioButt = root.Q<RadioButtonGroup>("R_group");
        Debug.Log(root.childCount);
        tipBit = root.Q<Label>("tipbit");
        counter = root.Q<Label>("count");

        inventory = root.Q<Toggle>("Inv");
        sp = root.Q<Toggle>("sp");
        mp = root.Q<Toggle>("mp");
        lp = root.Q<Toggle>("lp");
        Debug.Log(tipBit);
        
        if(tipBit != null)
            tipBit.text = strtipBit[couresellCounter];
    }

    void Update(){
        timerSwitch +=  Time.deltaTime; 
        if(timerSwitch>=TimerDelay){
            timerSwitch = 0f;
            Debug.Log("Made It Here");
            nextBinder();
        }

    }

    private static void nextBinder(){
        couresellCounter++;
        if(couresellCounter >= 4)
            couresellCounter = 0;
        
        tipBit.text = strtipBit[couresellCounter];
    }

    private static void plantSmallPot(){}
    private static void plantMediumPot(){}
    private static void plantLargePot(){}
    private static void addPlantcounter(){
        countPlants++;
        counter.text = ""+countPlants;
    }
    private void openedInventory(){
        if(inventory != null)
            inventory.value = true;
    }
    private void plantedSp(){
        if(sp != null)
            sp.value = true;
    }
    private void plantedMp(){
        if(mp != null)
            mp.value = true;
    }
    private void plantedLp(){
        if(lp != null)
            lp.value = true;
    }

}