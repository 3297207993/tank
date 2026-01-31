using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StartSceneControler : MonoBehaviour
{
    public void NewRoom()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("Host");
    }

    public void JoinRoom()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("Client");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
