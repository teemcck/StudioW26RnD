using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldTransitionUI : MonoBehaviour
{
    [SerializeField] private GameObject transitionPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private Button continueButton;
    [SerializeField] private TextMeshProUGUI continueButtonText;

    public bool TransitionConfirmed { get; private set; }

    private void Awake()
    {
        HideImmediate();
    }

    public void Show(string title, string body, string buttonLabel = "Continue")
    {
        TransitionConfirmed = false;

        if (transitionPanel != null)
            transitionPanel.SetActive(true);

        if (titleText != null)
            titleText.text = title;

        if (bodyText != null)
            bodyText.text = body;

        if (continueButtonText != null)
            continueButtonText.text = buttonLabel;

        if (continueButton == null)
        {
            Debug.LogWarning("[WorldTransitionUI] Continue button is not assigned. Auto-confirming transition.");
            TransitionConfirmed = true;
            return;
        }

        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(Confirm);
    }

    public void HideImmediate()
    {
        TransitionConfirmed = false;
        if (transitionPanel != null)
            transitionPanel.SetActive(false);
    }

    private void Confirm()
    {
        if (transitionPanel != null)
            transitionPanel.SetActive(false);

        TransitionConfirmed = true;
    }
}
