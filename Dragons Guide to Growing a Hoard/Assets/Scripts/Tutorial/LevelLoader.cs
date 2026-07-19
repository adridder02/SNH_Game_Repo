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

    void Update()
    {
        if(Tutorial_1.Instance != null)
            if (Tutorial_1.Instance.tutorialStageComplete())
                {
                    Debug.Log("Done with Tut");
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
