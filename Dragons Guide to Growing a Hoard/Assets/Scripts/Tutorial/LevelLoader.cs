using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Data;
public class LevelLoader : MonoBehaviour
{   
    public Animator transition;
    public static float delay = 0f;
    public float transitionTime = 5f;

    [Tooltip("The mission that must be fully complete for this level to advance. Replaces the old " +
             "Tutorial_1.Instance.tutorialStageComplete() check now that completion lives in MissionProgressManager.")]
    [SerializeField] private MissionData mission;

    // Guards against LoadNextLevel() firing every single frame once the mission is complete
    // (the original Tutorial_1-based check had this same gap).
    private bool nextLevelTriggered = false;

    void Update()
    {
        if (nextLevelTriggered || mission == null || MissionProgressManager.Instance == null)
            return;

        if (MissionProgressManager.Instance.IsMissionComplete(mission))
        {
            Debug.Log("Done with Tut");
            nextLevelTriggered = true;
            LoadNextLevel();
        }
    }
    public void LoadNextLevel()
    {   
        int Index = SceneManager.GetActiveScene().buildIndex + 1;
        if (Index < 2)
        { StartCoroutine(LoadLevel(SceneManager.GetActiveScene().buildIndex + 1)); }
    }

    IEnumerator LoadLevel(int levelIndex)
    {    
        transition.SetTrigger("Start");
        yield return new WaitForSeconds(transitionTime);
        SceneManager.LoadScene(levelIndex);
    }
}
