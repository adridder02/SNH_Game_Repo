using UnityEngine;
using UnityEngine.InputSystem;

// =============================================================
// LorePickup.cs
// -------------------------------------------------------------
// A lore item lying in the world (plaque, note, journal page...). Walk up, press E: it's added to the
// journal's Lore bookmark and the journal opens on it so it can be read; the back button closes it.
// If it was already collected in an earlier session, the object is simply hidden at start.
//
// SETUP:
//   1. Put this on the world object (needs to be somewhere the player can walk up to).
//   2. Assign `entry` = a MissionData asset with Entry Type = Lore (and add that asset to the Lore
//      Database on the GuideUIController).
//   3. Optional: `visual` = what to hide on pickup (defaults to this whole object).
// =============================================================
public class LorePickup : MonoBehaviour
{
    [SerializeField] private MissionData entry;

    [Header("Interaction")]
    [SerializeField] private float interactRange = 3f;
    [Tooltip("The player's transform. Auto-found via PlayerController if left empty.")]
    [SerializeField] private Transform player;
    [SerializeField] private bool showInteractPrompt = true;

    [Header("After pickup")]
    [Tooltip("Object to hide when collected. Leave empty to hide this whole GameObject.")]
    [SerializeField] private GameObject visual;
    [SerializeField] private bool openJournalOnPickup = true;

    private MissionProgressManager progress;
    private JournalUIController journal;
    private MainUIController mainUI;
    private bool inRange;

    private void Start()
    {
        progress = MissionProgressManager.Instance != null ? MissionProgressManager.Instance : FindAnyObjectByType<MissionProgressManager>();
        journal = FindAnyObjectByType<JournalUIController>();
        mainUI = FindAnyObjectByType<MainUIController>();

        if (player == null)
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }

        if (entry != null && progress != null && progress.IsLoreCollected(entry))
            Hide();
    }

    private void Update()
    {
        if (entry == null || player == null || progress == null) return;

        bool menuOpen = journal != null && journal.IsJournalOpen;
        bool now = !menuOpen && Vector3.Distance(transform.position, player.position) <= interactRange;

        if (now != inRange)
        {
            inRange = now;
            if (showInteractPrompt) mainUI?.SetInteractPromptVisible(inRange, this);
        }

        if (inRange && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            Collect();
    }

    private void OnDisable()
    {
        if (inRange) mainUI?.SetInteractPromptVisible(false, this);
        inRange = false;
    }

    private void Collect()
    {
        progress.CollectLore(entry);

        if (showInteractPrompt) mainUI?.SetInteractPromptVisible(false, this);
        inRange = false;

        if (openJournalOnPickup) journal?.OpenLoreEntry(entry);
        Hide();
    }

    private void Hide()
    {
        GameObject target = visual != null ? visual : gameObject;
        // If this component is on the object being hidden it stops running, which is what we want.
        target.SetActive(false);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.25f);
        Gizmos.DrawSphere(transform.position, interactRange);
    }
#endif
}
