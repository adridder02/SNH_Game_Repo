using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================
// PotPlantOptionUI.cs
// -------------------------------------------------------------
// One entry in the Choose Plant panel's grid — same slot look as
// InventorySlotUI / JournalSlotUI (a background frame + icon), not
// a plain text button. Laid out by PotMenuUIController using the
// same template-derived grid math as the inventory/journal grids
// (see ManualSlotLayout) — this component only cares about its own
// visuals and click, not its position.
//
// Plants whose size doesn't fit the pot stay visible (so the player
// can see everything they own) but aren't clickable and are dimmed —
// same treatment the old text menu gave them with a "✗" prefix, just
// visual instead of a symbol.
// =============================================================
[RequireComponent(typeof(Button))]
public class PotPlantOptionUI : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI label;

    [Tooltip("Alpha applied to background/icon when this plant doesn't fit the current pot — still " +
             "visible, just dimmed, matching the greyed-out treatment locked journal slots use.")]
    [SerializeField] private float dimmedAlpha = 0.35f;

    public void Initialize(InventoryItemInstance item, bool fits, bool locked, System.Action onClick)
    {
        Button button = GetComponent<Button>();

        // A size mismatch (fits=false) and a progression lock (locked=true) both dim the same way
        // visually, but behave differently on click — see button.interactable below. Sized-wrong
        // items have nothing useful to explain, so they stay unclickable same as before. Locked
        // items are still the right size, so they stay clickable specifically so onClick can tell
        // the player WHY it's locked instead of just doing nothing.
        bool dimmed = !fits || locked;

        if (icon != null)
        {
            Sprite sprite = item.icon != null ? item.icon : item.displayImage;
            icon.sprite = sprite;
            icon.enabled = sprite != null;

            Color c = icon.color;
            c.a = dimmed ? dimmedAlpha : 1f;
            icon.color = c;
        }

        if (background != null)
        {
            Color c = background.color;
            c.a = dimmed ? dimmedAlpha : 1f;
            background.color = c;
        }

        if (label != null)
            label.text = !string.IsNullOrEmpty(item.displayName)
                ? item.displayName
                : (item.plantPrefab != null ? item.plantPrefab.name.Replace("(Clone)", "").Trim() : "Unknown");

        button.interactable = fits;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke());
    }
}