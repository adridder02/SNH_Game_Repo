using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

// =============================================================
// MissionObjectiveLinks.cs
// -------------------------------------------------------------
// Completes a mission's tasks from real gameplay. Put ONE of these in the main scene per mission
// (or one for everything, with the mission field switched per component), assign the Mission, then add
// one Link per objective:
//
//   Task Id            - the task's id in the MissionData (e.g. "revive_sparkmint")
//   Type               - what has to happen:
//     PlantRevived        a plant of `species` reaches the Revived state (PlantEvents.Revived)
//     PlantHarvested      a plant of `species` is harvested from a pot
//     ItemObtained        `item` is added to the player's ability inventory (e.g. a Heart)
//     ItemReturnedToTree  `item` is handed to the World Tree (WorldTreeCrystals reveals its crystal)
//     ZoneHappiness       `zone`'s happiness reaches `happinessThreshold` (and it has at least one plant)
//     AreaReached         the player gets within `radius` of `area`        ("Find the Dorms")
//     AreaInspected       the player is within `radius` of `area` and presses E ("Inspect the bookshelves")
//
// Tasks complete in any order WITHIN their group, and only once they have been revealed (see the
// Group number on each task in the MissionData); an objective done before it is revealed does not count.
// Anything else can call CompleteLink("task_id") / CompleteTaskById from a UnityEvent or your own code.
// =============================================================
public class MissionObjectiveLinks : MonoBehaviour
{
    public enum LinkType { PlantRevived, PlantHarvested, ItemObtained, ItemReturnedToTree, ZoneHappiness, AreaReached, AreaInspected }

    [Serializable]
    public class Link
    {
        public string label;
        [Tooltip("Must match the task's Task Id in the MissionData.")]
        public string taskId;
        public LinkType type;

        [Tooltip("PlantRevived / PlantHarvested")] public PlantSpeciesData species;
        [Tooltip("ItemObtained / ItemReturnedToTree — e.g. the Heart item")] public AbilityItemData item;
        [Tooltip("ZoneHappiness — the room/wing's ZoneHealth")] public ZoneHealth zone;
        [Tooltip("ZoneHappiness — happiness (0-100) the zone must reach")] public float happinessThreshold = 60f;
        [Tooltip("AreaReached / AreaInspected — the spot (an empty object works)")] public Transform area;
        [Tooltip("AreaReached / AreaInspected — how close the player must be")] public float radius = 4f;
    }

    [SerializeField] private MissionData mission;
    [SerializeField] private Link[] links;
    [Tooltip("Auto-found if empty.")]
    [SerializeField] private Transform player;
    [SerializeField] private float pollInterval = 0.25f;

    private MissionProgressManager pm;
    private float pollTimer;

    private void Awake()
    {
        if (player == null)
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }
    }

    private void Start()
    {
        pm = MissionProgressManager.Instance != null ? MissionProgressManager.Instance : FindAnyObjectByType<MissionProgressManager>();

        if (mission != null && links != null)
            foreach (Link l in links)
                if (l != null && !string.IsNullOrEmpty(l.taskId) && !HasTask(l.taskId))
                    Debug.LogWarning($"[MissionObjectiveLinks] '{mission.missionTitle}' has no task with id '{l.taskId}' (link '{l.label}').", this);
    }

    private void OnEnable()
    {
        PlantEvents.Revived += OnRevived;
        PlantEvents.Harvested += OnHarvested;
        PlayerAbilityInventory.ItemAdded += OnItemAdded;
        WorldTreeCrystals.CrystalRevealed += OnCrystalRevealed;
    }

    private void OnDisable()
    {
        PlantEvents.Revived -= OnRevived;
        PlantEvents.Harvested -= OnHarvested;
        PlayerAbilityInventory.ItemAdded -= OnItemAdded;
        WorldTreeCrystals.CrystalRevealed -= OnCrystalRevealed;
    }

    // ---- event-driven links ----
    private void OnRevived(PlantSpeciesData s) => ForEach(LinkType.PlantRevived, l => l.species != null && l.species == s);
    private void OnHarvested(PlantSpeciesData s) => ForEach(LinkType.PlantHarvested, l => l.species != null && l.species == s);
    private void OnItemAdded(AbilityItemData d, int n) => ForEach(LinkType.ItemObtained, l => l.item != null && l.item == d);
    private void OnCrystalRevealed(AbilityItemData d) => ForEach(LinkType.ItemReturnedToTree, l => l.item != null && l.item == d);

    private void ForEach(LinkType type, Func<Link, bool> matches)
    {
        if (links == null) return;
        foreach (Link l in links)
            if (l != null && l.type == type && matches(l))
                CompleteLink(l.taskId);
    }

    // ---- polled links (areas + zone happiness) ----
    private void Update()
    {
        if (links == null || mission == null) return;

        pollTimer -= Time.unscaledDeltaTime;
        bool poll = pollTimer <= 0f;
        if (poll) pollTimer = pollInterval;

        bool ePressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;

        foreach (Link l in links)
        {
            if (l == null || IsDone(l.taskId)) continue;

            switch (l.type)
            {
                case LinkType.ZoneHappiness:
                    if (poll && l.zone != null && l.zone.PlantCount > 0 && l.zone.ZoneHappiness >= l.happinessThreshold)
                        CompleteLink(l.taskId);
                    break;

                case LinkType.AreaReached:
                    if (poll && InRange(l)) CompleteLink(l.taskId);
                    break;

                case LinkType.AreaInspected:
                    if (ePressed && InRange(l)) CompleteLink(l.taskId);
                    break;
            }
        }
    }

    private bool InRange(Link l)
        => player != null && l.area != null && Vector3.Distance(player.position, l.area.position) <= l.radius;

    // ---- completion ----
    /// <summary>Completes the task with this id (safe to call repeatedly). Wire to a UnityEvent for custom triggers.</summary>
    public void CompleteLink(string taskId)
    {
        if (mission == null || string.IsNullOrEmpty(taskId)) return;
        if (pm == null) pm = MissionProgressManager.Instance;
        if (pm == null || !pm.IsTaskUnlocked(mission, taskId)) return; // not revealed yet — doesn't count
        pm.CompleteTask(mission.ResolvedId, taskId);
    }

    private bool IsDone(string taskId)
    {
        if (pm == null) pm = MissionProgressManager.Instance;
        return pm != null && pm.IsTaskComplete(mission.ResolvedId, taskId);
    }

    private bool HasTask(string taskId)
    {
        foreach (MissionTaskEntry t in mission.tasks)
            if (t.ResolvedId == taskId) return true;
        return false;
    }

#if UNITY_EDITOR
    // ------------------------------------------------------------------
    // AUTO-POPULATE: right-click this component's header -> "Populate Links From Mission".
    // Adds one link per task in the mission (task id + label filled in, type guessed from the wording,
    // species / Heart item matched by name). Existing links are kept untouched, so it's safe to re-run
    // after adding tasks. You only need to fill in the scene references it can't find (zones, areas).
    // ------------------------------------------------------------------
    [ContextMenu("Populate Links From Mission")]
    private void PopulateFromMission()
    {
        if (mission == null) { Debug.LogWarning("[MissionObjectiveLinks] Assign a Mission first.", this); return; }

        Undo.RecordObject(this, "Populate mission links");

        List<Link> list = links != null ? new List<Link>(links) : new List<Link>();
        PlantSpeciesData[] allSpecies = LoadAll<PlantSpeciesData>();
        AbilityItemData[] allItems = LoadAll<AbilityItemData>();
        int added = 0;

        foreach (MissionTaskEntry task in mission.tasks)
        {
            string id = task.ResolvedId;
            if (list.Exists(l => l != null && l.taskId == id)) continue;

            string d = (task.description ?? "").ToLowerInvariant();
            Link l = new Link { label = task.description, taskId = id };

            if (d.StartsWith("revive")) l.type = LinkType.PlantRevived;
            else if (d.StartsWith("harvest")) l.type = LinkType.ItemObtained;
            else if (d.StartsWith("return")) l.type = LinkType.ItemReturnedToTree;
            else if (d.Contains("happiness")) l.type = LinkType.ZoneHappiness;
            else if (d.StartsWith("inspect")) l.type = LinkType.AreaInspected;
            else l.type = LinkType.AreaReached;

            if (l.type == LinkType.PlantRevived)
                foreach (PlantSpeciesData s in allSpecies)
                    if (s != null && !string.IsNullOrEmpty(s.displayName) && d.Contains(s.displayName.ToLowerInvariant())) { l.species = s; break; }

            if (l.type == LinkType.ItemObtained || l.type == LinkType.ItemReturnedToTree)
                foreach (AbilityItemData it in allItems)
                    if (it != null && !string.IsNullOrEmpty(it.displayName) && d.Contains(it.displayName.ToLowerInvariant())) { l.item = it; break; }

            list.Add(l);
            added++;
        }

        links = list.ToArray();
        EditorUtility.SetDirty(this);
        Debug.Log($"[MissionObjectiveLinks] Added {added} link(s) for '{mission.missionTitle}'. " +
                  "Now assign the scene references it couldn't find (zones, areas) and any species/items still empty.", this);
    }

    private static T[] LoadAll<T>() where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
        T[] result = new T[guids.Length];
        for (int i = 0; i < guids.Length; i++)
            result[i] = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
        return result;
    }

    private void OnDrawGizmosSelected()
    {
        if (links == null) return;
        Gizmos.color = new Color(0.4f, 0.9f, 0.6f, 0.25f);
        foreach (Link l in links)
            if (l != null && l.area != null && (l.type == LinkType.AreaReached || l.type == LinkType.AreaInspected))
                Gizmos.DrawSphere(l.area.position, l.radius);
    }
#endif
}
