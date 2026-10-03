using UnityEngine;

// =============================================================
// TutorialAdvanceTrigger.cs
// -------------------------------------------------------------
// Put this on the cube you place in the Tutorial scene, with a Collider
// set to "Is Trigger". When the player walks into it, shows a Yes/No
// ConfirmationDialog asking if they're ready to head into the main game —
// Yes loads mainSceneName, No just dismisses (walking back into the cube
// asks again).
// =============================================================
public class TutorialAdvanceTrigger : MonoBehaviour
{
    [Header("Prompt")]
    [SerializeField] private ConfirmationDialog confirmationDialog;
    [TextArea]
    [SerializeField] private string promptMessage = "Ready to head into the main game?";

    [Header("Scene To Load")]
    [Tooltip("Exact scene name as it appears in File > Build Settings — NOT a build index, so this " +
             "doesn't break if scenes get reordered/added later.")]
    [SerializeField] private string mainSceneName = "Main";

    [Header("Transition (optional)")]
    [Tooltip("Same fade-out Animator LevelLoader uses (needs a 'Start' trigger parameter). Leave " +
             "empty to load instantly with no fade.")]
    [SerializeField] private Animator transitionAnimator;
    [SerializeField] private float transitionTime = 1f;

    [Tooltip("Tag checked on the colliding object, alongside a PlayerController fallback check.")]
    [SerializeField] private string playerTag = "Player";

    // Stops the prompt from re-firing every frame the player stands inside the trigger volume,
    // and from stacking a second prompt on top of itself if they walk in, out, and back in fast.
    private bool promptShown = false;

    private void OnTriggerEnter(Collider other)
    {
        if (promptShown) return;
        if (!other.CompareTag(playerTag) && other.GetComponentInParent<PlayerController>() == null) return;

        promptShown = true;

        if (confirmationDialog != null)
        {
            confirmationDialog.Show(promptMessage, OnConfirmed, OnCancelled);
        }
        else
        {
            Debug.LogWarning("[TutorialAdvanceTrigger] No ConfirmationDialog assigned — loading the " +
                              "main scene immediately instead of prompting.", this);
            OnConfirmed();
        }
    }

    private void OnConfirmed()
    {
        SceneTransitionUtility.LoadScene(this, mainSceneName, transitionAnimator, transitionTime);
    }

    private void OnCancelled()
    {
        // Said no — let them walk back into the cube later to ask again.
        promptShown = false;
    }

#if UNITY_EDITOR
    // Placeholder visual so the trigger volume is visible in the Scene view without needing an
    // actual cube mesh/renderer yet — swap this out (or just add a real model alongside this
    // GameObject) whenever you're ready for something the player actually sees. Draws whatever
    // Collider is on this GameObject: a wireframe box for a BoxCollider (most likely, given "cube"),
    // or a wireframe sphere for a SphereCollider as a fallback.
    private void OnDrawGizmos()
    {
        Gizmos.color = promptShown ? new Color(1f, 0.6f, 0.2f, 0.6f) : new Color(0.3f, 1f, 0.5f, 0.6f);

        Collider col = GetComponent<Collider>();
        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);

        if (col is BoxCollider box)
        {
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
        else
        {
            // No collider yet (or an unsupported shape) — draw a default 1x1x1 cube at the
            // GameObject's origin so there's still SOMETHING to see and click on before you've
            // added/sized the real trigger Collider.
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }

        Gizmos.matrix = originalMatrix;
    }
#endif
}