using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =============================================================
// SaveSlotButtonsUI.cs
// -------------------------------------------------------------
// Drop this on any panel (the pause/exit menu, the settings page's "Game" section...) and assign
// whichever buttons that panel has. All fields are optional.
//
//   saveButton         Save now (manual save).
//   loadButton         Load the last save (asks first — you lose progress since then).
//   quitToTitleButton  Autosaves, then goes to the title screen (no confirmation).
//   deleteSaveButton   Deletes the save (asks first).
//   statusLabel        "Saved 25 Oct 2026, 14:03" / "Game saved." / "No saved game".
//
// Confirmations use the scene's ConfirmationDialog (auto-found); without one the action just happens.
// Buttons grey out when they can't do anything (e.g. Save in the title scene, Load with no save).
// =============================================================
public class SaveSlotButtonsUI : MonoBehaviour
{
    [SerializeField] private Button saveButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button quitToTitleButton;
    [SerializeField] private Button deleteSaveButton;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private ConfirmationDialog confirmDialog;

    [SerializeField] private float flashSeconds = 2.5f;

    private Coroutine flash;

    private void Awake()
    {
        saveButton?.onClick.AddListener(SaveNow);
        loadButton?.onClick.AddListener(AskLoad);
        quitToTitleButton?.onClick.AddListener(AskQuitToTitle);
        deleteSaveButton?.onClick.AddListener(AskDelete);
        if (confirmDialog == null) confirmDialog = FindAnyObjectByType<ConfirmationDialog>();
    }

    private void OnEnable()
    {
        SaveManager.SaveStateChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        SaveManager.SaveStateChanged -= Refresh;
        if (flash != null) { StopCoroutine(flash); flash = null; }
    }

    private void Refresh()
    {
        bool canSave = SaveManager.InMainScene;
        bool has = SaveManager.HasSave;
        if (saveButton != null) saveButton.interactable = canSave;
        if (loadButton != null) loadButton.interactable = has;
        if (deleteSaveButton != null) deleteSaveButton.interactable = has;
        if (quitToTitleButton != null) quitToTitleButton.interactable = true;
        if (flash == null) SetStatus(has ? SaveManager.DescribeSave() : "No saved game");
    }

    private void SetStatus(string t) { if (statusLabel != null) statusLabel.text = t; }

    // ---- actions ----
    public void SaveNow()
    {
        bool ok = SaveManager.Save("manual");
        Flash(ok ? "Game saved." : "Can't save here.");
    }

    public void AskLoad()
    {
        Confirm("Load your last save? Anything since then is lost.", () =>
        {
            if (!SaveManager.Continue(this)) Flash("Couldn't load the save.");
        }, "Load");
    }

    public void AskQuitToTitle()
    {
        SaveManager.QuitToTitle(this); // autosaves, then goes straight to the title (no confirmation)
    }

    public void AskDelete()
    {
        Confirm("Delete your saved game? This can't be undone.", () =>
        {
            SaveManager.DeleteSave();
            Flash("Save deleted.");
        }, "Delete");
    }

    private void Confirm(string message, System.Action yes, string yesLabel)
    {
        if (confirmDialog != null) confirmDialog.Show(message, yes, null, yesLabel, "Cancel");
        else yes();
    }

    private void Flash(string message)
    {
        if (!isActiveAndEnabled) return;
        if (flash != null) StopCoroutine(flash);
        flash = StartCoroutine(FlashRoutine(message));
    }

    private IEnumerator FlashRoutine(string message)
    {
        SetStatus(message);
        yield return new WaitForSecondsRealtime(flashSeconds);
        flash = null;
        Refresh();
    }
}