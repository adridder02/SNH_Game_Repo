using System;
using UnityEngine;

// =============================================================
// ConfirmationDialog.cs
// -------------------------------------------------------------
// PLACEHOLDER: draws a plain Yes/No box with Unity's built-in OnGUI system
// instead of a themed Canvas UI — zero scene setup needed (no Canvas, no
// TextMeshPro, no Buttons to build/wire). Swap this out for a proper
// Canvas-based dialog whenever you're ready; Show()/Hide() are the same
// public API either way, so nothing calling into this (e.g.
// TutorialAdvanceTrigger) will need to change when you do.
// =============================================================
public class ConfirmationDialog : MonoBehaviour
{
    [Tooltip("Locks the cursor/camera the same way Journal/Inventory/etc. do while this dialog is showing.")]
    [SerializeField] private bool lockGameplayWhileOpen = true;

    private bool isShowing = false;
    private string message = "";
    private Action onConfirm;
    private Action onCancel;

    /// <summary>Shows the dialog with `message`. onCancelCallback is optional — pass null if
    /// dismissing shouldn't do anything beyond closing the popup.</summary>
    public void Show(string message, Action onConfirmCallback, Action onCancelCallback = null)
    {
        this.message = message;
        onConfirm = onConfirmCallback;
        onCancel = onCancelCallback;
        isShowing = true;

        if (lockGameplayWhileOpen)
        {
            GameInputModeManager.Instance?.SetMenuUIMode();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void Hide()
    {
        isShowing = false;
        if (lockGameplayWhileOpen)
            GameInputModeManager.Instance?.SetGameplayMode();
    }

    private void HandleConfirm()
    {
        Hide();
        onConfirm?.Invoke();
    }

    private void HandleCancel()
    {
        Hide();
        onCancel?.Invoke();
    }

    private void OnGUI()
    {
        if (!isShowing) return;

        const float boxWidth = 360f;
        const float boxHeight = 140f;
        Rect boxRect = new Rect((Screen.width - boxWidth) / 2f, (Screen.height - boxHeight) / 2f, boxWidth, boxHeight);

        GUI.Box(boxRect, GUIContent.none);

        GUIStyle messageStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            wordWrap = true
        };
        Rect messageRect = new Rect(boxRect.x + 20f, boxRect.y + 15f, boxRect.width - 40f, 70f);
        GUI.Label(messageRect, message, messageStyle);

        const float buttonWidth = 120f;
        const float buttonHeight = 36f;
        const float spacing = 20f;
        float buttonsY = boxRect.y + boxRect.height - buttonHeight - 15f;

        Rect yesRect = new Rect(boxRect.center.x - buttonWidth - spacing / 2f, buttonsY, buttonWidth, buttonHeight);
        Rect noRect = new Rect(boxRect.center.x + spacing / 2f, buttonsY, buttonWidth, buttonHeight);

        if (GUI.Button(yesRect, "Yes")) HandleConfirm();
        if (GUI.Button(noRect, "No")) HandleCancel();
    }
}