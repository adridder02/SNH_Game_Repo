using System;

// =============================================================
// MenuLayerManager.cs
// -------------------------------------------------------------
// Ensures only one menu (Journal, Inventory, Exit Menu, Pot Menu) is ever
// open at a time. Doesn't touch HUD visibility (see MainUIController.
// SetHudHidden for that) or input mode (GameInputModeManager) — this is
// purely "if a second menu opens while one is already open, close the
// first one first."
//
// HOW IT WORKS:
//   Whichever menu opens calls NotifyOpened(this, CloseSelf) with itself
//   as the owner and a delegate that closes IT. If a different menu was
//   already open, its own registered close-delegate gets invoked first —
//   so each menu is responsible for closing itself via whatever it
//   already uses (CloseJournal/CloseInventory/CloseExitMenu/CloseMenu),
//   this class just decides WHEN to call that.
//
// A plain static class rather than a MonoBehaviour singleton — no scene
// object to wire up, and there's nothing here that needs Update() or a
// lifecycle. State is intentionally simple: just "who currently owns the
// menu layer, and how do I close them."
// =============================================================
public static class MenuLayerManager
{
    private static object _currentOwner;
    private static Action _currentCloseAction;

    /// <summary>Call this right as a menu opens. If a different menu is currently open, this closes
    /// it first (via the close delegate IT registered when it opened) before taking over as the
    /// current owner. Safe to call even if nothing else is open.</summary>
    public static void NotifyOpened(object owner, Action closeAction)
    {
        if (owner == null) return;

        if (_currentOwner != null && !ReferenceEquals(_currentOwner, owner))
            _currentCloseAction?.Invoke();

        _currentOwner = owner;
        _currentCloseAction = closeAction;
    }

    /// <summary>Call this right as a menu closes (however it closed — its own back button, Escape,
    /// or because another menu just took over). Only actually clears state if this owner is still
    /// the current one, so it can't accidentally clear a different menu that opened in between.</summary>
    public static void NotifyClosed(object owner)
    {
        if (owner == null) return;
        if (!ReferenceEquals(_currentOwner, owner)) return;

        _currentOwner = null;
        _currentCloseAction = null;
    }
}
