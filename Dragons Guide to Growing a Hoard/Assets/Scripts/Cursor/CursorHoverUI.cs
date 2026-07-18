using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to any UGUI element (Button, Image, etc.) to swap the cursor
/// to "Clickable" on hover and back to "Normal" on exit.
/// </summary>
public class CursorHoverUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        CursorManager.Instance.SetCursor(CursorManager.CursorState.Clickable);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        CursorManager.Instance.SetCursor(CursorManager.CursorState.Normal);
    }
}
