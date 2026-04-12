using UnityEngine;
using UnityEngine.SceneManagement;

public class DeathSceneController : MonoBehaviour
{
    public void RetryGame()
    {
        SceneManager.LoadScene("GameplayLoop");
    }

    public void GoToMainMenu()
    {
        SceneManager.LoadScene("Menu");
    }
}
