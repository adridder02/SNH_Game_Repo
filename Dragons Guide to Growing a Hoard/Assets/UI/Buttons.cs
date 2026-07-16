using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
public class Buttons : MonoBehaviour
{     
    public void QuitGame() 
    {   
        Debug.Log("Game is exiting");
        //Just to make sure its working
        Application.Quit();
        
    }

    public void RestartGame()
    {   
        Debug.Log("Game is restarting");
        //Just to make sure its working
        SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);
    }
}
