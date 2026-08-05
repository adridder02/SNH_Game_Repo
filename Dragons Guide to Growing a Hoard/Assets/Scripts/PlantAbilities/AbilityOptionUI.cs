using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================
// AbilityOptionUI.cs
// -------------------------------------------------------------
// One entry in the pot menu's Abilities panel — mirrors
// PotPlantOptionUI's shape (background + icon + label + Button)
// exactly, just for AbilityItemInstance stacks instead of plants,
// plus a stack-count label since these are stackable.
// =============================================================
[RequireComponent(typeof(Button))]
public class AbilityOptionUI : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private TextMeshProUGUI countLabel;

    [Tooltip("Alpha applied when this item can't be used right now (e.g. a pot-targeted consumable with no plant in the current pot).")]
    [SerializeField] private float dimmedAlpha = 0.35f;

    public void Initialize(AbilityItemInstance stack, bool usable, System.Action onClick)
    {
        Button button = GetComponent<Button>();

        if (icon != null)
        {
            icon.sprite = stack.data.icon;
            icon.enabled = stack.data.icon != null;
            Color c = icon.color;
            c.a = usable ? 1f : dimmedAlpha;
            icon.color = c;
        }

        if (background != null)
        {
            Color c = background.color;
            c.a = usable ? 1f : dimmedAlpha;
            background.color = c;
        }

        if (label != null)
            label.text = stack.data.displayName;

        if (countLabel != null)
            countLabel.text = $"x{stack.count}";

        button.interactable = usable;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke());
    }
}
