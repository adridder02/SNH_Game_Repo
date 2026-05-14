using UnityEngine;

public class Zone_Miasma : MonoBehaviour
{
    [Header("Miasma Object")]
    public MiasmaController miasma;

    [Header("Zones In the Level")]
    public ZoneHealth z1;
    public ZoneHealth z2;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private bool reseT25R = true;
    private bool reseT50R = true;
    private bool reseT75R = true;
 
    void Start()
    {
        
    }

    private float checkTimer = 0f;
    private float checkInterval = 1f; // Check every second
    // Update is called once per frame
    void Update()
    {
        checkTimer += Time.deltaTime;
    
        if (checkTimer >= checkInterval)
        {
            checkTimer = 0f;
            calcualteMiasmaControl(); // Call your function every second
        }
    }

    public void calcualteMiasmaControl()
    {
        float averageZoneHappiness = (z1.ZoneHappiness + z2.ZoneHappiness)/2;
        if(averageZoneHappiness>25 &&averageZoneHappiness < 50){
            miasma.rateOfMiasma =.5f;
            if (reseT25R)
            {
                miasma.currentIntensity = MiasmaController.MiasmaIntensity.Easy;
                miasma.timeToIncreaseIntensity= 120f;
                reseT25R = !reseT25R;
            }
        }
        else if(averageZoneHappiness>=50 && averageZoneHappiness<=75){
            miasma.rateOfMiasma =-.5f;
            if (reseT50R)
            {
                miasma.currentIntensity = MiasmaController.MiasmaIntensity.Easy;
                miasma.timeToIncreaseIntensity= 120f;
                reseT50R = !reseT50R;
            }
        }
        else if(averageZoneHappiness >=75){
            miasma.rateOfMiasma =-1f;
            if (reseT75R)
            {
                miasma.currentIntensity = MiasmaController.MiasmaIntensity.Easy;
                miasma.timeToIncreaseIntensity= 120f;
                reseT75R = !reseT75R;
            }
        }
        else miasma.rateOfMiasma = 1f;

    }
}
