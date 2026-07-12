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

    void Start(){
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        radioButt = root.Q<RadioButtonGroup>("R_group");
    
    }


}