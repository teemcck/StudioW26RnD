using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives all level-related UI panels.
/// GameplayHandler waits on the three bool flags:
///   PlayerConfirmedStart  - player clicked Begin on the pre-level preview.
///   SummaryConfirmed      - player clicked Continue on the XP summary.
///   RewardConfirmed       - player selected an upgrade on the reward screen.
///
/// The LevelUI GameObject can remain inactive in the editor.
/// GameplayHandler calls Activate() before the first panel is shown.
/// </summary>
public class LevelUI : MonoBehaviour
{
    [Header("Pre-Level Preview Panel")]
    [SerializeField] private GameObject previewPanel;
    [SerializeField] private TextMeshProUGUI previewLevelIndexText;
    [SerializeField] private TextMeshProUGUI previewDifficultyText;
    [SerializeField] private TextMeshProUGUI previewLengthText;
    [SerializeField] private Button previewBeginButton;

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

    public bool PlayerConfirmedStart { get; private set; }
    public bool SummaryConfirmed     { get; private set; }
    public bool RewardConfirmed      { get; private set; }
    public bool XPBarAnimationComplete { get; private set; }

    private IEventBinding<UpgradeSelectedEvent> _upgradeSelectedBinding;

    private void Awake()
    {
        // Ensure all panels start hidden regardless of editor state.
        previewPanel.SetActive(false);
        summaryPanel.SetActive(false);
        xpBarPanel.SetActive(false);
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

    public void ShowLevelPreview(int difficulty, int chunkCount, int levelIndex)
    {
        PlayerConfirmedStart = false;
        previewPanel.SetActive(true);

        previewLevelIndexText.text = $"Level {levelIndex + 1}";
        previewDifficultyText.text = $"Difficulty: {difficulty}";
        previewLengthText.text     = $"Islands: {chunkCount}";

        previewBeginButton.onClick.RemoveAllListeners();
        previewBeginButton.onClick.AddListener(() =>
        {
            previewPanel.SetActive(false);
            PlayerConfirmedStart = true;
        });
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

    public void ShowXPBarAnimation(int previousXP, int levelXP, int killed, int total, float elapsed)
    {
        XPBarAnimationComplete = false;
        xpBarPanel.SetActive(true);
        if (xpBarContinueButton != null) xpBarContinueButton.gameObject.SetActive(false);

        int startXP = previousXP;
        int targetXP = previousXP + levelXP;

        // Calculate XP display as currentXP % xpPerLevel
        int xpPerLevel = GameplayHandler.Instance.XPPerLevel;
        int startDisplayXP = startXP % xpPerLevel;
        int targetDisplayXP = targetXP % xpPerLevel;

        // Handle level-up: if target wrapped around, it means we leveled up
        if (targetDisplayXP < startDisplayXP)
        {
            // Animate from startDisplay to xpPerLevel, then continue from 0 to targetDisplay
            targetDisplayXP += xpPerLevel;
        }

        float initialFill = (float)startDisplayXP / xpPerLevel;

        // Set initial state
        if (xpBarFill != null) xpBarFill.fillAmount = initialFill;
        if (xpBarText != null) xpBarText.text = $"{startDisplayXP} / {xpPerLevel} XP";

        // Display level stats
        int avoided = total - killed;
        if (xpBarStatsText != null)
            xpBarStatsText.text = $"Enemies Killed: {killed}\nEnemies Avoided: {avoided}\nTime: {elapsed:F1}s";

        // Start animation coroutine
        StartCoroutine(AnimateXPBar(startXP, targetXP, xpPerLevel));
    }

    private System.Collections.IEnumerator AnimateXPBar(int startXP, int targetXP, int xpPerLevel)
    {
        float animationDuration = 2f;
        float elapsed = 0f;

        // Calculate the XP range for this level
        int levelXP = targetXP - startXP;

        // Calculate display range (modulo xpPerLevel)
        int startDisplayXP = startXP % xpPerLevel;
        int targetDisplayXP = targetXP % xpPerLevel;

        // Handle level-up: if target wrapped around, it means we leveled up
        if (targetDisplayXP < startDisplayXP)
        {
            // Animate from startDisplay to xpPerLevel, then continue from 0 to targetDisplay
            targetDisplayXP += xpPerLevel;
        }

        float initialFill = (float)startDisplayXP / xpPerLevel;

        // Set initial fill based on previous XP
        xpBarFill.fillAmount = initialFill;
        xpBarText.text = $"{startDisplayXP} / {xpPerLevel} XP";

        // Wait a moment to show the starting state
        yield return new WaitForSeconds(0.5f);

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;

            // Smooth animation curve
            t = Mathf.SmoothStep(0f, 1f, t);

            // Calculate current XP display
            int gainedXP = Mathf.RoundToInt(levelXP * t);
            int currentTotalXP = startXP + gainedXP;
            int displayXP = currentTotalXP % xpPerLevel;

            // Calculate fill amount
            float fillAmount = (float)displayXP / xpPerLevel;

            xpBarFill.fillAmount = fillAmount;
            xpBarText.text = $"{displayXP} / {xpPerLevel} XP";

            yield return null;
        }

        // Ensure final state
        int finalDisplayXP = targetXP % xpPerLevel;
        xpBarFill.fillAmount = (float)finalDisplayXP / xpPerLevel;
        xpBarText.text = $"{finalDisplayXP} / {xpPerLevel} XP";

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