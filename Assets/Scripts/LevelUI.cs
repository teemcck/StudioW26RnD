using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Drives all floor-related UI panels.
/// GameplayHandler waits on the three bool flags:
///   PlayerConfirmedStart  - player clicked Begin on the pre-floor preview.
///   SummaryConfirmed      - player clicked Continue on the XP summary.
///   RewardConfirmed       - player selected an upgrade on the reward screen.
///
/// The LevelUI GameObject can remain inactive in the editor.
/// GameplayHandler calls Activate() before the first panel is shown.
/// </summary>
public class LevelUI : MonoBehaviour
{
    [Header("Pre-Floor Preview Panel")]
    [SerializeField] private GameObject previewPanel;
    [SerializeField] private TextMeshProUGUI previewFloorIndexText;
    [SerializeField] private TextMeshProUGUI previewDifficultyText;
    [SerializeField] private Button previewBeginButton;

    [Header("Preview Animation")]
    [SerializeField] private float difficultyRollDuration = 0.75f;
    [SerializeField] private float difficultyRollInterval = 0.06f;

    [Header("XP Summary Panel")]
    [SerializeField] private GameObject summaryPanel;
    [SerializeField] private TextMeshProUGUI summaryKillsText;
    [SerializeField] private TextMeshProUGUI summaryAvoidedText;
    [SerializeField] private TextMeshProUGUI summaryXPText;
    [SerializeField] private Button summaryContinueButton;

    [Header("XP Bar Animation Panel")]
    [SerializeField] private GameObject xpBarPanel;
    [SerializeField] private Image xpBarFill;
    [SerializeField] private TextMeshProUGUI xpBarText;
    [SerializeField] private TextMeshProUGUI xpBarStatsText;
    [SerializeField] private Button xpBarContinueButton;

    [Header("World Transition")]
    [SerializeField] private WorldTransitionUI worldTransitionUI;

    public bool PlayerConfirmedStart { get; private set; }
    public bool SummaryConfirmed     { get; private set; }
    public bool RewardConfirmed      { get; private set; }
    public bool XPBarAnimationComplete { get; private set; }
    public bool TransitionConfirmed => worldTransitionUI == null || worldTransitionUI.TransitionConfirmed;

    private IEventBinding<UpgradeSelectedEvent> _upgradeSelectedBinding;
    private Coroutine _difficultyRollCoroutine;

    private void Awake()
    {
        // Ensure all panels start hidden regardless of editor state.
        previewPanel.SetActive(false);
        summaryPanel.SetActive(false);
        xpBarPanel.SetActive(false);
        worldTransitionUI?.HideImmediate();
    }

    private void OnEnable()
    {
        _upgradeSelectedBinding = EventBus<UpgradeSelectedEvent>.Register(OnUpgradeSelected);
    }

    private void OnDisable()
    {
        EventBus<UpgradeSelectedEvent>.Unsubscribe(_upgradeSelectedBinding);
    }

    // Called by GameplayHandler.cs

    /// <summary>
    /// Activates the LevelUI GameObject.
    /// Called by GameplayHandler on Start before any panel is shown.
    /// </summary>
    public void Activate()
    {
        gameObject.SetActive(true);
    }

    public void ShowFloorPreview(int difficulty, int floorIndex)
    {
        PlayerConfirmedStart = false;
        previewPanel.SetActive(true);
        if (_difficultyRollCoroutine != null)
            StopCoroutine(_difficultyRollCoroutine);

        previewFloorIndexText.text = $"Floor {floorIndex + 1}";
        previewDifficultyText.text = "Difficulty: ?";
        previewBeginButton.interactable = false;

        previewBeginButton.onClick.RemoveAllListeners();
        previewBeginButton.onClick.AddListener(() =>
        {
            previewPanel.SetActive(false);
            PlayerConfirmedStart = true;
        });

        _difficultyRollCoroutine = StartCoroutine(AnimateDifficultyRoll(difficulty));
    }

    public void ShowWorldTransition(string title, string body, string buttonLabel = "Continue")
    {
        if (worldTransitionUI == null)
        {
            Debug.LogWarning("[LevelUI] WorldTransitionUI is not assigned. Skipping transition screen.");
            return;
        }

        worldTransitionUI.Show(title, body, buttonLabel);
    }

    public void ShowXPSummary(int killed, int total, int xp)
    {
        SummaryConfirmed = false;
        summaryPanel.SetActive(true);

        int avoided = total - killed;
        summaryKillsText.text   = $"Enemies Killed: {killed}";
        summaryAvoidedText.text = $"Enemies Avoided: {avoided}";
        summaryXPText.text      = $"XP Gained: {xp}";

        summaryContinueButton.onClick.RemoveAllListeners();
        summaryContinueButton.onClick.AddListener(() =>
        {
            summaryPanel.SetActive(false);
            SummaryConfirmed = true;
        });
    }

    public void ShowXPBarAnimation(int previousXP, int floorXP, int killed, int total, float elapsed)
    {
        XPBarAnimationComplete = false;
        xpBarPanel.SetActive(true);
        if (xpBarContinueButton != null) xpBarContinueButton.gameObject.SetActive(false);

        int startXP = previousXP;
        int targetXP = previousXP + floorXP;

        // Calculate XP display as currentXP % xpPerFloor
        int xpPerFloor = GameplayHandler.Instance.XPPerFloor;
        int startDisplayXP = startXP % xpPerFloor;
        int targetDisplayXP = targetXP % xpPerFloor;

        // Handle floor-up: if target wrapped around, it means we progressed.
        if (targetDisplayXP < startDisplayXP)
        {
            // Animate from startDisplay to xpPerFloor, then continue from 0 to targetDisplay.
            targetDisplayXP += xpPerFloor;
        }

        float initialFill = (float)startDisplayXP / xpPerFloor;

        // Set initial state
        if (xpBarFill != null) xpBarFill.fillAmount = initialFill;
        if (xpBarText != null) xpBarText.text = $"{startDisplayXP} / {xpPerFloor} XP";

        // Display floor stats
        int avoided = total - killed;
        if (xpBarStatsText != null)
            xpBarStatsText.text = $"Enemies Killed: {killed}\nEnemies Avoided: {avoided}\nTime: {elapsed:F1}s";

        // Start animation coroutine
        StartCoroutine(AnimateXPBar(startXP, targetXP, xpPerFloor));
    }

    private System.Collections.IEnumerator AnimateDifficultyRoll(int finalDifficulty)
    {
        float elapsed = 0f;
        float interval = Mathf.Max(0.01f, difficultyRollInterval);
        float duration = Mathf.Max(0f, difficultyRollDuration);

        while (elapsed < duration)
        {
            previewDifficultyText.text = $"Difficulty: {Random.Range(GameConstants.MinDifficulty, GameConstants.MaxDifficulty + 1)}";
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        previewDifficultyText.text = $"Difficulty: {finalDifficulty}";
        previewBeginButton.interactable = true;
        _difficultyRollCoroutine = null;
    }

    private System.Collections.IEnumerator AnimateXPBar(int startXP, int targetXP, int xpPerFloor)
    {
        float animationDuration = 2f;
        float elapsed = 0f;

        // Calculate the XP range for this floor
        int floorXP = targetXP - startXP;

        // Calculate display range (modulo xpPerFloor)
        int startDisplayXP = startXP % xpPerFloor;
        int targetDisplayXP = targetXP % xpPerFloor;

        // Handle floor-up: if target wrapped around, it means we progressed.
        if (targetDisplayXP < startDisplayXP)
        {
            // Animate from startDisplay to xpPerFloor, then continue from 0 to targetDisplay
            targetDisplayXP += xpPerFloor;
        }

        float initialFill = (float)startDisplayXP / xpPerFloor;

        // Set initial fill based on previous XP
        xpBarFill.fillAmount = initialFill;
        xpBarText.text = $"{startDisplayXP} / {xpPerFloor} XP";

        // Wait a moment to show the starting state
        yield return new WaitForSeconds(0.5f);

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;

            // Smooth animation curve
            t = Mathf.SmoothStep(0f, 1f, t);

            // Calculate current XP display
            int gainedXP = Mathf.RoundToInt(floorXP * t);
            int currentTotalXP = startXP + gainedXP;
            int displayXP = currentTotalXP % xpPerFloor;

            // Calculate fill amount
            float fillAmount = (float)displayXP / xpPerFloor;

            xpBarFill.fillAmount = fillAmount;
            xpBarText.text = $"{displayXP} / {xpPerFloor} XP";

            yield return null;
        }

        // Ensure final state
        int finalDisplayXP = targetXP % xpPerFloor;
        xpBarFill.fillAmount = (float)finalDisplayXP / xpPerFloor;
        xpBarText.text = $"{finalDisplayXP} / {xpPerFloor} XP";

        // Show continue button
        xpBarContinueButton.gameObject.SetActive(true);
        xpBarContinueButton.onClick.RemoveAllListeners();
        xpBarContinueButton.onClick.AddListener(() =>
        {
            xpBarPanel.SetActive(false);
            XPBarAnimationComplete = true;
        });
    }

    // Event Handlers

    private void OnUpgradeSelected(UpgradeSelectedEvent evt)
    {
        RewardConfirmed = true;
    }

    public void ResetRewardConfirmed()
    {
        RewardConfirmed = false;
    }
}
