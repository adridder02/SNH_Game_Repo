using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================
// InteractPromptView.cs
// -------------------------------------------------------------
// Attach this to the ROOT of a prefab, not to the Player.
//
// Build the three-part prompt by hand in the Editor, once:
//   InteractPrompt (this component + a World Space Canvas)
//    ├─ Background   Image — the panel behind everything
//    ├─ KeybindIcon  Image — the key art (e.g. an "[E]" keycap sprite)
//    └─ Label        TextMeshProUGUI — e.g. "Interact"
//   Position/size/style all three however you like.
//
// Then drag the four objects below onto this component's fields.
//
// PotInteraction.cs never builds or aligns any of this — at runtime it
// Instantiate()s this prefab once (see BuildInteractPrompt) and only
// ever swaps the keybind sprite / label text via
// interactKeybindSprite / interactPromptLabel. Change the look by
// editing THIS prefab.
// =============================================================
public class InteractPromptView : MonoBehaviour
{
    [Tooltip("The World Space Canvas at the root of this prefab.")]
    public Canvas canvas;

    [Tooltip("Background panel Image behind the icon/label.")]
    public Image background;

    [Tooltip("Icon Image showing the current keybind (e.g. an '[E]' keycap sprite).")]
    public Image keybindIcon;

    [Tooltip("Label text, e.g. 'Interact'.")]
    public TextMeshProUGUI label;
}
