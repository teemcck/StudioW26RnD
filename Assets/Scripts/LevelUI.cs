using System.Collections;
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
    [SerializeField] private TextMeshProUGUI summaryTimeText;
    [SerializeField] private TextMeshProUGUI summaryXPText;
    [SerializeField] private Button summaryContinueButton;

    [Header("XP Summary feel")]
    [SerializeField] private float summarySlamDuration = 0.38f;
    [SerializeField] private float summarySlamOffscreenX = 1400f;
    [SerializeField] private float summarySectionVerticalGap = 22f;
    [SerializeField] private float summaryShakePixels = 3f;
    [SerializeField] private float summaryShakeDuration = 0.11f;
    [SerializeField] private float summaryInterBlockPause = 0.06f;
    [SerializeField] private float summaryCountKillDur = 0.28f;
    [SerializeField] private float summaryCountAvoidDur = 0.52f;
    [SerializeField] private float summaryCountTimeDur = 0.55f;
    [SerializeField] private float summaryCountTotalDur = 0.58f;
    [SerializeField] private float summaryScalePunchAmount = 0.06f;
    [SerializeField] private float summaryScalePunchDuration = 0.14f;
    [SerializeField] private float summaryCountTickInterval = 0.09f;

    [Header("XP Bar Animation Panel")]
    [SerializeField] private GameObject xpBarPanel;
    [SerializeField] private RawImage xpBarFill;
    [SerializeField] private TextMeshProUGUI xpBarText;
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
    private Coroutine _summaryBreakdownCoroutine;
    private RectTransform _xpBarFillRect;
    private float _xpBarBaseWidth;
    private bool _cachedXpBarWidth;
    private bool _summaryLayoutCached;
    private Vector2 _killsAnchoredRest;
    private Vector2 _timeAnchoredRest;
    private Vector2 _xpAnchoredRest;
    private Vector2 _summaryPanelAnchoredRest;
    private Vector3 _killsScaleRest = Vector3.one;
    private Vector3 _timeScaleRest = Vector3.one;
    private Vector3 _xpScaleRest = Vector3.one;
    private float _nextSummaryCountTickUnscaled = -999f;

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
        if (_summaryBreakdownCoroutine != null)
        {
            StopCoroutine(_summaryBreakdownCoroutine);
            _summaryBreakdownCoroutine = null;
            RestoreSummaryPresentationTransforms();
        }
    }

    private void RestoreSummaryPresentationTransforms()
    {
        if (summaryContinueButton != null)
            summaryContinueButton.interactable = true;
        if (summaryPanel != null && summaryPanel.TryGetComponent<RectTransform>(out var panelRt))
            panelRt.anchoredPosition = _summaryPanelAnchoredRest;
        if (!_summaryLayoutCached)
            return;
        if (summaryKillsText != null)
        {
            summaryKillsText.rectTransform.anchoredPosition = _killsAnchoredRest;
            summaryKillsText.rectTransform.localScale = _killsScaleRest;
        }
        if (summaryTimeText != null)
        {
            summaryTimeText.rectTransform.anchoredPosition = _timeAnchoredRest;
            summaryTimeText.rectTransform.localScale = _timeScaleRest;
        }
        if (summaryXPText != null)
        {
            summaryXPText.rectTransform.anchoredPosition = _xpAnchoredRest;
            summaryXPText.rectTransform.localScale = _xpScaleRest;
        }
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
            AudioManager.Instance?.PlayUiButton();
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

    public void ShowXPSummary(int killed, int total, float elapsedFloorSeconds, GameplayHandler.FloorXPBreakdown xp)
    {
        SummaryConfirmed = false;
        summaryPanel.SetActive(true);

        if (_summaryBreakdownCoroutine != null)
        {
            StopCoroutine(_summaryBreakdownCoroutine);
            _summaryBreakdownCoroutine = null;
        }

        int avoided = Mathf.Max(0, total - killed);
        string floorTime = FormatElapsedHms(elapsedFloorSeconds);

        if (summaryAvoidedText != null)
            summaryAvoidedText.gameObject.SetActive(false);

        if (summaryContinueButton != null)
            summaryContinueButton.interactable = false;

        _summaryBreakdownCoroutine = StartCoroutine(PlaySummaryPresentation(killed, avoided, floorTime, xp));
        summaryContinueButton.onClick.RemoveAllListeners();
        summaryContinueButton.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayUiButton();
            summaryPanel.SetActive(false);
            if (summaryAvoidedText != null)
                summaryAvoidedText.gameObject.SetActive(true);
            SummaryConfirmed = true;
        });
    }

    private void CacheSummaryLayout()
    {
        if (_summaryLayoutCached)
            return;

        if (summaryKillsText != null)
        {
            RectTransform krt = summaryKillsText.rectTransform;
            _killsAnchoredRest = krt.anchoredPosition;
            _killsScaleRest = krt.localScale;
        }
        if (summaryTimeText != null)
        {
            RectTransform trt = summaryTimeText.rectTransform;
            _timeAnchoredRest = trt.anchoredPosition;
            _timeAnchoredRest += new Vector2(0f, -summarySectionVerticalGap);
            _timeScaleRest = trt.localScale;
        }
        if (summaryXPText != null)
        {
            RectTransform xrt = summaryXPText.rectTransform;
            _xpAnchoredRest = xrt.anchoredPosition;
            _xpAnchoredRest += new Vector2(0f, -summarySectionVerticalGap * 2f);
            _xpScaleRest = xrt.localScale;
        }

        RectTransform panelRt = summaryPanel != null ? summaryPanel.GetComponent<RectTransform>() : null;
        if (panelRt != null)
            _summaryPanelAnchoredRest = panelRt.anchoredPosition;

        _summaryLayoutCached = true;
    }

    private IEnumerator PlaySummaryPresentation(int killed, int avoided, string floorTimeHms, GameplayHandler.FloorXPBreakdown xp)
    {
        CacheSummaryLayout();
        Vector2 off = new Vector2(-Mathf.Abs(summarySlamOffscreenX), 0f);

        RectTransform killsRt = summaryKillsText != null ? summaryKillsText.rectTransform : null;
        RectTransform timeRt = summaryTimeText != null ? summaryTimeText.rectTransform : null;
        RectTransform xpRt = summaryXPText != null ? summaryXPText.rectTransform : null;
        RectTransform panelRt = summaryPanel != null ? summaryPanel.GetComponent<RectTransform>() : null;

        if (timeRt != null)
        {
            summaryTimeText.text = string.Empty;
            timeRt.anchoredPosition = _timeAnchoredRest + off;
        }
        if (xpRt != null)
        {
            summaryXPText.text = string.Empty;
            xpRt.anchoredPosition = _xpAnchoredRest + off;
        }
        if (killsRt != null)
        {
            summaryKillsText.text = FormatEnemyXpBlock(killed, avoided, 0, 0);
            killsRt.anchoredPosition = _killsAnchoredRest + off;
            killsRt.localScale = _killsScaleRest;
        }
        if (timeRt != null)
            timeRt.localScale = _timeScaleRest;
        if (xpRt != null)
            xpRt.localScale = _xpScaleRest;

        _nextSummaryCountTickUnscaled = -999f;
        yield return null;

        if (killsRt != null)
        {
            yield return SlamRect(killsRt, _killsAnchoredRest + off, _killsAnchoredRest, summarySlamDuration);
            AudioManager.Instance?.PlayXpSummaryBlockLand(1.5f);
            yield return ScalePunchRect(killsRt, _killsScaleRest);
            yield return ShakeSummaryPanel(panelRt);
            yield return CountEnemyXp(killed, avoided, xp);
            if (summaryInterBlockPause > 0f)
                yield return new WaitForSecondsRealtime(summaryInterBlockPause);
        }

        if (timeRt != null)
        {
            summaryTimeText.text = FormatTimeBonusBlock(floorTimeHms, 0);
            yield return SlamRect(timeRt, _timeAnchoredRest + off, _timeAnchoredRest, summarySlamDuration);
            AudioManager.Instance?.PlayXpSummaryBlockLand(1.5f);
            yield return ScalePunchRect(timeRt, _timeScaleRest);
            yield return ShakeSummaryPanel(panelRt);
            yield return CountTimeXp(floorTimeHms, xp);
            if (summaryInterBlockPause > 0f)
                yield return new WaitForSecondsRealtime(summaryInterBlockPause);
        }

        if (xpRt != null)
        {
            summaryXPText.text = FormatTotalXpBlock(0);
            yield return SlamRect(xpRt, _xpAnchoredRest + off, _xpAnchoredRest, summarySlamDuration);
            AudioManager.Instance?.PlayXpSummaryBlockLand(1.5f);
            yield return ScalePunchRect(xpRt, _xpScaleRest);
            yield return ShakeSummaryPanel(panelRt);
            yield return CountTotalXp(xp);
        }

        yield return new WaitForSecondsRealtime(0.4f);
        AudioManager.Instance?.PlayContinueReady();

        if (summaryContinueButton != null)
            summaryContinueButton.interactable = true;

        _summaryBreakdownCoroutine = null;
    }

    private IEnumerator SlamRect(RectTransform rt, Vector2 start, Vector2 end, float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            float eased = EaseOutQuart(u);
            rt.anchoredPosition = Vector2.LerpUnclamped(start, end, eased);
            yield return null;
        }
        rt.anchoredPosition = end;
    }

    private IEnumerator ScalePunchRect(RectTransform rt, Vector3 restLocalScale)
    {
        if (rt == null)
            yield break;

        float dur = Mathf.Max(0.01f, summaryScalePunchDuration);
        float mag = Mathf.Max(0f, summaryScalePunchAmount);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            float bell = Mathf.Sin(u * Mathf.PI);
            float punch = bell * mag;
            rt.localScale = restLocalScale * (1f + punch);
            yield return null;
        }
        rt.localScale = restLocalScale;
    }

    private static float EaseOutQuart(float x) => 1f - Mathf.Pow(1f - x, 4f);

    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    private void TryPlaySummaryCountTick(float progress01 = 0f)
    {
        float interval = Mathf.Max(0.02f, summaryCountTickInterval);
        if (Time.unscaledTime - _nextSummaryCountTickUnscaled < interval)
            return;
        _nextSummaryCountTickUnscaled = Time.unscaledTime;
        float pitch = Mathf.Lerp(1.25f, 2.25f, Mathf.Clamp01(progress01));
        AudioManager.Instance?.PlayXpSummaryCountTick(pitch: pitch);
    }

    private IEnumerator ShakeSummaryPanel(RectTransform panelRt)
    {
        if (panelRt == null)
            yield break;

        float mag = Mathf.Max(0f, summaryShakePixels);
        float dur = Mathf.Max(0.01f, summaryShakeDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float decay = 1f - Mathf.Clamp01(t / dur);
            float w = mag * decay * decay;
            panelRt.anchoredPosition = _summaryPanelAnchoredRest + (Vector2)Random.insideUnitCircle * w;
            yield return null;
        }
        panelRt.anchoredPosition = _summaryPanelAnchoredRest;
    }

    private IEnumerator CountEnemyXp(int killed, int avoided, GameplayHandler.FloorXPBreakdown xp)
    {
        float start = Time.unscaledTime;
        float killDur = Mathf.Max(0.01f, summaryCountKillDur);
        float avoidDur = Mathf.Max(0.01f, summaryCountAvoidDur);
        float endT = Mathf.Max(killDur, avoidDur);
        while (Time.unscaledTime - start < endT)
        {
            float now = Time.unscaledTime - start;
            float killT = EaseOutCubic(Mathf.Clamp01(now / killDur));
            float avoidT = EaseOutCubic(Mathf.Clamp01(now / avoidDur));
            int killShown = Mathf.RoundToInt(Mathf.Lerp(0f, xp.KillXP, killT));
            int avoidShown = Mathf.RoundToInt(Mathf.Lerp(0f, xp.AvoidXP, avoidT));
            if (summaryKillsText != null)
                summaryKillsText.text = FormatEnemyXpBlock(killed, avoided, killShown, avoidShown);
            if (killShown > 0 || avoidShown > 0)
                TryPlaySummaryCountTick(Mathf.Max(killT, avoidT));
            yield return null;
        }
        if (summaryKillsText != null)
            summaryKillsText.text = FormatEnemyXpBlock(killed, avoided, xp.KillXP, xp.AvoidXP);
    }

    private IEnumerator CountTimeXp(string floorTimeHms, GameplayHandler.FloorXPBreakdown xp)
    {
        float start = Time.unscaledTime;
        float dur = Mathf.Max(0.01f, summaryCountTimeDur);
        while (Time.unscaledTime - start < dur)
        {
            float now = Time.unscaledTime - start;
            float te = EaseOutCubic(Mathf.Clamp01(now / dur));
            int timeShown = Mathf.RoundToInt(Mathf.Lerp(0f, xp.TimeXP, te));
            if (summaryTimeText != null)
                summaryTimeText.text = FormatTimeBonusBlock(floorTimeHms, timeShown);
            if (timeShown > 0)
                TryPlaySummaryCountTick(te);
            yield return null;
        }
        if (summaryTimeText != null)
            summaryTimeText.text = FormatTimeBonusBlock(floorTimeHms, xp.TimeXP);
    }

    private IEnumerator CountTotalXp(GameplayHandler.FloorXPBreakdown xp)
    {
        float start = Time.unscaledTime;
        float dur = Mathf.Max(0.01f, summaryCountTotalDur);
        while (Time.unscaledTime - start < dur)
        {
            float now = Time.unscaledTime - start;
            float u = EaseOutCubic(Mathf.Clamp01(now / dur));
            int shown = Mathf.RoundToInt(Mathf.Lerp(0f, xp.TotalXP, u));
            if (summaryXPText != null)
                summaryXPText.text = FormatTotalXpBlock(shown);
            if (shown > 0)
                TryPlaySummaryCountTick(u);
            yield return null;
        }
        if (summaryXPText != null)
            summaryXPText.text = FormatTotalXpBlock(xp.TotalXP);
        AudioManager.Instance?.PlayXpSummaryTotalComplete();
    }

    private static string FormatEnemyXpBlock(int killed, int avoided, int killXp, int avoidXp)
    {
        return
            "<b>Enemy XP</b>\n" +
            $"    Kills · {killed} · ({killXp} xp)\n" +
            $"    Avoided · {avoided} · ({avoidXp} xp)";
    }

    private static string FormatTimeBonusBlock(string floorTimeHms, int timeXp)
    {
        return
            "<b>Time bonus</b>\n" +
            $"    {floorTimeHms} · ({timeXp} xp)";
    }

    private static string FormatTotalXpBlock(int totalXp)
    {
        return $"<b>Total XP</b>\n{totalXp} xp";
    }

    private static string FormatElapsedHms(float elapsedSeconds)
    {
        int es = Mathf.Max(0, Mathf.FloorToInt(elapsedSeconds));
        int h = es / 3600;
        int m = (es % 3600) / 60;
        int s = es % 60;
        return $"{h:00}:{m:00}:{s:00}";
    }

    public void ShowXPBarAnimation(int previousXP, int floorXP, int killed, int total, float elapsed)
    {
        XPBarAnimationComplete = false;
        xpBarPanel.SetActive(true);
        if (xpBarContinueButton != null) xpBarContinueButton.gameObject.SetActive(false);
        CacheXpBarWidth();

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
        SetXpBarFill(initialFill);
        if (xpBarText != null) xpBarText.text = $"{startDisplayXP}";

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
        SetXpBarFill(initialFill);
        xpBarText.text = $"{startDisplayXP}";

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

            SetXpBarFill(fillAmount);
            xpBarText.text = $"{displayXP}";

            yield return null;
        }

        // Ensure final state
        int finalDisplayXP = targetXP % xpPerFloor;
        SetXpBarFill((float)finalDisplayXP / xpPerFloor);
        xpBarText.text = $"{finalDisplayXP}";

        // Show continue button
        xpBarContinueButton.gameObject.SetActive(true);
        xpBarContinueButton.onClick.RemoveAllListeners();
        xpBarContinueButton.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayUiButton();
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

    private void CacheXpBarWidth()
    {
        if (_cachedXpBarWidth || xpBarFill == null)
            return;

        _xpBarFillRect = xpBarFill.rectTransform;
        if (_xpBarFillRect == null)
            return;

        if (_xpBarFillRect.pivot.x != 0f)
            _xpBarFillRect.pivot = new Vector2(0f, _xpBarFillRect.pivot.y);

        _xpBarBaseWidth = _xpBarFillRect.sizeDelta.x;
        _cachedXpBarWidth = _xpBarBaseWidth > 0f;
    }

    private void SetXpBarFill(float normalized)
    {
        if (xpBarFill == null)
            return;

        CacheXpBarWidth();
        if (_xpBarFillRect == null || !_cachedXpBarWidth)
            return;

        float clamped = Mathf.Clamp01(normalized);
        _xpBarFillRect.sizeDelta = new Vector2(_xpBarBaseWidth * clamped, _xpBarFillRect.sizeDelta.y);
    }
}
