using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private GameObject menuPanel;

    private void Start()
    {
        if (creditsPanel)
            creditsPanel.SetActive(false);
        if (menuPanel)
            menuPanel.SetActive(true);
    }

    public void PlayGame()
    {
        AudioManager.Instance?.PlayUiButton();
        SceneManager.LoadScene("GameplayLoop");
    }

    public void OpenCredits()
    {
        AudioManager.Instance?.PlayUiButton();
        if (creditsPanel)
            creditsPanel.SetActive(true);
        if (menuPanel)
            menuPanel.SetActive(false);
    }

    public void CloseCredits()
    {
        AudioManager.Instance?.PlayUiButton();
        if (creditsPanel)
            creditsPanel.SetActive(false);
        if (menuPanel)
            menuPanel.SetActive(true);
    }

    public void QuitGame()
    {
        AudioManager.Instance?.PlayUiButton();
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
