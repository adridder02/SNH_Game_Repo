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
    private static bool inv = false;
    private static bool spBool = false;
    private static bool mpBool = false;
    private static bool lpBool = false;

    private static bool decreaseMiasma = false;
    private static float miasmaStart = 100f;
    private static float stage = 33f;
    private static ProgressBar miasmaMeter;

    private static string[] strtipBit = {
        "Follow the arrow and collect all interactive plants glowing yellow",
        "Harvesed plants can be viewed in Inventory, press 'I' to open Inventory.",
        "Open the binder, to see all collected plants. and explored plants",
        "Ensure that the miasma bar doesn't affect the plants plant stratigicly to avoid affects"
    };
    private static float timerSwitch = 0f; 
    private static float timerMiasma = 0f; 
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
        
        miasmaMeter = root.Q<ProgressBar>("miasma");
        
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
        timerMiasma += Time.deltaTime;
        if(timerMiasma >= 1){
            miasmaControl();
            timerMiasma = 0f;
        }



    }

    private static void nextBinder(){
        couresellCounter++;
        if(couresellCounter >= 4)
            couresellCounter = 0;
        
        tipBit.text = strtipBit[couresellCounter];
    }

    /*
        private static void plantSmallPot(){}
        private static void plantMediumPot(){}
        private static void plantLargePot(){}
    */
    public  static void addPlantcounter(){
        countPlants++;
        if(counter != null)
            counter.text = ""+countPlants;
        if(countPlants >2 && miasmaStart> 66f){
            decreaseMiasma = true;
        }
        if(countPlants >5 && miasmaStart> 33f){
            decreaseMiasma = true;
        }
        if(countPlants >8 && miasmaStart> 0f && miasmaStart< 33f){
            decreaseMiasma = true;
        }
    }
    public  static void removePlantcounter(){
        countPlants--;
        if(counter != null)
            counter.text = ""+countPlants;
    }
    public static void openedInventory(){
        if(inv)
            return;
        if(inventory != null)
            inventory.value = true;
            inv = true;
    }
    public static void plantedSp(){
        if(spBool)
            return;
        if(sp != null)
            sp.value = true;
        spBool = false;
    }
    public static void plantedMp(){
        if(mpBool)
            return;
        if(mp != null)
            mp.value = true;
        mpBool = true;
    }
    public static void plantedLp(){
        if(lpBool)
            return;
        if(lp != null)
            lp.value = true;
        lpBool = true;
    }
    public static void miasmaControl(){
        if(!decreaseMiasma)
            return;
        miasmaStart -=1f;
        if(miasmaStart>= 33f&&miasmaStart<=66f){
            decreaseMiasma = false;
        }
        else if(miasmaStart<= 33f){
            decreaseMiasma = false;

        }
        else if(miasmaStart<=0){
            decreaseMiasma = false;
            miasmaMeter.value = 0;
            return;
        }
        miasmaMeter.value = miasmaStart;

    }

}