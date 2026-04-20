using UnityEngine;
using UnityEngine.SceneManagement;

public class DeathSceneController : MonoBehaviour
{
    public void RetryGame()
    {
        AudioManager.Instance?.PlayUiButton();
        SceneManager.LoadScene("GameplayLoop");
    }

    public void GoToMainMenu()
    {
        AudioManager.Instance?.PlayUiButton();
        SceneManager.LoadScene("MenuScene");
    }
}
