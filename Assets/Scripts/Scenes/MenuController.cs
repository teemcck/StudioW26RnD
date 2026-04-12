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
        SceneManager.LoadScene("GameplayLoop");
    }

    public void OpenCredits()
    {
        if (creditsPanel)
            creditsPanel.SetActive(true);
        if (menuPanel)
            menuPanel.SetActive(false);
    }

    public void CloseCredits()
    {
        if (creditsPanel)
            creditsPanel.SetActive(false);
        if (menuPanel)
            menuPanel.SetActive(true);
    }

    public void QuitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
