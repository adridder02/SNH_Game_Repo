using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// =============================================================
// PlayerInventory.cs
// -------------------------------------------------------------
// Owns the player's plants AND the shared main-inventory grid: a
// fixed-size InventoryGrid plus an unlimited "Available" overflow
// list for anything that doesn't currently fit in the grid.
//
// FLOW (matches the design):
//   • Harvesting a node, or removing a plant from a pot, both
//     call AddPlantToInventory(). It tries to auto-place the
//     plant in the grid; if there's no room it lands in Available.
//   • The player can drag items around the grid (TryPlaceInGrid)
//     or drag an item out of the grid into Available (MoveToAvailable).
//   • Planting (PotInteraction.cs) reads GetInventory() / calls
//     RemoveFirstPlant() exactly as before — those signatures are
//     unchanged so PotInteraction.cs and PotContents.cs need NO edits.
//
// CONSUMABLES/PLACEABLES SHARE THIS SAME GRID:
//   PlayerAbilityInventory (its stacks are Consumable/Placeable
//   AbilityItemInstance objects — see AbilityItemInstance.cs) still
//   owns the actual item COUNTS. This script just also places each
//   stack into the SAME grid plants use, the first time it's picked
//   up — see ReconcileAbilityGridPlacement() below, which listens to
//   PlayerAbilityInventory.OnChanged. GetGridItems()/GetAvailableItems()
//   below return a mix of plants and ability stacks for that reason;
//   GetAllItems() (used by the Choose Plant panel) is deliberately
//   left plant-only, since you can't plant a consumable into a pot.
//
// NOTE: "IsInventoryFull" is now always false and kept only for
// backward compatibility — since Available has no cap, the player
// is never blocked from harvesting. If you want Available to also
// have a hard cap, add a maxAvailableSlots check in AddPlantToInventory.
// =============================================================
public class PlayerInventory : MonoBehaviour
{
    [Header("Grid Settings")]
    [Tooltip("Width of the inventory grid in cells. Pass a larger value into ExpandGrid() when the expansion consumable is used.")]
    [SerializeField] private int gridWidth = 4;
    [Tooltip("Height of the inventory grid in cells.")]
    [SerializeField] private int gridHeight = 3;

    [Header("Water Inventory")]
    [SerializeField] private float maxWaterRefill = 20f;
    private float waterPool = 20f;

    private InventoryGrid _grid;

    /// <summary>
    /// Backing access for the grid. Normally set in Awake(), but if this
    /// GameObject started inactive (e.g. accidentally nested under a UI
    /// panel that's hidden by default, or a player rig not yet active on
    /// spawn), Awake() won't have run yet the first time something tries
    /// to use the inventory — Unity only calls Awake() once a GameObject
    /// first becomes active. Rather than NullReferenceException on that
    /// first access, lazily create the grid here so things keep working,
    /// but log a warning since the real fix is making sure PlayerInventory
    /// lives on an always-active object.
    /// </summary>
    private InventoryGrid grid
    {
        get
        {
            if (_grid == null)
            {
                Debug.LogWarning("[PlayerInventory] Grid was accessed before Awake() ran — creating it now " +
                                 "as a fallback. This usually means this GameObject started inactive (e.g. " +
                                 "nested under a UI panel that's hidden by default), since Awake() only runs " +
                                 "once a GameObject first becomes active. Move PlayerInventory onto an " +
                                 "always-active object to avoid relying on this fallback.", this);
                _grid = new InventoryGrid(gridWidth, gridHeight);
            }
            return _grid;
        }
        set => _grid = value;
    }
    private readonly List<InventoryItemInstance> items = new List<InventoryItemInstance>();

    [Tooltip("Auto-found on this GameObject if left empty. Owns consumable/placeable stack COUNTS — " +
             "this script just also gives each stack a spot in the same grid plants use.")]
    [SerializeField] private PlayerAbilityInventory abilityInventory;

    // Which ability stacks (by instanceId) have already had their one-time auto-place attempt —
    // see ReconcileAbilityGridPlacement(). Prevents re-placing a stack the player has since
    // dragged to Available themselves.
    private readonly HashSet<string> trackedAbilityIds = new HashSet<string>();

    /// <summary>Fired whenever the grid or available contents change, so the UI can redraw.</summary>
    public event Action OnInventoryChanged;

    /// <summary>Fires exactly once — the first time the player gets a PLACEABLE Clovenwick (the Shelf):
    /// either picked up fully grown, or harvested from a pot. See UnlockWallPlacement, called by
    /// PlayerAbilityInventory.Add when the wall-placement item arrives. Lets onboarding-only UI that's specifically
    /// about wall placement (e.g. MainUIController's Tab-to-switch hint icon during floor Placing
    /// mode) stay hidden until wall placement is actually something the player can do. See
    /// HasUnlockedWallPlacement for the already-happened case.</summary>
    public event Action OnWallPlacementUnlocked;

    /// <summary>True once OnWallPlacementUnlocked has fired. Check this on Start()/OnEnable() for UI
    /// that initializes after the first Clovenwick pickup already happened this session.</summary>
    public bool HasUnlockedWallPlacement { get; private set; }

    /// <summary>Fires exactly once — the first time the player ever receives a plant into their
    /// inventory (harvest node, physical pickup, or pulling one back out of a pot). Lets onboarding-
    /// only UI (hotbar, inventory icon, the pot menu's ability button) stay hidden until there's
    /// actually something in the player's hands to use them with. See HasHarvestedFirstPlant for the
    /// already-happened case (UI that initializes after this already fired once this session).</summary>
    public event Action OnFirstPlantHarvested;

    /// <summary>True once OnFirstPlantHarvested has fired. Check this on Start()/OnEnable() for UI
    /// that initializes after the first harvest already happened this session — the event alone only
    /// reaches listeners that were already subscribed at the moment it fired.</summary>
    public bool HasHarvestedFirstPlant { get; private set; }

    public InventoryGrid Grid => grid;
    public int GridWidth => grid.Width;
    public int GridHeight => grid.Height;

    void Awake()
    {
        grid = new InventoryGrid(gridWidth, gridHeight);
        waterPool = maxWaterRefill;

        if (abilityInventory == null)
            abilityInventory = GetComponent<PlayerAbilityInventory>();

        if (abilityInventory != null)
            abilityInventory.OnChanged += ReconcileAbilityGridPlacement;
        else
            Debug.LogWarning("[PlayerInventory] No PlayerAbilityInventory on this GameObject — " +
                              "consumables/placeables won't appear in the main inventory grid. Add " +
                              "one alongside PlayerInventory.", this);
    }

    void OnDestroy()
    {
        if (abilityInventory != null)
            abilityInventory.OnChanged -= ReconcileAbilityGridPlacement;
    }

    // ------------------------------------------------------------
    // ABILITY STACK <-> GRID SYNC
    // ------------------------------------------------------------
    /// <summary>
    /// Keeps the shared grid in sync with PlayerAbilityInventory's stacks: auto-places any stack
    /// the FIRST time it's ever seen (grid first, Available overflow if the grid's full — the
    /// same one-time treatment AddPlantToInventory gives a freshly harvested plant), and forgets/
    /// clears the grid cells of any stack PlayerAbilityInventory has fully removed (its count hit
    /// 0). A stack the player has since dragged to Available manually is left alone from then on —
    /// this only ever auto-places a given stack once, exactly like a plant.
    /// </summary>
    private void ReconcileAbilityGridPlacement()
    {
        if (abilityInventory == null) return;
        bool changed = false;

        var currentIds = new HashSet<string>();
        foreach (AbilityItemInstance stack in abilityInventory.Stacks)
            currentIds.Add(stack.instanceId);

        // Forget anything that's been fully consumed and dropped from PlayerAbilityInventory.
        foreach (string id in new List<string>(trackedAbilityIds))
        {
            if (!currentIds.Contains(id))
            {
                grid.RemoveById(id);
                trackedAbilityIds.Remove(id);
                changed = true;
            }
        }

        // Auto-place any stack we haven't handled yet (a brand new pickup).
        foreach (AbilityItemInstance stack in abilityInventory.Stacks)
        {
            if (trackedAbilityIds.Contains(stack.instanceId)) continue;
            grid.TryAutoPlace(stack); // ok if this fails — falls to Available, same as a full plant grid
            trackedAbilityIds.Add(stack.instanceId);
            changed = true;
        }

        if (changed) OnInventoryChanged?.Invoke();
    }

    void OnCollisionEnter(Collision collision)
    {
        int plantLayer = LayerMask.NameToLayer("Plant_Seeds");
        if (collision.gameObject.layer == plantLayer)
        {
            CollectablePlant plant = collision.gameObject.GetComponent<CollectablePlant>();
            if (plant != null)
            {
                GameObject plantPrefab = plant.GetPlantPrefab();
                if (plantPrefab != null)
                {
                    CollectPlant(plant, plantPrefab);
                    Destroy(collision.gameObject);
                    Debug.Log($"Collected: {plantPrefab.name}. Inventory: {GetInventorySize()} items ({GetGridItems().Count} in grid, {GetAvailableItems().Count} in Available)");
                }
                else
                {
                    Debug.LogWarning("Plant prefab is null!");
                }
            }
            else
            {
                Debug.LogWarning("No CollectablePlant component found on the object!");
            }
        }

        // NOTE: water refill used to be handled here via a "WaterRefill" layer check, but
        // OnCollisionEnter never fires against a trigger collider — and water volumes are almost
        // always triggers (so the player can pass through instead of colliding solidly with the
        // water's surface). That's why refill wasn't working. Water is now handled by
        // PlayerWaterSource.cs (attach it to the Player alongside this component), which uses
        // OnTriggerEnter/Stay and calls AddWater() below.
    }

    /// <summary>
    /// Single entry point for picking up a CollectablePlant (harvest node or physical collision). A normal
    /// plant goes into the plant inventory. A node marked Collected Fully Grown gives the plant's harvest
    /// item (e.g. the placeable Clovenwick Shelf) straight into the ability inventory instead - the species
    /// is still marked discovered in the journal either way.
    /// </summary>
    public void CollectPlant(CollectablePlant plant, GameObject plantPrefab)
    {
        if (plant != null && plant.TryGetGrownYield(out AbilityItemData item, out int amount) && abilityInventory != null)
        {
            PlantState state = plantPrefab != null ? plantPrefab.GetComponentInChildren<PlantState>() : null;
            if (state != null && state.journalSpecies != null && PlantJournalManager.Instance != null)
            {
                PlantJournalManager.Instance.MarkDiscovered(state.journalSpecies);
                NewItemTracker.Instance?.MarkAcquiredJournal(state.journalSpecies.ResolvedId);
            }

            abilityInventory.Add(item, amount);
            return;
        }

        AddPlantToInventory(plantPrefab, plant.GetPlantIcon(), plant.GetPlantImage(), plant.GetPlantName(), plant.StartingCondition);
    }

    /// <summary>Unlocks wall placement (once per session) and fires the wall-placement tutorial trigger.
    /// Called by PlayerAbilityInventory.Add the first time the player receives the wall-placement item.</summary>
    public void UnlockWallPlacement()
    {
        if (HasUnlockedWallPlacement) return;
        HasUnlockedWallPlacement = true;
        TutorialSequenceController.Instance?.NotifyExternalTrigger("picked_up_clovenwick");
        OnWallPlacementUnlocked?.Invoke();
    }

    // ------------------------------------------------------------
    // ADD / REMOVE — used by harvest nodes and by pots returning plants
    // ------------------------------------------------------------

    /// <summary>
    /// Adds a new plant instance to the inventory: tries the grid first,
    /// falls back to Available. This is the single entry point used by
    /// both node-harvesting and "remove plant from pot".
    /// Pass the plant's UI icon (CollectablePlant.GetPlantIcon()) when you have
    /// one — plant prefabs are 3D and have no SpriteRenderer, so this is the
    /// only way the inventory slot gets an icon to display. Callers that don't
    /// have an icon (e.g. returning a plant from a pot) can omit it.
    /// Also pass the larger detail image (CollectablePlant.GetPlantImage()) and
    /// display name (CollectablePlant.GetPlantName()) when available — these
    /// feed the Plant detail panel and are deliberately separate from the icon.
    /// Also marks the species discovered in PlantJournalManager if the prefab's
    /// PlantState.journalSpecies is set — this is why a harvested-but-undiscovered
    /// species shows up correctly positioned in the journal grid but with no icon
    /// and no click response otherwise: IsDiscovered would be false.
    /// Pass 'condition' to carry a plant's saved state through — PotContents.RemovePlant()
    /// captures the LIVE condition of a plant being pulled from a pot; HarvestNodeContainer
    /// passes a harvest node's designer-set starting condition. Defaults to fully healthy
    /// (PlantCondition.Healthy) if omitted.
    /// </summary>
    /// <summary>Fired every time a plant lands in the inventory (harvest or returned from a pot). Used by
    /// MainUIPopups for the "first time you pick this up" card; it decides itself whether it's the first time.</summary>
    public static event Action<PlantSpeciesData> PlantAcquired;

    public bool AddPlantToInventory(GameObject plantPrefab, Sprite icon = null, Sprite displayImage = null, string displayName = null, PlantCondition condition = null, bool countsAsFirstPickup = true)
    {
        if (plantPrefab == null)
        {
            Debug.LogWarning("Tried to add a null plant prefab to inventory!");
            return false;
        }

        var instance = new InventoryItemInstance(plantPrefab, icon, displayImage, displayName, condition);
        bool placedInGrid = grid.TryAutoPlace(instance);
        items.Add(instance);
        // NOTE: this used to complete an "AddedPotToInventory" checklist task here —
        // that task was dropped from the mission, so there's nothing to call anymore.

        // Inventory "new item" badge (NewItemTracker) — see that file's header for why this is a
        // separate, independently-clearing domain from the journal discovery block below even
        // though it's often the exact same plant.
        NewItemTracker.Instance?.MarkAcquiredInventory(instance.newItemTypeId);

        // Unlock the journal entry for this species, if it has one — this is the
        // single entry point for both harvesting a node and returning a plant from
        // a pot, so this is the one place that needs to know about journal discovery.
        // PlantState.journalSpecies lives on the prefab itself (no instance needed).
        PlantState state = plantPrefab.GetComponent<PlantState>();
        if (state != null && state.journalSpecies != null)
        {
            if (PlantJournalManager.Instance != null)
            {
                // NOTE: journal discovery (IsDiscovered/MarkDiscovered) is PlayerPrefs-backed and
                // persists forever, across scenes AND across separate play sessions. The wall-
                // placement unlock below used to be gated on "not already journal-discovered" too,
                // but that meant it could only ever fire once in the ENTIRE lifetime of that save —
                // every later session (including repeated dev testing) would find the species already
                // discovered and silently skip the whole block, never re-firing the tutorial trigger
                // or unlocking HasUnlockedWallPlacement even though THIS session's state is fresh.
                // HasUnlockedWallPlacement is intentionally gated on its own check instead (see below),
                // since that's the actual per-session flag that needs to go true exactly once per run.
                PlantJournalManager.Instance.MarkDiscovered(state.journalSpecies);
                NewItemTracker.Instance?.MarkAcquiredJournal(state.journalSpecies.ResolvedId);

            }
            else
                Debug.LogWarning("[PlayerInventory] No PlantJournalManager in scene — journal discovery was skipped.");
        }

        if (state != null && state.journalSpecies != null)
            PlantAcquired?.Invoke(state.journalSpecies);

        Debug.Log(placedInGrid
            ? $"Added {plantPrefab.name} to grid at ({instance.gridX},{instance.gridY})"
            : $"Grid full — {plantPrefab.name} sent to Available");

        // countsAsFirstPickup = false is for starter items handed to the player (StartingInventoryGrant): they
        // must NOT unlock the onboarding UI (inventory icon...) - that waits for the real first pickup.
        if (countsAsFirstPickup && !HasHarvestedFirstPlant)
        {
            HasHarvestedFirstPlant = true;
            OnFirstPlantHarvested?.Invoke();
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Removes the first instance matching this prefab, wherever it currently
    /// lives (grid or Available). Called by PotInteraction after AddPlant()
    /// succeeds on the pot side.
    /// </summary>
    public void RemoveFirstPlant(GameObject plantPrefab)
    {
        var instance = items.FirstOrDefault(i => i.plantPrefab == plantPrefab);
        if (instance == null)
        {
            Debug.LogWarning($"Plant {plantPrefab?.name} not found in inventory!");
            return;
        }
        RemoveInstance(instance);
    }

    /// <summary>Fully removes a specific instance from the inventory (grid + item list).</summary>
    public void RemoveInstance(InventoryItemInstance instance)
    {
        if (instance == null) return;
        if (instance.IsInGrid) grid.RemoveItem(instance);
        items.Remove(instance);
        OnInventoryChanged?.Invoke();
    }

    // ------------------------------------------------------------
    // GRID ORGANISATION — used by the drag-and-drop UI
    // ------------------------------------------------------------

    /// <summary>Attempts to place/move an existing item (a plant OR a consumable/placeable stack)
    /// to a specific grid cell.</summary>
    public bool TryPlaceInGrid(IGridPlaceable item, int x, int y)
    {
        if (item == null) return false;
        bool ok = grid.PlaceAt(item, x, y);
        if (ok) OnInventoryChanged?.Invoke();
        return ok;
    }

    /// <summary>Pulls an item (plant or consumable/placeable stack) out of the grid into the
    /// Available panel (does not remove it from the inventory / consume its count).</summary>
    public void MoveToAvailable(IGridPlaceable item)
    {
        if (item == null || !item.IsInGrid) return;
        grid.RemoveItem(item);
        OnInventoryChanged?.Invoke();
    }

    /// <summary>Grows the grid — call this from your expansion-consumable logic (e.g. a Bubble Blossom
    /// bubble). After resizing, automatically tries to pull items sitting in Available into the newly
    /// opened grid space — a bubble that unlocks a slot should actually put a waiting plant into it
    /// rather than leaving it stranded in Available until the player manually drags it in.</summary>
    public void ExpandGrid(int newWidth, int newHeight)
    {
        grid.Resize(newWidth, newHeight);
        gridWidth = newWidth;
        gridHeight = newHeight;

        PromoteAvailableToGrid();

        OnInventoryChanged?.Invoke();
    }

    /// <summary>Tries to auto-place every Available item into the grid, in the order they were added
    /// (oldest first). Stops placing a given item as soon as the grid reports full — cheap to call
    /// after any operation that might have freed up grid space. Fires OnInventoryChanged itself only
    /// if something actually moved, so callers that already fire it afterward (like ExpandGrid above)
    /// don't double-notify.</summary>
    public void PromoteAvailableToGrid()
    {
        bool anyMoved = false;

        foreach (InventoryItemInstance instance in items.Where(i => !i.IsInGrid).ToList())
        {
            if (grid.TryAutoPlace(instance))
                anyMoved = true;
        }

        if (abilityInventory != null)
        {
            foreach (AbilityItemInstance stack in abilityInventory.Stacks.Where(s => !s.IsInGrid).ToList())
            {
                if (grid.TryAutoPlace(stack))
                    anyMoved = true;
            }
        }

        if (anyMoved) OnInventoryChanged?.Invoke();
    }

    /// <summary>Everything currently occupying a grid cell — plants AND consumable/placeable stacks
    /// mixed together, since they now share one grid. Used by InventoryUIController to draw the
    /// main grid panel.</summary>
    public List<IGridPlaceable> GetGridItems()
    {
        var result = new List<IGridPlaceable>(items.Where(i => i.IsInGrid));
        if (abilityInventory != null)
            result.AddRange(abilityInventory.Stacks.Where(s => s.IsInGrid));
        return result;
    }

    /// <summary>Everything currently overflowed into Available — plants AND consumable/placeable
    /// stacks mixed together. Used by InventoryUIController to draw the Available panel.</summary>
    public List<IGridPlaceable> GetAvailableItems()
    {
        var result = new List<IGridPlaceable>(items.Where(i => !i.IsInGrid));
        if (abilityInventory != null)
            result.AddRange(abilityInventory.Stacks.Where(s => !s.IsInGrid));
        return result;
    }

    /// <summary>Every owned PLANT (not consumables/placeables) regardless of grid/Available —
    /// deliberately plant-only, since this feeds the Choose Plant panel and you can't plant a
    /// consumable into a pot.</summary>
    public List<InventoryItemInstance> GetAllItems() => new List<InventoryItemInstance>(items);

    // ------------------------------------------------------------
    // BACKWARDS-COMPATIBLE API
    // (PotInteraction.cs / PotContents.cs use these exactly as before)
    // ------------------------------------------------------------

    public List<GameObject> GetInventory()
    {
        return items.Select(i => i.plantPrefab).ToList();
    }

    public bool IsInventoryFull()
    {
        // Available has no cap, so the player is never hard-blocked from
        // collecting. Kept for compatibility with any existing callers.
        return false;
    }

    public int GetInventorySize() => items.Count;
    public int GetMaxInventorySize() => grid.Width * grid.Height;

    public void RemovePlantAtIndex(int index)
    {
        if (index < 0 || index >= items.Count)
        {
            Debug.LogWarning($"Index {index} is out of range!");
            return;
        }
        RemoveInstance(items[index]);
    }

    public void ClearInventory()
    {
        foreach (var i in items.ToList()) RemoveInstance(i);
        Debug.Log("Inventory cleared!");
    }

    public bool ContainsPlant(GameObject plantPrefab) => items.Any(i => i.plantPrefab == plantPrefab);

    public GameObject GetFirstPlant() => items.Count > 0 ? items[0].plantPrefab : null;

    public float getWaterPool() => waterPool;
    public float getMaxWaterPool() => maxWaterRefill;
    public void reduceWaterPool(float decreaseW) => waterPool = Mathf.Max(0f, waterPool - decreaseW);
    public void refillWaterPool() => waterPool = maxWaterRefill;

    [Tooltip("How many units of water refill per second while standing in a water source (see " +
             "PlayerWaterSource) — replaces the old instant-fill behavior.")]
    public float waterRefillRate = 10f;

    /// <summary>Adds water gradually rather than instantly maxing the pool — call every frame the
    /// player is standing in a water source (PlayerWaterSource.OnTriggerStay), passing Time.deltaTime
    /// (safe to use directly here even though this fires from a trigger callback — Unity's
    /// Time.deltaTime already reflects the physics step duration in that context). Overload of
    /// refillWaterPool() above, which is left in place as an instant full-refill for anything else
    /// that might still want that (a debug/cheat action, a full-refill consumable, ...).</summary>
    public void refillWaterPool(float deltaTime)
    {
        waterPool = Mathf.Min(waterPool + waterRefillRate * deltaTime, maxWaterRefill);
    }

    // ------------------------------------------------------------
    // SAVE / LOAD (SaveWorld) — plants, ability stacks, grid layout, water, flags
    // ------------------------------------------------------------
    public void CaptureSave(PlayerSave s)
    {
        s.water = waterPool;
        s.gridW = grid.Width;
        s.gridH = grid.Height;
        s.hasUnlockedWallPlacement = HasUnlockedWallPlacement;
        s.hasHarvestedFirstPlant = HasHarvestedFirstPlant;
        s.plants.Clear();
        s.abilities.Clear();

        foreach (InventoryItemInstance i in items)
        {
            if (i.plantPrefab == null) continue;
            s.plants.Add(new PlantItemSave
            {
                prefab = i.plantPrefab.name,
                dead = i.condition.isPermanentlyDead,
                health01 = i.condition.startingHealth01,
                setGrowth = i.condition.setStartingGrowth,
                growth01 = i.condition.startingGrowth01,
                gx = i.gridX,
                gy = i.gridY,
            });
        }

        if (abilityInventory != null)
        {
            s.hasHarvestedFirstAbilityItem = abilityInventory.HasHarvestedFirstAbilityItem;
            s.hasHarvestedFirstHotbarItem = abilityInventory.HasHarvestedFirstHotbarItem;
            foreach (AbilityItemInstance a in abilityInventory.Stacks)
            {
                if (a.data == null) continue;
                s.abilities.Add(new AbilityStackSave { item = a.data.name, count = a.count, gx = a.gridX, gy = a.gridY });
            }
        }
    }

    /// <summary>Replaces the whole inventory with the saved one WITHOUT the pickup side effects
    /// (no journal discovery, new-item badges, tutorial triggers or first-pickup events).</summary>
    public void RestoreSave(PlayerSave s, SaveRegistry registry)
    {
        // 1. wipe what's here (grid cells + lists)
        foreach (InventoryItemInstance i in items.ToList())
        {
            if (i.IsInGrid) grid.RemoveItem(i);
            items.Remove(i);
        }
        foreach (string id in new List<string>(trackedAbilityIds)) grid.RemoveById(id);
        trackedAbilityIds.Clear();

        // 2. grid size, water, flags
        if (s.gridW > 0 && s.gridH > 0)
        {
            grid.Resize(s.gridW, s.gridH);
            gridWidth = s.gridW;
            gridHeight = s.gridH;
        }
        waterPool = Mathf.Clamp(s.water, 0f, maxWaterRefill);
        HasUnlockedWallPlacement = s.hasUnlockedWallPlacement;
        HasHarvestedFirstPlant = s.hasHarvestedFirstPlant;

        var placeList = new List<KeyValuePair<IGridPlaceable, Vector2Int>>();

        // 3. ability stacks
        if (abilityInventory != null)
        {
            var stacks = new List<AbilityItemInstance>();
            foreach (AbilityStackSave a in s.abilities)
            {
                AbilityItemData d = registry != null ? registry.FindAbility(a.item) : null;
                if (d == null || a.count <= 0) continue;
                var inst = new AbilityItemInstance(d, a.count);
                stacks.Add(inst);
                trackedAbilityIds.Add(inst.instanceId); // so ReconcileAbilityGridPlacement doesn't re-place it
                if (a.gx >= 0 && a.gy >= 0) placeList.Add(new KeyValuePair<IGridPlaceable, Vector2Int>(inst, new Vector2Int(a.gx, a.gy)));
            }
            abilityInventory.ReplaceStacksSilently(stacks, s.hasHarvestedFirstAbilityItem, s.hasHarvestedFirstHotbarItem);
        }

        // 4. plants
        foreach (PlantItemSave p in s.plants)
        {
            GameObject prefab = registry != null ? registry.FindPlant(p.prefab) : null;
            if (prefab == null) continue;

            PlantSpeciesData species = prefab.GetComponentInChildren<PlantState>() != null
                ? prefab.GetComponentInChildren<PlantState>().journalSpecies : null;
            var cond = new PlantCondition
            {
                isPermanentlyDead = p.dead,
                startingHealth01 = p.health01,
                setStartingGrowth = p.setGrowth,
                startingGrowth01 = p.growth01,
            };
            var inst = new InventoryItemInstance(prefab,
                species != null ? species.journalIcon : null,
                species != null ? species.InventoryImage : null,
                species != null ? species.displayName : null,
                cond);
            items.Add(inst);
            if (p.gx >= 0 && p.gy >= 0) placeList.Add(new KeyValuePair<IGridPlaceable, Vector2Int>(inst, new Vector2Int(p.gx, p.gy)));
        }

        // 5. back into their saved grid cells (anything that no longer fits just stays in Available)
        foreach (var kv in placeList) grid.PlaceAt(kv.Key, kv.Value.x, kv.Value.y);

        abilityInventory?.NotifyChanged();
        OnInventoryChanged?.Invoke();
    }
}