using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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

        RegisterHoverSounds(menuPanel);
        RegisterHoverSounds(creditsPanel);
    }

    public void PlayGame()
    {
        AudioManager.Instance?.PlayMenuStart();
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

    private static void RegisterHoverSounds(GameObject root)
    {
        if (root == null)
            return;

        var buttons = root.GetComponentsInChildren<Button>(true);
        foreach (var button in buttons)
        {
            if (button == null)
                continue;

            var trigger = button.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();
            bool hasPointerEnter = false;
            for (int i = 0; i < trigger.triggers.Count; i++)
            {
                if (trigger.triggers[i].eventID == EventTriggerType.PointerEnter)
                {
                    hasPointerEnter = true;
                    break;
                }
            }

            if (hasPointerEnter)
                continue;

            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ => AudioManager.Instance?.PlayMenuHover());
            trigger.triggers.Add(entry);
        }
    }
}
