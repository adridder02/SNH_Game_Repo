using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class UI_Script : MonoBehaviour
{
    [Header("Miasma Reference")]
    public MiasmaController miasma;
    
    [Header("UI Settings")]
    [Tooltip("How long to animate the progress bar (seconds)")]
    public float lerpDuration = 2f; // Changed from 30f to 2f for smoother animation
    
    [Tooltip("Time between intensity increases when plants are in miasma (seconds)")]
    public float intensityIncreaseInterval = 60f;
    
    [Header("Progress Bar Mapping")]
    [Tooltip("What 100% represents on the progress bar")]
    public float maxProgressValue = 100f;
    
    // UI Elements
    private ProgressBar progressBar;
    private Label intensityLabel;
    private Label plantsAffectedLabel;
    private Label nextIncreaseLabel;
    
    // Runtime variables
    private float targetValue;
    private float lerpTimer;
    private float nextIntensityTime;
    
    void Start()
    {
        // Find miasma controller if not assigned
        if (miasma == null)
            miasma = FindObjectOfType<MiasmaController>();
        
        if (miasma == null)
        {
            Debug.LogError("[MiasmaUI] No MiasmaController found in scene!");
            enabled = false;
            return;
        }
        miasma.flipSize();
        // Get UI elements from UIDocument
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        progressBar = root.Q<ProgressBar>("MiasmaProg");
        intensityLabel = root.Q<Label>("IntensityLabel");
        plantsAffectedLabel = root.Q<Label>("PlantsAffected");
        nextIncreaseLabel = root.Q<Label>("NextIncrease");
        
        // Initialize progress bar
        if (progressBar != null)
        {
            progressBar.value = maxProgressValue;
            targetValue = maxProgressValue;
            UpdateProgressBarColor();
        }
        
        // Reset timers
        nextIntensityTime = Time.time + intensityIncreaseInterval;
        //miasma.flipSize();
        // Start periodic UI updates
        StartCoroutine(UpdateUIEverySecond());
    }
    
    void Update()
    {
        if (miasma == null) return;
        
        // Calculate progress bar value based on miasma intensity
        float intensityProgress = GetIntensityProgress();
        targetValue = Mathf.Lerp(maxProgressValue, 0f, intensityProgress);
        
        // Animate towards target
        if (progressBar != null && Mathf.Abs(progressBar.value - targetValue) > 0.1f)
        {
            //miasma.flipSize();
            lerpTimer += Time.deltaTime;
            float t = Mathf.Clamp01(lerpTimer / lerpDuration);
            progressBar.value = Mathf.Lerp(progressBar.value, targetValue, t);
            UpdateProgressBarColor();
        }
        
        // Auto-start miasma growth when UI shows (optional)
        // Uncomment if you want miasma to start growing automatically
        // if (Input.GetKeyDown(KeyCode.Space))
       //  {
        //    miasma.flipSize();
        //}
    }
    
    IEnumerator UpdateUIEverySecond()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            UpdateInfoLabels();
        }
    }
    
    float GetIntensityProgress()
    {
        // Convert intensity to progress (0 = no miasma, 1 = full intensity)
        switch (miasma.currentIntensity)
        {
            case MiasmaController.MiasmaIntensity.Easy:
                return 0.33f;
            case MiasmaController.MiasmaIntensity.Mild:
                return 0.66f;
            case MiasmaController.MiasmaIntensity.Intense:
                return 1.0f;
            default:
                return 0f;
        }
    }
    
    void UpdateProgressBarColor()
    {
        if (progressBar == null) return;
        
        // Change color based on intensity
        if (progressBar.value > 66f)
            progressBar.style.backgroundColor = new StyleColor(Color.green);
        else if (progressBar.value > 33f)
            progressBar.style.backgroundColor = new StyleColor(Color.yellow);
        else
            progressBar.style.backgroundColor = new StyleColor(Color.red);
    }
    
    void UpdateInfoLabels()
    {
        if (miasma == null) return;
        
        // Update intensity label
        if (intensityLabel != null)
        {
            string intensityText = miasma.currentIntensity.ToString().ToUpper();
            Color intensityColor = GetIntensityColor();
            intensityLabel.text = $"MIASMA INTENSITY: <color=#{ColorUtility.ToHtmlStringRGB(intensityColor)}>{intensityText}</color>";
        }
        
        // Update plants affected count
        if (plantsAffectedLabel != null)
        {
            int plantCount = miasma.GetPlantsInMiasmaCount();
            plantsAffectedLabel.text = $"PLANTS AFFECTED: {plantCount}";
        }
        
        // Update next intensity increase timer
        if (nextIncreaseLabel != null && miasma.currentIntensity != MiasmaController.MiasmaIntensity.Intense)
        {
            float timeUntilIncrease = Mathf.Max(0, nextIntensityTime - Time.time);
            if (timeUntilIncrease <= 0 && miasma.GetPlantsInMiasmaCount() > 0)
            {
                nextIntensityTime = Time.time + miasma.timeToIncreaseIntensity;
            }
            nextIncreaseLabel.text = $"NEXT INCREASE: {Mathf.CeilToInt(timeUntilIncrease)}s";
        }
        else if (nextIncreaseLabel != null)
        {
            nextIncreaseLabel.text = "MAX INTENSITY";
        }
    }
    
    Color GetIntensityColor()
    {
        switch (miasma.currentIntensity)
        {
            case MiasmaController.MiasmaIntensity.Easy:
                return Color.gray;
            case MiasmaController.MiasmaIntensity.Mild:
                return new Color(0.7f, 0.3f, 0.7f);
            case MiasmaController.MiasmaIntensity.Intense:
                return new Color(0.9f, 0.1f, 0.9f);
            default:
                return Color.white;
        }
    }
    
    // Public method to manually trigger miasma growth (call from button)
    public void OnGrowMiasmaButton()
    {
        if (miasma != null)
        {
            miasma.flipSize();
            Debug.Log("[MiasmaUI] Miasma growth toggled");
        }
    }
    
    // Public method to reset miasma (call from button)
    public void OnResetMiasmaButton()
    {
        if (miasma != null)
        {
            miasma.ResetIntensity();
            targetValue = maxProgressValue;
            Debug.Log("[MiasmaUI] Miasma reset");
        }
    }
}