using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =============================================================
// ConfirmationDialog.cs
// -------------------------------------------------------------
// Canvas-based Yes/No popup. Build the UI in your scene and assign it here:
//
//   Panel (root)            -> "Panel Root"   (start it INACTIVE, or leave it; Awake hides it)
//     ├─ Message (TMP)      -> "Message Text"
//     ├─ Yes Button         -> "Yes Button"   (optional: its TMP label -> "Yes Label")
//     └─ No Button          -> "No Button"    (optional: its TMP label -> "No Label")
//
// Tip: put the Panel under a full-screen semi-transparent Image so it dims the game behind it
// and blocks clicks. The Panel Root is the object shown/hidden, so include that dimmer in it.
//
// Show()/Hide() are unchanged from the old OnGUI placeholder, so callers
// (e.g. TutorialAdvanceTrigger) need no changes. If Panel Root isn't assigned yet, the old
// OnGUI box is used as a fallback so nothing breaks while you build the UI.
// =============================================================
public class ConfirmationDialog : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    [Tooltip("Optional. Set per Show() call if you pass custom labels; otherwise left as authored.")]
    [SerializeField] private TMP_Text yesLabel;
    [SerializeField] private TMP_Text noLabel;

    [Header("Accident protection")]
    [Tooltip("Seconds the Yes button stays disabled (with a countdown on its label) after the dialog opens. 0 = off.")]
    [SerializeField] private float yesCountdownSeconds = 5f;

    [Header("Behaviour")]
    [Tooltip("Locks the cursor/camera the same way Journal/Inventory/etc. do while this dialog is showing.")]
    [SerializeField] private bool lockGameplayWhileOpen = true;

    private bool isShowing = false;
    private string message = "";
    private Action onConfirm;
    private Action onCancel;
    private string yesBaseText;
    private Coroutine countdown;

    private bool HasUI => panelRoot != null;

    public bool IsShowing => isShowing;

    private void Awake()
    {
        if (yesLabel != null) yesBaseText = yesLabel.text;
        yesButton?.onClick.AddListener(HandleConfirm);
        noButton?.onClick.AddListener(HandleCancel);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>Shows the dialog with `message`. onCancelCallback is optional — pass null if
    /// dismissing shouldn't do anything beyond closing the popup. yes/no labels are optional overrides.</summary>
    public void Show(string message, Action onConfirmCallback, Action onCancelCallback = null,
                     string yesText = null, string noText = null)
    {
        this.message = message;
        onConfirm = onConfirmCallback;
        onCancel = onCancelCallback;
        isShowing = true;

        if (HasUI)
        {
            if (messageText != null) messageText.text = message;
            if (yesText != null) yesBaseText = yesText;
            if (noText != null && noLabel != null) noLabel.text = noText;
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling(); // draw above the rest of its canvas

            if (countdown != null) StopCoroutine(countdown);
            countdown = yesCountdownSeconds > 0f ? StartCoroutine(YesCountdown()) : null;
            if (countdown == null) SetYesReady();
        }

        if (lockGameplayWhileOpen)
        {
            GameInputModeManager.Instance?.SetMenuUIMode();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private System.Collections.IEnumerator YesCountdown()
    {
        if (yesButton != null) yesButton.interactable = false;
        float end = Time.unscaledTime + yesCountdownSeconds;
        int shown = -1;
        while (Time.unscaledTime < end)
        {
            int secs = Mathf.CeilToInt(end - Time.unscaledTime);
            if (secs != shown && yesLabel != null)
            {
                shown = secs;
                yesLabel.text = $"{yesBaseText} ({secs})";
            }
            yield return null;
        }
        countdown = null;
        SetYesReady();
    }

    private void SetYesReady()
    {
        if (yesLabel != null && yesBaseText != null) yesLabel.text = yesBaseText;
        if (yesButton != null) yesButton.interactable = true;
    }

    public void Hide()
    {
        if (countdown != null) { StopCoroutine(countdown); countdown = null; }
        if (HasUI) SetYesReady();
        isShowing = false;
        if (panelRoot != null) panelRoot.SetActive(false);
        if (lockGameplayWhileOpen)
            GameInputModeManager.Instance?.SetGameplayMode();
    }

    private void HandleConfirm()
    {
        Action cb = onConfirm;
        Hide();
        cb?.Invoke();
    }

    private void HandleCancel()
    {
        Action cb = onCancel;
        Hide();
        cb?.Invoke();
    }

    // Fallback only: used until a Panel Root is assigned.
    private void OnGUI()
    {
        if (!isShowing || HasUI) return;

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
        GUI.Label(new Rect(boxRect.x + 20f, boxRect.y + 15f, boxRect.width - 40f, 70f), message, messageStyle);

        const float bw = 120f, bh = 36f, sp = 20f;
        float y = boxRect.y + boxRect.height - bh - 15f;
        if (GUI.Button(new Rect(boxRect.center.x - bw - sp / 2f, y, bw, bh), "Yes")) HandleConfirm();
        if (GUI.Button(new Rect(boxRect.center.x + sp / 2f, y, bw, bh), "No")) HandleCancel();
    }
}