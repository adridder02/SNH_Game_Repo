using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// =============================================================
// HingeInteractable.cs
// -------------------------------------------------------------
// One interactable that swings one or more "leaves" (doors, a bookshelf...) between CLOSED and OPEN.
//
// SETUP
//   1. Put this on an empty object near the thing (e.g. "DoorSet_West"). Players press E within
//      Interact Range of it (or of "Interact Point" if you assign one).
//   2. Add each door/shelf to "Leaves". For each leaf set:
//        Transform   the object that rotates
//        Open Angle  degrees to swing to (negative = other direction) — two doors usually get +90 / -90
//      "Axis" below picks which local axis they rotate around (yours are Z).
//   3. IMPORTANT: a leaf rotates around ITS OWN pivot. If the door swings around its middle instead
//      of its hinge edge, parent it to an empty object placed at the hinge, and put that empty object
//      in Leaves instead.
//
// On Awake every leaf is snapped to Closed Angle (default 0) on the chosen axis, so wherever you left
// them in the editor, they start closed.
// =============================================================
public class HingeInteractable : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [System.Serializable]
    public class Leaf
    {
        public Transform transform;
        [Tooltip("Local angle (degrees) when open. Negative swings the other way.")]
        public float openAngle = 90f;
        [System.NonSerialized] public Vector3 baseEuler;
    }

    [Header("Leaves")]
    [SerializeField] private Leaf[] leaves = new Leaf[1];
    [SerializeField] private Axis axis = Axis.Z;
    [Tooltip("Local angle every leaf starts (and returns) at.")]
    [SerializeField] private float closedAngle = 0f;
    [SerializeField] private float openSeconds = 1.2f;

    [Header("Behaviour")]
    [Tooltip("ON: pressing E again closes it. OFF: it opens once and stays open.")]
    [SerializeField] private bool canClose = true;
    [SerializeField] private bool startLocked = false;
    [Tooltip("Shown nowhere by default — set it if you want a log message when someone tries a locked one.")]
    [SerializeField] private string lockedMessage = "";

    [Header("Interaction")]
    [SerializeField] private Transform interactPoint;
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private bool showInteractPrompt = true;
    [Tooltip("Auto-found if empty.")]
    [SerializeField] private MainUIController mainUI;
    [Tooltip("Auto-found via PlayerInventory if empty.")]
    [SerializeField] private Transform player;

    [Header("Sound (optional)")]
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;

    [Header("Events")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    public bool IsOpen { get; private set; }
    public bool IsLocked { get; private set; }

    private float t;          // 0 = closed, 1 = open
    private bool inRange;
    private AudioSource audioSource;

    private void Awake()
    {
        IsLocked = startLocked;
        foreach (Leaf l in leaves)
        {
            if (l == null || l.transform == null) continue;
            l.baseEuler = l.transform.localEulerAngles;
        }
        Apply();   // snaps everything to the closed angle
    }

    private void Start()
    {
        if (player == null)
        {
            PlayerInventory inv = FindAnyObjectByType<PlayerInventory>();
            if (inv != null) player = inv.transform;
        }
        if (mainUI == null && showInteractPrompt) mainUI = FindAnyObjectByType<MainUIController>();
    }

    private void Update()
    {
        Vector3 origin = (interactPoint != null ? interactPoint : transform).position;
        bool near = player != null && Vector3.Distance(origin, player.position) <= interactRange
                    && (canClose || !IsOpen);

        if (near != inRange)
        {
            inRange = near;
            mainUI?.SetInteractPromptVisible(showInteractPrompt && inRange, this);
        }

        if (inRange && !ThirdPersonCameraController.CameraLocked &&
            Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            Interact();

        float target = IsOpen ? 1f : 0f;
        if (!Mathf.Approximately(t, target))
        {
            t = Mathf.MoveTowards(t, target, Time.deltaTime / Mathf.Max(0.01f, openSeconds));
            Apply();
        }
    }

    private void OnDisable() => mainUI?.SetInteractPromptVisible(false, this);

    // ------------------------------------------------------------
    public void Interact()
    {
        if (IsLocked)
        {
            if (!string.IsNullOrEmpty(lockedMessage)) Debug.Log("[HingeInteractable] " + lockedMessage, this);
            return;
        }
        if (IsOpen) { if (canClose) SetOpen(false); }
        else SetOpen(true);
    }

    public void Unlock() => IsLocked = false;
    public void Lock() => IsLocked = true;

    /// <summary>Open/close from code or a UnityEvent. instant = skip the swing (used by load).</summary>
    public void SetOpen(bool open, bool instant = false)
    {
        if (IsOpen == open && !instant) return;
        IsOpen = open;
        if (instant) { t = open ? 1f : 0f; Apply(); }
        else PlaySound(open ? openClip : closeClip);
        (open ? onOpened : onClosed)?.Invoke();
    }

    private void Apply()
    {
        float eased = Mathf.SmoothStep(0f, 1f, t);
        foreach (Leaf l in leaves)
        {
            if (l == null || l.transform == null) continue;
            float angle = Mathf.Lerp(closedAngle, l.openAngle, eased);
            Vector3 e = l.baseEuler;
            if (axis == Axis.X) e.x = angle; else if (axis == Axis.Y) e.y = angle; else e.z = angle;
            l.transform.localRotation = Quaternion.Euler(e);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.playOnAwake = false;
        }
        audioSource.PlayOneShot(clip);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.5f);
        Gizmos.DrawWireSphere((interactPoint != null ? interactPoint : transform).position, interactRange);
    }
}
