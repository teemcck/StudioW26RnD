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

    public bool PlayerConfirmedStart { get; private set; }
    public bool SummaryConfirmed     { get; private set; }
    public bool RewardConfirmed      { get; private set; }

    private IEventBinding<UpgradeSelectedEvent> _upgradeSelectedBinding;

    private void Awake()
    {
        // Ensure all panels start hidden regardless of editor state.
        previewPanel.SetActive(false);
        summaryPanel.SetActive(false);
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

    // Event Handlers

    private void OnUpgradeSelected(UpgradeSelectedEvent evt)
    {
        RewardConfirmed = true;
    }
}