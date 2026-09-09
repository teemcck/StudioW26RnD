using System.Collections;
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

    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private Image vignetteImage;
    [SerializeField] private float fadeInDuration = 1.0f;
    [SerializeField] private Color vignetteTargetColor = new Color(0f, 0.08f, 0.12f, 0.6f);

    public bool TransitionConfirmed { get; private set; }

    private Coroutine _fadeRoutine;

    private void Awake() => HideImmediate();

    public void Show(string title, string body, string buttonLabel = "Continue")
    {
        TransitionConfirmed = false;

        if (transitionPanel != null) transitionPanel.SetActive(true);
        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = body;
        if (continueButtonText != null) continueButtonText.text = buttonLabel;

        if (continueButton == null)
        {
            TransitionConfirmed = true;
            return;
        }

        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(Confirm);

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeIn());
    }

    public void HideImmediate()
    {
        TransitionConfirmed = false;
        if (transitionPanel != null) transitionPanel.SetActive(false);
        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;
        if (vignetteImage != null) vignetteImage.color = new Color(0f, 0f, 0f, 0f);
    }

    private IEnumerator FadeIn()
    {
        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;

        float dur = Mathf.Max(0.01f, fadeInDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = u;
            if (vignetteImage != null) vignetteImage.color = Color.Lerp(new Color(0f, 0f, 0f, 0f), vignetteTargetColor, u);
            yield return null;
        }

        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 1f;
        if (vignetteImage != null) vignetteImage.color = vignetteTargetColor;
        _fadeRoutine = null;
    }

    private void Confirm()
    {
        AudioManager.Instance?.PlayUiButton();
        VolumeSwitcher.Instance?.NotifyWorldDescendConfirmed();

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = null;

        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;
        if (vignetteImage != null) vignetteImage.color = new Color(0f, 0f, 0f, 0f);
        if (transitionPanel != null) transitionPanel.SetActive(false);

        TransitionConfirmed = true;
    }
}
