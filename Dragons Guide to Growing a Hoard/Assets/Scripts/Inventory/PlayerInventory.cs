using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// =============================================================
// PlayerInventory.cs
// -------------------------------------------------------------
// Owns the player's plants: a fixed-size InventoryGrid plus an
// unlimited "Available" overflow list for anything that doesn't
// currently fit in the grid.
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

    /// <summary>Fired whenever the grid or available contents change, so the UI can redraw.</summary>
    public event Action OnInventoryChanged;

    public InventoryGrid Grid => grid;
    public int GridWidth => grid.Width;
    public int GridHeight => grid.Height;

    void Awake()
    {
        grid = new InventoryGrid(gridWidth, gridHeight);
        waterPool = maxWaterRefill;
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
                    AddPlantToInventory(plantPrefab, plant.GetPlantIcon());
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

        int waterLayer = LayerMask.NameToLayer("WaterRefill");
        if (collision.gameObject.layer == waterLayer)
        {
            refillWaterPool();
            Debug.Log($"Water is refilled to: {waterPool}");
        }
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
    /// </summary>
    public bool AddPlantToInventory(GameObject plantPrefab, Sprite icon = null)
    {
        if (plantPrefab == null)
        {
            Debug.LogWarning("Tried to add a null plant prefab to inventory!");
            return false;
        }

        var instance = new InventoryItemInstance(plantPrefab, icon);
        bool placedInGrid = grid.TryAutoPlace(instance);
        items.Add(instance);

        Debug.Log(placedInGrid
            ? $"Added {plantPrefab.name} to grid at ({instance.gridX},{instance.gridY})"
            : $"Grid full — {plantPrefab.name} sent to Available");

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

    /// <summary>Attempts to place/move an existing instance to a specific grid cell.</summary>
    public bool TryPlaceInGrid(InventoryItemInstance instance, int x, int y)
    {
        if (instance == null) return false;
        bool ok = grid.PlaceAt(instance, x, y);
        if (ok) OnInventoryChanged?.Invoke();
        return ok;
    }

    /// <summary>Pulls an item out of the grid into the Available panel (does not remove it from the inventory).</summary>
    public void MoveToAvailable(InventoryItemInstance instance)
    {
        if (instance == null || !instance.IsInGrid) return;
        grid.RemoveItem(instance);
        OnInventoryChanged?.Invoke();
    }

    /// <summary>Grows the grid — call this from your expansion-consumable logic.</summary>
    public void ExpandGrid(int newWidth, int newHeight)
    {
        grid.Resize(newWidth, newHeight);
        gridWidth = newWidth;
        gridHeight = newHeight;
        OnInventoryChanged?.Invoke();
    }

    public List<InventoryItemInstance> GetGridItems() => items.Where(i => i.IsInGrid).ToList();
    public List<InventoryItemInstance> GetAvailableItems() => items.Where(i => !i.IsInGrid).ToList();
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
    public void reduceWaterPool(float decreaseW) => waterPool = Mathf.Max(0f, waterPool - decreaseW);
    public void refillWaterPool() => waterPool = maxWaterRefill;
}