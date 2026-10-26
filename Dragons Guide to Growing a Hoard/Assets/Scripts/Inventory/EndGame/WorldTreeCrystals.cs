using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

// =============================================================
// WorldTreeCrystals.cs
// -------------------------------------------------------------
// Attach to the tree. Press E in range: for each crystal entry below
// whose layer is still hidden and whose keyItem the player is carrying
// at least one of (in PlayerAbilityInventory), reveals that layer and
// consumes one of the item. A single press can reveal more than one
// crystal at once if the player happens to be carrying more than one
// Heart at the same time.
//
// SETUP:
//   1. Assign dragonAbilityInventory (auto-found on the player if left
//      empty — same PlayerAbilityInventory the hotbar/pot abilities use).
//   2. Fill in `crystals`: one entry per crystal layer, each with its
//      GameObject (should start INACTIVE in the scene — this script
//      doesn't hide it for you, just reveals it) and the AbilityItemData
//      that unlocks it (your three "Heart of the ..." Consumable assets —
//      mark requiresTreeTarget = true on those so they don't show up on
//      the hotbar).
//   3. Getting the Heart items in the first place: put a PlantHarvestYield
//      component on the Luminary/Deluge Catcher/Maw Jaw plant prefabs
//      with yieldItem set to the matching Heart asset — PotContents.
//      HarvestPlant() already grants whatever's on that component, no
//      extra code needed there.
// =============================================================
public class WorldTreeCrystals : MonoBehaviour
{
    [System.Serializable]
    public class CrystalEntry
    {
        [Tooltip("Display-only, for your own reference in the Inspector list.")]
        public string label;

        [Tooltip("The item that reveals this crystal (e.g. 'Heart of the Night'). Consumed on use.")]
        public AbilityItemData keyItem;

        [Tooltip("The crystal's GameObject — should start INACTIVE in the scene. Set active once, never hidden again by this script.")]
        public GameObject layer;

        [System.NonSerialized] public bool revealed;
    }

    /// <summary>Fired when a Heart item is handed to the tree and its crystal is revealed.</summary>
    public static event System.Action<AbilityItemData> CrystalRevealed;

    [Header("Crystals")]
    [SerializeField] private CrystalEntry[] crystals = new CrystalEntry[3];

    [Header("When enough crystals are in place...")]
    [Tooltip("The glass section. Its material is swapped to the transparent one below.")]
    [SerializeField] private Renderer[] glassRenderers;
    [Tooltip("Which material slot on those renderers is the glass (0 = the first).")]
    [SerializeField] private int glassMaterialIndex = 0;
    [Tooltip("Duplicate your glass material, set its Surface Type to Transparent (keep the colour/opacity), and drop " +
             "it here. Recommended - it always works in builds. If left empty the script converts the current " +
             "material at runtime, which works in the editor but can fail in a build.")]
    [SerializeField] private Material transparentGlassMaterial;
    [Tooltip("How many crystals must be revealed before the glass turns transparent.")]
    [SerializeField] private int crystalsNeededForGlass = 2;

    [Tooltip("The directional light. Once enough crystals are in, it starts affecting the Light Layer below.")]
    [SerializeField] private Light directionalLight;
    [Tooltip("Rendering/Light Layer number the light currently excludes (Light Layer 1 = 1).")]
    [SerializeField] private int lightLayerToInclude = 1;
    [SerializeField] private int crystalsNeededForLight = 2;

    private bool glassApplied;
    private bool lightApplied;

    [Header("Range")]
    [SerializeField] private float interactRange = 3f;

    [Header("References")]
    [Tooltip("Auto-found on the player if left empty.")]
    [SerializeField] private PlayerAbilityInventory dragonAbilityInventory;
    [Tooltip("The player's transform — used for range checking. Auto-found via PlayerInventory if left empty.")]
    [SerializeField] private Transform player;

    [Header("UI — Interaction Prompt")]
    [SerializeField] private bool showInteractPrompt = true;
    [Tooltip("Same fixed HUD prompt PotInteraction/HarvestNodeContainer can use — see MainUIController." +
             "SetInteractPromptVisible. Auto-found in the scene if left empty.")]
    [SerializeField] private MainUIController mainUI;

    private bool playerInRange;

    private void Start()
    {
        if (dragonAbilityInventory == null)
            dragonAbilityInventory = FindAnyObjectByType<PlayerAbilityInventory>();

        if (player == null)
        {
            PlayerInventory inv = FindAnyObjectByType<PlayerInventory>();
            player = inv != null ? inv.transform : null;
        }

        if (dragonAbilityInventory == null)
            Debug.LogWarning("[WorldTreeCrystals] No PlayerAbilityInventory found — crystals can never be revealed.", this);

        if (mainUI == null && showInteractPrompt)
            mainUI = FindAnyObjectByType<MainUIController>();

        // Crystals should already be hidden in the scene per-file, but this guarantees it —
        // same "never trust the scene's starting active-state alone" treatment used elsewhere
        // (e.g. PotMenuUIController hiding its Choose Plant template).
        foreach (CrystalEntry entry in crystals)
        {
            if (entry?.layer == null) continue;
            entry.revealed = entry.layer.activeSelf;
            if (!entry.revealed)
                entry.layer.SetActive(false);
        }

        ApplyCrystalThresholds();
    }

    private void Update()
    {
        bool inRange = player != null &&
            Vector3.Distance(transform.position, player.position) <= interactRange &&
            !AllRevealed();

        if (inRange != playerInRange)
        {
            playerInRange = inRange;
            mainUI?.SetInteractPromptVisible(showInteractPrompt && playerInRange, this);
        }

        if (playerInRange && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            TryRevealCrystals();
    }

    private void OnDisable()
    {
        mainUI?.SetInteractPromptVisible(false, this);
    }

    // Save/load (SaveWorld)
    public bool[] CaptureRevealed()
    {
        var r = new bool[crystals.Length];
        for (int i = 0; i < crystals.Length; i++) r[i] = crystals[i] != null && crystals[i].revealed;
        return r;
    }

    public void RestoreRevealed(System.Collections.Generic.List<bool> revealed)
    {
        for (int i = 0; i < crystals.Length && i < revealed.Count; i++)
        {
            CrystalEntry e = crystals[i];
            if (!revealed[i] || e == null || e.layer == null) continue;
            e.layer.SetActive(true);
            e.revealed = true;
        }
        ApplyCrystalThresholds();
    }

    private int RevealedCount()
    {
        int n = 0;
        foreach (CrystalEntry e in crystals) if (e != null && e.revealed) n++;
        return n;
    }

    /// <summary>Glass -> transparent and the directional light -> includes the layer, once enough crystals are in.</summary>
    private void ApplyCrystalThresholds()
    {
        int count = RevealedCount();

        if (!glassApplied && count >= crystalsNeededForGlass && glassRenderers != null && glassRenderers.Length > 0)
        {
            glassApplied = true;
            Material converted = transparentGlassMaterial;
            foreach (Renderer r in glassRenderers)
            {
                if (r == null) continue;
                Material[] mats = r.sharedMaterials;
                if (glassMaterialIndex < 0 || glassMaterialIndex >= mats.Length) continue;

                Material use = converted;
                if (use == null)
                {
                    use = new Material(mats[glassMaterialIndex]);
                    MakeTransparent(use);
                }
                mats[glassMaterialIndex] = use;
                r.sharedMaterials = mats;
            }
        }

        if (!lightApplied && count >= crystalsNeededForLight && directionalLight != null)
        {
            var data = directionalLight.GetComponent<UniversalAdditionalLightData>();
            if (data != null)
            {
                lightApplied = true;
#if UNITY_6000_0_OR_NEWER
                data.renderingLayers |= (1u << lightLayerToInclude);
#else
                data.lightLayerMask |= (LightLayerEnum)(1 << lightLayerToInclude);
#endif
            }
        }
    }

    /// <summary>Runtime URP/Lit opaque -> transparent (same switches the material Inspector flips).</summary>
    private static void MakeTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private bool AllRevealed()
    {
        foreach (CrystalEntry entry in crystals)
        {
            if (entry?.layer == null) continue;
            if (!entry.revealed) return false;
        }
        return true;
    }

    private void TryRevealCrystals()
    {
        if (dragonAbilityInventory == null) return;

        bool revealedAny = false;

        foreach (CrystalEntry entry in crystals)
        {
            if (entry == null || entry.revealed || entry.layer == null || entry.keyItem == null) continue;
            if (dragonAbilityInventory.GetCount(entry.keyItem) <= 0) continue;

            if (!dragonAbilityInventory.TryConsume(entry.keyItem, 1)) continue;

            entry.layer.SetActive(true);
            entry.revealed = true;
            revealedAny = true;
            CrystalRevealed?.Invoke(entry.keyItem);
            ApplyCrystalThresholds();

            Debug.Log($"[WorldTreeCrystals] Revealed '{entry.label}' using {entry.keyItem.displayName}.");
        }

        if (!revealedAny)
            Debug.Log("[WorldTreeCrystals] Nothing to reveal — carry one of the Heart items and try again.");
        else if (AllRevealed())
            mainUI?.SetInteractPromptVisible(false, this); // nothing left to interact for here
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.25f);
        Gizmos.DrawSphere(transform.position, interactRange);
    }
#endif
}