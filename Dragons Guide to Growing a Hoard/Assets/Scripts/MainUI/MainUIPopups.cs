using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =============================================================
// MainUIPopups.cs
// -------------------------------------------------------------
// Three pieces of main-UI feedback in one component:
//
//   1. UNDERWATER PANEL   — shown (with a fade) while the camera is under water.
//   2. FIRST-PICKUP POPUP — the first time the player ever picks up a given plant or ability item
//                           (consumables, placeables...): icon + name + short description, counts down
//                           and closes itself after `popupSeconds` (default 5).
//   3. PLANT COMPLETED    — the first time a plant is fully grown ("Plant completed successfully!"),
//                           with its own panel and a sound effect.
//
// Popups queue, so two pickups in the same moment show one after the other instead of overlapping.
// "Seen" lists are saved to PlayerPrefs (and are part of the save file, see SaveRegistry), so a popup
// never repeats after Continue, and New Game starts them fresh.
//
// SETUP: see the PopupPanel tooltip below, or the instructions that came with this script.
// Put this component on any always-active object under your main Canvas (e.g. the MainUI root).
// =============================================================
public class MainUIPopups : MonoBehaviour
{
    public const string PickupSeenKey = "FirstPickup_Seen";
    public const string CompletedSeenKey = "PlantCompleted_Seen";

    // ---------------------------------------------------------------
    // Panel description
    // ---------------------------------------------------------------
    [Serializable]
    public class PopupPanel
    {
        [Tooltip("The panel's root object. It is switched off until a popup is shown, so you can leave it " +
                 "disabled in the scene. A CanvasGroup is added automatically for the fade.")]
        public GameObject root;
        [Tooltip("Optional big icon (plant or item picture).")]
        public Image icon;
        [Tooltip("Optional heading, e.g. 'New Item' or 'Plant Completed Successfully!'. Leave empty to keep " +
                 "whatever text you typed in the editor.")]
        public TMP_Text titleText;
        [Tooltip("Item / plant name.")]
        public TMP_Text nameText;
        [Tooltip("Short description under the name.")]
        public TMP_Text descriptionText;
        [Tooltip("Optional text showing the seconds left, e.g. 'Closing in 4'.")]
        public TMP_Text countdownText;
        [Tooltip("Optional Image (Type = Filled) that drains from full to empty over the countdown.")]
        public Image countdownFill;
        [Tooltip("Optional button that closes the popup early.")]
        public Button closeButton;
    }

    // ---------------------------------------------------------------
    // Underwater
    // ---------------------------------------------------------------
    [Header("Underwater Panel")]
    [Tooltip("Panel shown while the camera is under water. Leave empty to disable the feature.")]
    [SerializeField] private GameObject underwaterPanel;

    [Tooltip("Camera that counts as 'the player's eyes'. Leave empty to use Camera.main.")]
    [SerializeField] private Camera viewCamera;

    [Tooltip("Option A (best): trigger/box colliders that fill your water. The camera is underwater when its " +
             "position is inside any of them. Works for ponds, pools and tanks.")]
    [SerializeField] private Collider[] waterVolumes;

    [Tooltip("Option B: one flat water surface. The camera is underwater when it is BELOW this object's Y. " +
             "Used only if no water volumes are assigned.")]
    [SerializeField] private Transform waterSurface;

    [SerializeField] private float underwaterFadeSeconds = 0.25f;

    // ---------------------------------------------------------------
    // Item popups
    // ---------------------------------------------------------------
    [Header("First Pickup Popup")]
    [SerializeField] private PopupPanel pickupPanel = new PopupPanel();
    [SerializeField] private string pickupTitle = "New Item";
    [Tooltip("Seconds the popup stays up before closing itself.")]
    [SerializeField] private float popupSeconds = 5f;
    [Tooltip("Text for the countdown. {0} is the whole seconds left.")]
    [SerializeField] private string countdownFormat = "Closing in {0}";
    [SerializeField] private AudioClip pickupSound;

    [Header("Plant Completed Popup")]
    [SerializeField] private PopupPanel completedPanel = new PopupPanel();
    [SerializeField] private string completedTitle = "Plant Completed Successfully!";
    [Tooltip("On = the popup appears the first time EACH species is completed. Off = only the very first " +
             "plant you ever complete.")]
    [SerializeField] private bool oncePerSpecies = true;
    [Tooltip("Seconds the completion popup stays up. Longer than a pickup so it feels like a reward.")]
    [SerializeField] private float completedSeconds = 6f;
    [SerializeField] private AudioClip completedSound;

    [Header("Audio")]
    [Tooltip("Plays the sounds. Leave empty and one is created on this object (the AudioMixerRouter sends it " +
             "to the Sfx slider automatically).")]
    [SerializeField] private AudioSource audioSource;
    [Range(0f, 1f)] [SerializeField] private float soundVolume = 1f;

    [Header("Animation")]
    [SerializeField] private float popInSeconds = 0.3f;
    [SerializeField] private float fadeOutSeconds = 0.25f;
    [Tooltip("Seconds to wait after the trigger before the popup appears (lets the pickup finish first).")]
    [SerializeField] private float startDelay = 0.25f;

    [Header("Testing (right-click this component → Test ...)")]
    [SerializeField] private PlantSpeciesData debugSpecies;
    [SerializeField] private AbilityItemData debugItem;

    // ---------------------------------------------------------------
    // Runtime
    // ---------------------------------------------------------------
    private enum Kind { Pickup, Completed }

    private struct Request
    {
        public Kind kind;
        public string name, description;
        public Sprite icon;
    }

    private readonly Queue<Request> queue = new Queue<Request>();
    private readonly HashSet<string> seenPickups = new HashSet<string>();
    private readonly HashSet<string> seenCompleted = new HashSet<string>();
    private Coroutine runner;
    private bool closeRequested;

    private CanvasGroup underwaterGroup;
    private bool underwaterNow;
    private float underwaterTimer;

    // ---------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------
    void Awake()
    {
        LoadSeen(PickupSeenKey, seenPickups);
        LoadSeen(CompletedSeenKey, seenCompleted);

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        if (underwaterPanel != null)
        {
            underwaterGroup = GetOrAddGroup(underwaterPanel);
            underwaterGroup.alpha = 0f;
            underwaterPanel.SetActive(false);
        }

        HidePanel(pickupPanel);
        HidePanel(completedPanel);

        if (pickupPanel.closeButton != null) pickupPanel.closeButton.onClick.AddListener(RequestClose);
        if (completedPanel.closeButton != null) completedPanel.closeButton.onClick.AddListener(RequestClose);
    }

    void OnEnable()
    {
        PlayerInventory.PlantAcquired += OnPlantAcquired;
        PlayerAbilityInventory.ItemAdded += OnAbilityItemAdded;
        PlantEvents.Completed += OnPlantCompleted;
    }

    void OnDisable()
    {
        PlayerInventory.PlantAcquired -= OnPlantAcquired;
        PlayerAbilityInventory.ItemAdded -= OnAbilityItemAdded;
        PlantEvents.Completed -= OnPlantCompleted;
    }

    void Update()
    {
        UpdateUnderwater();
    }

    // ---------------------------------------------------------------
    // Underwater
    // ---------------------------------------------------------------
    private void UpdateUnderwater()
    {
        if (underwaterPanel == null) return;

        underwaterTimer -= Time.unscaledDeltaTime;
        if (underwaterTimer <= 0f)
        {
            underwaterTimer = 0.1f;
            underwaterNow = IsCameraUnderwater();
        }

        float target = underwaterNow ? 1f : 0f;
        float step = underwaterFadeSeconds <= 0.001f ? 1f : Time.unscaledDeltaTime / underwaterFadeSeconds;
        underwaterGroup.alpha = Mathf.MoveTowards(underwaterGroup.alpha, target, step);

        bool visible = underwaterGroup.alpha > 0.001f || underwaterNow;
        if (underwaterPanel.activeSelf != visible) underwaterPanel.SetActive(visible);
    }

    /// <summary>True while the view camera is inside a water volume (or below the water surface).</summary>
    public bool IsCameraUnderwater()
    {
        Camera cam = viewCamera != null ? viewCamera : Camera.main;
        if (cam == null) return false;
        Vector3 p = cam.transform.position;

        if (waterVolumes != null && waterVolumes.Length > 0)
        {
            foreach (Collider v in waterVolumes)
            {
                if (v == null || !v.enabled || !v.gameObject.activeInHierarchy) continue;

                bool needsBoundsOnly = v is MeshCollider mc && !mc.convex;
                if (needsBoundsOnly)
                {
                    if (v.bounds.Contains(p)) return true;
                }
                else if ((v.ClosestPoint(p) - p).sqrMagnitude < 0.0001f) return true;
            }
            return false;
        }

        return waterSurface != null && p.y < waterSurface.position.y;
    }

    // ---------------------------------------------------------------
    // Triggers
    // ---------------------------------------------------------------
    private void OnPlantAcquired(PlantSpeciesData species)
    {
        if (species == null || pickupPanel.root == null) return;
        if (!seenPickups.Add("plant:" + species.ResolvedId)) return;
        SaveSeen(PickupSeenKey, seenPickups);

        Enqueue(new Request
        {
            kind = Kind.Pickup,
            name = species.displayName,
            description = species.description,
            icon = species.InventoryImage != null ? species.InventoryImage : species.journalIcon
        });
    }

    private void OnAbilityItemAdded(AbilityItemData data, int amount)
    {
        if (data == null || pickupPanel.root == null) return;
        if (!seenPickups.Add("item:" + data.name)) return;
        SaveSeen(PickupSeenKey, seenPickups);

        Enqueue(new Request
        {
            kind = Kind.Pickup,
            name = data.displayName,
            description = data.description,
            icon = data.icon
        });
    }

    private void OnPlantCompleted(PlantSpeciesData species)
    {
        if (completedPanel.root == null) return;

        string key = oncePerSpecies && species != null ? "done:" + species.ResolvedId : "done:first";
        if (!seenCompleted.Add(key)) return;
        SaveSeen(CompletedSeenKey, seenCompleted);

        Enqueue(new Request
        {
            kind = Kind.Completed,
            name = species != null ? species.displayName : "Plant",
            description = species != null ? species.description : string.Empty,
            icon = species == null ? null
                 : species.goldIcon != null ? species.goldIcon
                 : species.InventoryImage != null ? species.InventoryImage : species.journalIcon
        });
    }

    // ---------------------------------------------------------------
    // Queue + display
    // ---------------------------------------------------------------
    private void Enqueue(Request r)
    {
        queue.Enqueue(r);
        if (runner == null) runner = StartCoroutine(Run());
    }

    private void RequestClose() => closeRequested = true;

    private IEnumerator Run()
    {
        if (startDelay > 0f) yield return new WaitForSecondsRealtime(startDelay);

        while (queue.Count > 0)
        {
            Request r = queue.Dequeue();
            yield return Show(r);
        }
        runner = null;
    }

    private IEnumerator Show(Request r)
    {
        PopupPanel p = r.kind == Kind.Completed ? completedPanel : pickupPanel;
        float duration = Mathf.Max(0.5f, r.kind == Kind.Completed ? completedSeconds : popupSeconds);
        AudioClip clip = r.kind == Kind.Completed ? completedSound : pickupSound;
        string title = r.kind == Kind.Completed ? completedTitle : pickupTitle;
        if (p.root == null) yield break;

        // Fill in the content
        if (p.titleText != null && !string.IsNullOrEmpty(title)) p.titleText.text = title;
        if (p.nameText != null) p.nameText.text = r.name;
        if (p.descriptionText != null) p.descriptionText.text = r.description;
        if (p.icon != null)
        {
            p.icon.sprite = r.icon;
            p.icon.enabled = r.icon != null;
        }
        if (p.countdownFill != null) p.countdownFill.fillAmount = 1f;

        closeRequested = false;
        CanvasGroup group = GetOrAddGroup(p.root);
        RectTransform rt = p.root.transform as RectTransform;
        Vector3 baseScale = rt != null ? rt.localScale : Vector3.one;
        float startScale = r.kind == Kind.Completed ? 0.5f : 0.8f;

        group.alpha = 0f;
        p.root.SetActive(true);
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, soundVolume);

        // Pop in
        for (float t = 0f; t < popInSeconds; t += Time.unscaledDeltaTime)
        {
            float k = popInSeconds <= 0.001f ? 1f : t / popInSeconds;
            group.alpha = Mathf.Clamp01(k * 1.5f);
            if (rt != null) rt.localScale = baseScale * Mathf.LerpUnclamped(startScale, 1f, EaseOutBack(k));
            yield return null;
        }
        group.alpha = 1f;
        if (rt != null) rt.localScale = baseScale;

        // Countdown
        float left = duration;
        while (left > 0f && !closeRequested)
        {
            left -= Time.unscaledDeltaTime;
            if (p.countdownText != null)
                p.countdownText.text = string.Format(countdownFormat, Mathf.CeilToInt(Mathf.Max(0f, left)));
            if (p.countdownFill != null)
                p.countdownFill.fillAmount = Mathf.Clamp01(left / duration);
            yield return null;
        }

        // Fade out
        for (float t = 0f; t < fadeOutSeconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - (fadeOutSeconds <= 0.001f ? 1f : t / fadeOutSeconds);
            yield return null;
        }
        HidePanel(p);
        if (rt != null) rt.localScale = baseScale;
    }

    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    private static CanvasGroup GetOrAddGroup(GameObject go)
    {
        CanvasGroup g = go.GetComponent<CanvasGroup>();
        if (g == null) g = go.AddComponent<CanvasGroup>();
        return g;
    }

    private static void HidePanel(PopupPanel p)
    {
        if (p != null && p.root != null) p.root.SetActive(false);
    }

    // ---------------------------------------------------------------
    // Persistence (PlayerPrefs, included in the save file via SaveRegistry.BuiltInStringKeys)
    // ---------------------------------------------------------------
    private static void LoadSeen(string key, HashSet<string> set)
    {
        set.Clear();
        string raw = PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrEmpty(raw)) return;
        foreach (string s in raw.Split('|'))
            if (!string.IsNullOrEmpty(s)) set.Add(s);
    }

    private static void SaveSeen(string key, HashSet<string> set)
    {
        PlayerPrefs.SetString(key, string.Join("|", set));
        PlayerPrefs.Save();
    }

    // ---------------------------------------------------------------
    // Test helpers
    // ---------------------------------------------------------------
    [ContextMenu("Test: pickup popup (plant)")]
    private void TestPlant()
    {
        if (debugSpecies == null) { Debug.LogWarning("[MainUIPopups] Assign Debug Species first."); return; }
        seenPickups.Remove("plant:" + debugSpecies.ResolvedId);
        OnPlantAcquired(debugSpecies);
    }

    [ContextMenu("Test: pickup popup (item)")]
    private void TestItem()
    {
        if (debugItem == null) { Debug.LogWarning("[MainUIPopups] Assign Debug Item first."); return; }
        seenPickups.Remove("item:" + debugItem.name);
        OnAbilityItemAdded(debugItem, 1);
    }

    [ContextMenu("Test: plant completed popup")]
    private void TestCompleted()
    {
        seenCompleted.Clear();
        OnPlantCompleted(debugSpecies);
    }
}
