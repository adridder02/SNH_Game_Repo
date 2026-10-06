using UnityEngine;

// =============================================================
// HarvestNodeTypeGroup.cs
// -------------------------------------------------------------
// Put this on an empty GameObject nested one level inside a
// HarvestNodeContainer, with all of ONE plant type's harvest node meshes
// as its direct children:
//
//   HarvestNodeContainer
//    ├─ SunbloomGroup (this component)         <- set plantPrefab etc. ONCE here
//    │   ├─ SunbloomNode_01
//    │   ├─ SunbloomNode_02
//    │   └─ SunbloomNode_03
//    └─ MoonpetalGroup (another HarvestNodeTypeGroup)
//        ├─ MoonpetalNode_01
//        └─ MoonpetalNode_02
//
// HarvestNodeContainer.CacheChildren() recognises this component on a
// direct child and, instead of treating the group itself as a harvestable
// node, calls ConfigureChildren() below — which auto-adds and configures a
// CollectablePlant on every one of ITS children from the settings here —
// then flattens those children into its own node list. From the player's
// side nothing changes (each mesh is still individually harvestable,
// outlined, disintegrated, etc. exactly as before); you just set the plant
// prefab/icon/mission/condition ONCE per group instead of once per node.
//
// A child that already has its OWN CollectablePlant (e.g. one node in the
// group needs a different starting condition than the rest) is left
// completely alone — same "don't overwrite what's already there" rule
// HarvestNodeContainer's DisintegrateEffect auto-add already follows.
// =============================================================
public class HarvestNodeTypeGroup : MonoBehaviour
{
    [Header("Shared Plant Settings (applied to every node in this group)")]
    [Tooltip("REQUIRED: the plant prefab asset every node in this group adds to inventory when harvested.")]
    [SerializeField] private GameObject plantPrefab;

    [Header("Fallback Display (used only if the prefab has no PlantState.journalSpecies)")]
    [Tooltip("Fallback name — ignored once the prefab's PlantState.journalSpecies is assigned.")]
    [SerializeField] private string plantName = "Plant";
    [Tooltip("Fallback icon — ignored once the prefab's PlantState.journalSpecies is assigned.")]
    [SerializeField] private Sprite plantIcon;
    [Tooltip("Fallback detail image — ignored once the prefab's PlantState.journalSpecies is assigned.")]
    [SerializeField] private Sprite plantImage;

    [Header("Mission")]
    [Tooltip("Same purpose as CollectablePlant's own field — see its tooltip. Shared across every node " +
             "in this group.")]
    [SerializeField] private MissionData tutorialMission;

    [Header("Starting Condition")]
    [Tooltip("Same purpose as CollectablePlant's own field — see its tooltip. Shared across every node " +
             "in this group; add a CollectablePlant by hand on one specific node first if it needs a " +
             "different starting condition than the rest.")]
    [SerializeField] private PlantCondition startingCondition = new PlantCondition { isPermanentlyDead = false, startingHealth01 = 1f };

    /// <summary>Called by HarvestNodeContainer.CacheChildren() — adds and configures a CollectablePlant
    /// on every direct child of this group that doesn't already have one. Safe to call repeatedly (e.g.
    /// CacheChildren re-scanning after nodes are added/removed at runtime) — anything already configured,
    /// by hand or by a previous pass, is left untouched.</summary>
    public void ConfigureChildren()
    {
        if (plantPrefab == null)
        {
            Debug.LogWarning($"[HarvestNodeTypeGroup] '{gameObject.name}' has no Plant Prefab assigned — " +
                              "its child nodes won't be configured.", this);
            return;
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null) continue;

            if (child.GetComponent<CollectablePlant>() != null)
                continue; // already set up (by hand, or an earlier pass) - leave it exactly as it is

            CollectablePlant plant = child.gameObject.AddComponent<CollectablePlant>();
            plant.ConfigureFromGroup(plantPrefab, plantName, plantIcon, plantImage, tutorialMission, startingCondition);
        }
    }
}
