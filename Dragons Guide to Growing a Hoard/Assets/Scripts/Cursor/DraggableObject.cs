using UnityEngine;

/// <summary>
/// Attach to a world-space object (with a Collider2D or Collider) that the
/// player can drag around. Shows "Hand" on hover, "HandClose" while dragging.
/// Requires an OnMouseX-compatible setup (Collider present, Camera has physics raycaster for 3D
/// or the object has a Collider2D for 2D).
/// </summary>
public class DraggableObject : MonoBehaviour
{
    private bool isDragging = false;
    private Vector3 dragOffset;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void OnMouseEnter()
    {
        if (!isDragging)
            CursorManager.Instance.SetCursor(CursorManager.CursorState.Hand);
    }

    void OnMouseExit()
    {
        if (!isDragging)
            CursorManager.Instance.SetCursor(CursorManager.CursorState.Normal);
    }

    void OnMouseDown()
    {
        isDragging = true;
        CursorManager.Instance.SetCursor(CursorManager.CursorState.HandClose);
        dragOffset = transform.position - GetMouseWorldPosition();
    }

    void OnMouseDrag()
    {
        transform.position = GetMouseWorldPosition() + dragOffset;
    }

    void OnMouseUp()
    {
        isDragging = false;
        CursorManager.Instance.SetCursor(CursorManager.CursorState.Hand);
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = mainCamera.WorldToScreenPoint(transform.position).z;
        return mainCamera.ScreenToWorldPoint(mouseScreenPos);
    }
}
