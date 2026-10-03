using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// =============================================================
// RaycastDebugger.cs  — TEMPORARY diagnostic, delete once done
// -------------------------------------------------------------
// Attach to any always-active GameObject. Left-click anywhere and
// it logs every UI element under the cursor, front-to-back. The
// TOP entry in the list is whatever's actually eating the click —
// if it's not your Back button, that's your blocker.
// =============================================================
public class RaycastDebugger : MonoBehaviour
{
    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current == null)
            {
                Debug.LogError("[RaycastDebugger] No EventSystem in the scene at all — that alone would explain zero UI interaction anywhere.");
                return;
            }

            var pointerData = new PointerEventData(EventSystem.current)
            {
                position = Mouse.current.position.ReadValue()
            };

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            if (results.Count == 0)
            {
                Debug.Log("[RaycastDebugger] Nothing hit at cursor position.");
                return;
            }

            Debug.Log($"[RaycastDebugger] {results.Count} hit(s), front-to-back (TOP one wins the click):");
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                Debug.Log($"  {i}: {GetPath(r.gameObject)}  (sortingLayer={r.sortingLayer}, sortingOrder={r.sortingOrder}, depth={r.depth})");
            }
        }
    }

    private static string GetPath(GameObject go)
    {
        string path = go.name;
        Transform t = go.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return path;
    }
}
