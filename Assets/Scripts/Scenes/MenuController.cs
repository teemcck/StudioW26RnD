using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    [SerializeField] private GameObject creditsPanel;

    private void Start()
    {
        if (creditsPanel)
            creditsPanel.SetActive(false);
    }

    public void PlayGame()
    {
        SceneManager.LoadScene("GameplayLoop");
    }

    public void OpenCredits()
    {
        if (creditsPanel)
            creditsPanel.SetActive(true);
    }

    public void CloseCredits()
    {
        if (creditsPanel)
            creditsPanel.SetActive(false);
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
