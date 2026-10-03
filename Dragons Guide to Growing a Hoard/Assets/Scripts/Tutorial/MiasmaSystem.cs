using UnityEngine;
using UnityEngine.UIElements;

// =============================================================
// MiasmaSystem.cs
// -------------------------------------------------------------
// Pulled out of Tutorial_2.cs — this is gameplay/HUD logic (the
// plant counter label + the miasma progress bar), not part of the
// on-screen checklist that's being torn out. It has nothing to do
// with mission/task completion, so it doesn't touch
// MissionProgressManager at all.
//
// REPOINTING: wherever code used to call the old static methods on
// Tutorial_2, swap to the instance calls below via the singleton:
//   Tutorial_2.addPlantcounter()    -> MiasmaSystem.Instance.AddPlant()
//   Tutorial_2.removePlantcounter() -> MiasmaSystem.Instance.RemovePlant()
//
// NOTE: `stage` and `numberOFPlants` from the original were declared
// but never actually read anywhere, so they were dropped rather than
// carried over as dead fields. Shout if either was meant to do
// something and got missed.
// =============================================================
public class MiasmaSystem : MonoBehaviour
{
    public static MiasmaSystem Instance { get; private set; }

    [Header("UXML Element Names")]
    [SerializeField] private string counterLabelName = "count";
    [SerializeField] private string miasmaMeterName = "miasma";

    [Header("Miasma Tuning")]
    [SerializeField] private float miasmaStart = 100f;
    [SerializeField] private float miasmaTickInterval = 1f;

    private Label counterLabel;
    private ProgressBar miasmaMeter;

    private bool decreaseMiasma = false;
    private float miasmaValue;
    private float timerMiasma = 0f;
    private int countPlants = 0;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        miasmaValue = miasmaStart;
    }

    void Start()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        counterLabel = root.Q<Label>(counterLabelName);
        miasmaMeter = root.Q<ProgressBar>(miasmaMeterName);
    }

    void Update()
    {
        timerMiasma += Time.deltaTime;
        if (timerMiasma >= miasmaTickInterval)
        {
            MiasmaControl();
            timerMiasma = 0f;
        }
    }

    public void AddPlant()
    {
        countPlants++;
        if (counterLabel != null)
            counterLabel.text = "" + countPlants;

        // Same thresholds as the original: more plants under a healthy miasma
        // level starts the bar decreasing.
        if (countPlants > 2 && miasmaValue > 66f)
            decreaseMiasma = true;
        if (countPlants > 5 && miasmaValue > 33f)
            decreaseMiasma = true;
        if (countPlants > 8 && miasmaValue > 0f && miasmaValue < 33f)
            decreaseMiasma = true;
    }

    public void RemovePlant()
    {
        countPlants--;
        if (counterLabel != null)
            counterLabel.text = "" + countPlants;
    }

    private void MiasmaControl()
    {
        if (!decreaseMiasma) return;

        miasmaValue -= 1f;

        if (miasmaValue >= 33f && miasmaValue <= 66f)
        {
            decreaseMiasma = false;
        }
        else if (miasmaValue <= 33f)
        {
            decreaseMiasma = false;
        }
        else if (miasmaValue <= 0f)
        {
            decreaseMiasma = false;
            if (miasmaMeter != null) miasmaMeter.value = 0;
            return;
        }

        if (miasmaMeter != null) miasmaMeter.value = miasmaValue;
    }
}
