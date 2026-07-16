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

    public void Initialize(InventoryItemInstance item, bool fits, System.Action onClick)
    {
        Button button = GetComponent<Button>();

        if (icon != null)
        {
            Sprite sprite = item.icon != null ? item.icon : item.displayImage;
            icon.sprite = sprite;
            icon.enabled = sprite != null;

            Color c = icon.color;
            c.a = fits ? 1f : dimmedAlpha;
            icon.color = c;
        }

        if (background != null)
        {
            Color c = background.color;
            c.a = fits ? 1f : dimmedAlpha;
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