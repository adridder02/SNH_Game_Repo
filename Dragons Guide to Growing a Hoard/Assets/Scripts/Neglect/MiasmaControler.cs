using UnityEngine;
using System.Collections.Generic;

public class MiasmaController : MonoBehaviour
{
    public enum MiasmaIntensity { Easy, Mild, Intense }
    
    [Header("Miasma Growth")]
    private bool incSize = false;
    public float rateOfMiasma = 1f;
    
    [Header("Intensity Settings")]
    [Tooltip("Current intensity level")]
    public MiasmaIntensity currentIntensity = MiasmaIntensity.Easy;
    
    [Tooltip("Time in contact with plants to increase intensity (seconds)")]
    public float timeToIncreaseIntensity = 120f;
    
    [Tooltip("Easy intensity debuff values (applied every minute)")]
    public float easyLightPenalty = 0.01f;
    public int easySoilPenalty = 1;
    public float easyWaterDrainMultiplier = .02f;
    
    [Tooltip("Mild intensity debuff values")]
    public float mildLightPenalty = 0.02f;
    public int mildSoilPenalty = 1;
    public float mildWaterDrainMultiplier = .05f;
    
    [Tooltip("Intense intensity debuff values")]
    public float intenseLightPenalty = 0.3f;
    public int intenseSoilPenalty = 1;
    public float intenseWaterDrainMultiplier = .05f;
    
    [Header("Timing")]
    [Tooltip("How often to apply debuff ticks (seconds)")]
    public float debuffTickInterval = 60f;
    
    [Header("Visuals")]
    public Color easyColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    public Color mildColor = new Color(0.7f, 0.3f, 0.7f, 0.4f);
    public Color intenseColor = new Color(0.9f, 0.1f, 0.9f, 0.5f);
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    // Runtime variables
    private float contactTimer = 0f;
    private HashSet<PlantState> plantsInMiasma = new HashSet<PlantState>();
    private Dictionary<PlantState, float> lastDebuffTime = new Dictionary<PlantState, float>();
    private Renderer sphereRenderer;
    private Vector3 originalScale;
    private float currentSize = 1f;

    /// <summary>Current miasma sphere size (matches transform.localScale.x). Read by MainUIController for the miasma bar.</summary>
    public float CurrentSize => currentSize;

    /// <summary>Upper bound currentSize will grow to. Read by MainUIController to normalize the miasma bar.</summary>
    public float MaxSize => maxSize;

    void Start()
    {
        // Double the size of the object
        transform.localScale = new Vector3(2f, 2f, 2f);
        originalScale = transform.localScale;
        currentSize = originalScale.x;
        
        // Get renderer for visual feedback
        sphereRenderer = GetComponent<Renderer>();
        if (sphereRenderer == null)
            sphereRenderer = GetComponentInChildren<Renderer>();
        
        // Disable shadows so light passes through
        if (sphereRenderer != null)
        {
            sphereRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sphereRenderer.receiveShadows = false;
        }
        
        UpdateVisuals();
        
        if (showDebugLogs)
            Debug.Log($"[MiasmaController] Initialized at size {currentSize}, Intensity: {currentIntensity}");
    }
    private float growthTimer = 0f;
    private float growthInterval = 5f; // Grow every 5 seconds
    float maxSize = 250f;
    float minSize = 0f;
    void Update()
    {
        // Gradually increase size over time
        if (incSize)
        {
            growthTimer += Time.deltaTime;
        
            if (growthTimer >= growthInterval)
                    growthTimer = 0f;
            else{
                // Apply growth once every 5 seconds
                if((transform.localScale.x< minSize && rateOfMiasma<0) || (transform.localScale.x>maxSize && rateOfMiasma>0))
                    rateOfMiasma = 0;
                transform.localScale += new Vector3(rateOfMiasma, rateOfMiasma, rateOfMiasma)*Time.deltaTime;
                currentSize = transform.localScale.x;
                
                // Debug.Log($"Miasma grew to: {currentSize}");
            }
        
        }
        
        // Check for plants in miasma
        CheckForPlants();
        
        // Update intensity based on contact
        UpdateIntensity();
        
        // Apply debuffs to plants (every minute)
        ApplyDebuffsToPlants();
        
        // Update visual appearance based on intensity
        UpdateVisuals();
    }
    
    void CheckForPlants()
    {
        // Get all plants within the miasma sphere
        Collider[] hits = Physics.OverlapSphere(transform.position, currentSize);
        HashSet<PlantState> currentPlants = new HashSet<PlantState>();
        
        foreach (Collider hit in hits)
        {
            // Try to get PlantState from the hit object or its parent
            PlantState plant = hit.GetComponent<PlantState>();
            if (plant == null)
                plant = hit.GetComponentInParent<PlantState>();
                
            if (plant != null)
            {
                currentPlants.Add(plant);
                
                // New plant entered
                if (!plantsInMiasma.Contains(plant))
                {
                    plantsInMiasma.Add(plant);
                    lastDebuffTime[plant] = Time.time;
                    if (showDebugLogs)
                        Debug.Log($"[MiasmaController] {plant.gameObject.name} entered miasma - Intensity: {currentIntensity}");
                }
            }
        }
        
        // Remove plants that left
        List<PlantState> toRemove = new List<PlantState>();
        foreach (var plant in plantsInMiasma)
        {
            if (plant == null || !currentPlants.Contains(plant))
            {
                toRemove.Add(plant);
                if (showDebugLogs && plant != null)
                    Debug.Log($"[MiasmaController] {plant.gameObject.name} left miasma");
            }
        }
        
        foreach (var plant in toRemove)
        {
            plantsInMiasma.Remove(plant);
            lastDebuffTime.Remove(plant);
        }
    }
    
    void UpdateIntensity()
    {
        if (plantsInMiasma.Count > 0)
        {
            // Increase contact timer when plants are inside
            contactTimer += Time.deltaTime;
            
            if (contactTimer >= timeToIncreaseIntensity)
            {
                // Reset timer and increase intensity
                contactTimer = 0f;
                
                switch (currentIntensity)
                {
                    case MiasmaIntensity.Easy:
                        currentIntensity = MiasmaIntensity.Mild;
                        if (showDebugLogs)
                            Debug.Log("<color=purple>[MiasmaController] Intensity increased to MILD!</color>");
                        break;
                    case MiasmaIntensity.Mild:
                        currentIntensity = MiasmaIntensity.Intense;
                        if (showDebugLogs)
                            Debug.Log("<color=red>[MiasmaController] Intensity increased to INTENSE!</color>");
                        break;
                }
            }
        }
        else
        {
            // No plants in miasma - reset to Easy
            if (currentIntensity != MiasmaIntensity.Easy)
            {
                currentIntensity = MiasmaIntensity.Easy;
                contactTimer = 0f;
                if (showDebugLogs)
                    Debug.Log("[MiasmaController] Intensity reset to EASY (no plants in area)");
            }
        }
    }
    
    void ApplyDebuffsToPlants()
    {
        float currentTime = Time.time;
        
        foreach (var plant in plantsInMiasma)
        {
            if (plant == null) continue;
            
            // Check if it's time to apply debuff (every minute)
            if (lastDebuffTime.TryGetValue(plant, out float lastTime))
            {
                if (currentTime - lastTime >= debuffTickInterval)
                {
                    ApplyDebuffToPlant(plant);
                    lastDebuffTime[plant] = currentTime;
                }
            }
        }
    }
    
    void ApplyDebuffToPlant(PlantState plant)
    {
        // Get current intensity values
        float lightPenalty = 0f;
        int soilPenalty = 0;
        float waterDrainMultiplier = 1f;
        
        switch (currentIntensity)
        {
            case MiasmaIntensity.Easy:
                lightPenalty = easyLightPenalty;
                soilPenalty = easySoilPenalty;
                waterDrainMultiplier = easyWaterDrainMultiplier;
                break;
            case MiasmaIntensity.Mild:
                lightPenalty = mildLightPenalty;
                soilPenalty = mildSoilPenalty;
                waterDrainMultiplier = mildWaterDrainMultiplier;
                break;
            case MiasmaIntensity.Intense:
                lightPenalty = intenseLightPenalty;
                soilPenalty = intenseSoilPenalty;
                waterDrainMultiplier = intenseWaterDrainMultiplier;
                break;
        }
        
        // Apply the debuff to the plant
        plant.ApplyMiasmaDebuff(lightPenalty, soilPenalty, waterDrainMultiplier);
        
        if (showDebugLogs)
        {
            Debug.Log($"[MiasmaController] Debuff applied to {plant.gameObject.name} - " +
                      $"Intensity: {currentIntensity}, Light: -{lightPenalty}, Soil: -{soilPenalty}, Water Drain: x{waterDrainMultiplier}");
        }
    }
    
    void UpdateVisuals()
    {
        if (sphereRenderer == null) return;
        
        switch (currentIntensity)
        {
            case MiasmaIntensity.Easy:
                sphereRenderer.material.color = easyColor;
                break;
            case MiasmaIntensity.Mild:
                sphereRenderer.material.color = mildColor;
                break;
            case MiasmaIntensity.Intense:
                sphereRenderer.material.color = intenseColor;
                break;
        }
    }
    
    // Public methods to control miasma
    public void flipSize()
    {
        this.incSize = !incSize;
        if (showDebugLogs)
            Debug.Log($"[MiasmaController] Size growth: {(incSize ? "STARTED" : "STOPPED")}");
    }
    
    public void SetGrowthRate(float newRate)
    {
        rateOfMiasma = newRate;
        if (showDebugLogs)
            Debug.Log($"[MiasmaController] Growth rate set to {newRate}");
    }
    
    public void ResetIntensity()
    {
        currentIntensity = MiasmaIntensity.Easy;
        contactTimer = 0f;
        if (showDebugLogs)
            Debug.Log("[MiasmaController] Intensity reset to EASY");
    }
    
    public int GetPlantsInMiasmaCount()
    {
        return plantsInMiasma.Count;
    }
    
    void OnDrawGizmosSelected()
    {
        // Visualize the miasma radius in editor
        Gizmos.color = new Color(0.5f, 0f, 0.5f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, transform.localScale.x);
    }
}