using UnityEngine;
using UnityEngine.UIElements;
//using UnityEngine.UI;


public class Tutorial : MonoBehaviour
{
    private bool removePlant;
    private bool addPotToInventory;
    private bool hasMoved;
    private bool hasAddedSoil;
    private bool hasPlanted;
    private bool hasAddedWater;

    private Toggle Tasks1;
    private Toggle Tasks2;
    private Toggle Tasks3;
    private Toggle Tasks4;
    private Toggle Tasks5;
    private Toggle Tasks6;


    void Start()
    {

        this.removePlant = false;
        this.addPotToInventory = false;
        this.hasMoved = false;
        this.hasAddedSoil = false;
        this.hasPlanted = false;
        this.hasAddedWater = false;
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        GroupBox tutBox = root.Q<GroupBox>("TaskList");
        Tasks1 = tutBox.Q<Toggle>("Task1");
        Tasks2 = tutBox.Q<Toggle>("Task2");
        Tasks3 = tutBox.Q<Toggle>("Task3");
        Tasks4 = tutBox.Q<Toggle>("Task4");
        Tasks5 = tutBox.Q<Toggle>("Task5");
        Tasks6 = tutBox.Q<Toggle>("Task6");
    }


    public void removedPlant(){//Done in PotContents.cs
        if(this.removePlant)
            return;

        this.removePlant = true;
        if(Tasks1 != null){
            Tasks1.value = true;
            Tasks1.MarkDirtyRepaint();
        }
    }
    public void addedPotToInventory(){//Done in PlacementSystem.cs
        if(this.addPotToInventory)
            return;
        this.addPotToInventory = true;
        if(Tasks2 != null){
            Tasks2.value = true;
            Tasks2.MarkDirtyRepaint();
        }
    }
    
    public void movePot(){//Done in PlacementSystem.cs
        if(this.hasMoved)
            return;

        this.hasMoved = true;
        if(Tasks3 != null){
            Tasks3.value = true;
            Tasks3.MarkDirtyRepaint();
        }
    }
    public void addedSoil(){//Done in PotContents.cs
        if(this.hasAddedSoil)
            return;

        this.hasAddedSoil = true;
        if(Tasks4 != null){
            Tasks4.value = true;
            Tasks4.MarkDirtyRepaint();
        }
    }
    public void plantedSeed(){//Done in PotContents.cs
        if(this.hasPlanted)
            return;

        this.hasPlanted = true;
        if(Tasks5 != null){
            Tasks5.value = true;
            Tasks5.MarkDirtyRepaint();
        }
    }
    public void addedWater(){//Done in PotContents.cs
        if(this.hasAddedWater)
            return;

        this.hasAddedWater = true;
        if(Tasks6 != null){
            Tasks6.value = true;
            Tasks6.MarkDirtyRepaint();
        }
    }
}
