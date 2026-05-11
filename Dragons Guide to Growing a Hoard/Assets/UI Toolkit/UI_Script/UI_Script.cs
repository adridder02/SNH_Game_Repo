using UnityEngine;
using UnityEngine.UIElements;

public class UI_Script : MonoBehaviour
{
    public MiasmaControler miasma;
    private float targetValue;
    private float lerpDuration = 30f; // time to animate down
    private float lerpTimer;

    private float interval = 60f; // wait time before each drop
    private float intervalTimer;

    private int rateOfMiasma;
    private ProgressBar progressBar;
    void Start()
    {
        // Get the ProgressBar from your UIDocument
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        progressBar = root.Q<ProgressBar>("MiasmaProg"); // match the name in UXML

        progressBar.value = 100f; // start full
        targetValue = 100f;
    }

    void Update()
    {
        // Count down the interval
        intervalTimer += Time.deltaTime;
        if (intervalTimer >= interval)
        {
            intervalTimer = 0f;
            // Drop target by 25%
            miasma.flipSize();
            targetValue = Mathf.Max(0f, targetValue - 25f);
            lerpTimer = 0f; // reset animation timer
        }

        // Animate towards target
        if (progressBar.value > targetValue)
        {
            miasma.flipSize();
            lerpTimer += Time.deltaTime;
            float t = Mathf.Clamp01(lerpTimer / lerpDuration);
            progressBar.value = Mathf.Lerp(progressBar.value, targetValue, t);
        }
    }

}
