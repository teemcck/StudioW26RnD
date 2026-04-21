using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages the player HUD display including health and XP bars.
/// Updates bar widths and text values each frame based on player stats.
/// Bars are only visible during active gameplay, hidden otherwise.
/// </summary>
public sealed class PlayerHudUI : MonoBehaviour
{
    // Canvas references for visibility control
    [SerializeField] private Canvas hudCanvas;

    // Health bar UI references
    [SerializeField] private RectTransform healthFillRect;
    [SerializeField] private TextMeshProUGUI healthValueText;

    // XP bar UI references
    [SerializeField] private RectTransform xpFillRect;
    [SerializeField] private TextMeshProUGUI xpValueText;

    // Bar scaling constants - adjust these to control fill appearance
    private const float BAR_FILL_MIN = 0.02f; // Minimum anchor when bar is empty
    private const float BAR_FILL_MAX = 0.98f; // Maximum anchor when bar is full

    [Header("Animation")]
    [SerializeField] private float fillLerpSpeed = 10f;

    [Header("Upgrade strip (boss / single-scene builds)")]
    [SerializeField] private GameObject upgradeStripIconPrefab;
    [SerializeField] private GameObject upgradeStripOverflowChipPrefab;

    private PlayerHealth _playerHealth;
    private PlayerStats _playerStats;
    private CanvasGroup _hudCanvasGroup;
    private Vector2 _healthFillBaseSize;
    private Vector2 _xpFillBaseSize;
    private float _healthFillAnchoredX;
    private float _xpFillAnchoredX;
    private bool _cachedBaseSizes;
    private float _displayedHealthNormalized = 1f;
    private float _displayedXpNormalized;
    private bool _initializedDisplayedValues;
    private bool _bossCutsceneSuppressHud;
    private bool _bossHudIntroFadeActive;
    private float _bossHudIntroFadeAlpha;

    private void Awake()
    {
        _playerHealth = GetComponent<PlayerHealth>();
        _playerStats = GetComponent<PlayerStats>();
        EnsureUi();
        FixHudCanvasScaleIfBroken();

        SetHudVisibility(false);
    }

    private void Start()
    {
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.EnsurePersistentAppliedUpgradeStrip(upgradeStripIconPrefab, upgradeStripOverflowChipPrefab);
    }

    private void OnEnable()
    {
        // Boss intro toggles canvas/cutscene flags; re-sync bar baselines when the player is enabled again.
        _initializedDisplayedValues = false;
    }

    /// <summary>
    /// Boss intro hides the HUD without disabling gameplay state; avoids putting CanvasGroup on the
    /// player root (which would fight the HUD canvas CanvasGroup every LateUpdate).
    /// </summary>
    public void SetBossCutsceneHudSuppressed(bool suppress)
    {
        _bossCutsceneSuppressHud = suppress;
    }

    /// <summary>
    /// Boss intro: after letterbox, fade the player HUD in together with the boss bar. Alpha 0 keeps the
    /// canvas updating (bars) while invisible. Call <see cref="ClearBossHudIntroFade"/> when the fade ends.
    /// </summary>
    public void SetBossHudIntroFade(float alpha01)
    {
        _bossHudIntroFadeActive = true;
        _bossHudIntroFadeAlpha = Mathf.Clamp01(alpha01);
    }

    public void ClearBossHudIntroFade()
    {
        _bossHudIntroFadeActive = false;
    }

    public static void InvalidateAllDisplayedValues()
    {
        foreach (var hud in Object.FindObjectsByType<PlayerHudUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            hud.ClearSmoothedHudState();
    }

    private void ClearSmoothedHudState()
    {
        _initializedDisplayedValues = false;
    }

    private void LateUpdate()
    {
        bool isGameplayActive = IsGameplayActive();
        bool hudAllowed = isGameplayActive && !_bossCutsceneSuppressHud;
        bool barUpdates = hudAllowed || _bossHudIntroFadeActive;

        if (_bossHudIntroFadeActive)
        {
            EnsureUi();
            if (hudCanvas != null)
                hudCanvas.enabled = true;
            if (_hudCanvasGroup != null)
            {
                _hudCanvasGroup.alpha = _bossHudIntroFadeAlpha;
                _hudCanvasGroup.blocksRaycasts = false;
                _hudCanvasGroup.interactable = false;
            }
        }
        else
            SetHudVisibility(hudAllowed);

        if (_playerHealth == null || _playerStats == null)
            return;

        if (barUpdates)
        {
            UpdateHealthBar();
            UpdateXpBar();
        }
    }

    /// <summary>
    /// Updates the health bar width and text display.
    /// Bar scales from BAR_FILL_MIN to BAR_FILL_MAX based on current/max health.
    /// </summary>
    private void UpdateHealthBar()
    {
        EnsureUi();

        float maxHealth = Mathf.Max(1f, _playerStats.MaxHealth);
        float currentHealth = Mathf.Clamp(_playerHealth.CurrentHealth, 0f, maxHealth);
        float healthNormalized = currentHealth / maxHealth;
        _displayedHealthNormalized = Mathf.Lerp(_displayedHealthNormalized, healthNormalized, Time.deltaTime * fillLerpSpeed);

        // Update bar width
        if (healthFillRect)
        {
            SetFillWidth(healthFillRect, _healthFillBaseSize, _healthFillAnchoredX, _displayedHealthNormalized);
        }

        // Update text display
        if (healthValueText)
        {
            int displayHealth = Mathf.CeilToInt(currentHealth);
            int displayMaxHealth = Mathf.CeilToInt(maxHealth);
            healthValueText.text = $"HP {displayHealth} / {displayMaxHealth}";
        }
    }

    /// <summary>
    /// Updates the XP bar width and text display.
    /// Bar scales from BAR_FILL_MIN to BAR_FILL_MAX based on current XP progress toward next level.
    /// </summary>
    private void UpdateXpBar()
    {
        EnsureUi();

        int totalXp = RunStatsTracker.Instance != null ? Mathf.Max(0, RunStatsTracker.Instance.TotalXP) : 0;
        int xpThreshold = GameplayHandler.Instance != null
            ? Mathf.Max(1, GameplayHandler.Instance.XPPerFloor)
            : Mathf.Max(1, GameplayHandler.LastPublishedXpPerFloor);
        int displayXp = totalXp % xpThreshold;
        float xpNormalized = (float)displayXp / xpThreshold;
        _displayedXpNormalized = Mathf.Lerp(_displayedXpNormalized, xpNormalized, Time.deltaTime * fillLerpSpeed);

        if (xpFillRect)
        {
            SetFillWidth(xpFillRect, _xpFillBaseSize, _xpFillAnchoredX, _displayedXpNormalized);
            ApplyAlmostLevelPulse(xpNormalized);
        }

        if (xpValueText)
        {
            xpValueText.text = $"XP {displayXp} / {xpThreshold}";
        }
    }

    private void ApplyAlmostLevelPulse(float xpNormalized)
    {
        if (xpFillRect == null)
            return;

        var img = xpFillRect.GetComponent<UnityEngine.UI.Image>();
        if (img == null)
            return;

        bool almost = xpNormalized >= 0.85f;
        if (almost)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2f * Mathf.PI * 2f);
            xpFillRect.localScale = new Vector3(1f, Mathf.Lerp(0.95f, 1.05f, pulse), 1f);
            img.color = Color.Lerp(Color.white, GameColors.Reward, 0.45f + 0.35f * pulse);
        }
        else
        {
            xpFillRect.localScale = Vector3.one;
            img.color = Color.white;
        }
    }

    /// <summary>
    /// Checks if gameplay is currently active (not in preview or between floors).
    /// </summary>
    private bool IsGameplayActive()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "BossGameplay")
            return true;

        if (GameplayHandler.Instance == null)
            return false;

        // HUD should only show during active floor gameplay
        // Hidden during floor preview, between floors, and after floor ends
        return GameplayHandler.Instance.CurrentState == GameplayHandler.FloorState.Playing;
    }

    /// <summary>
    /// Controls visibility of the entire HUD canvas.
    /// </summary>
    private void SetHudVisibility(bool isVisible)
    {
        EnsureUi();

        if (hudCanvas == null)
            return;

        hudCanvas.enabled = isVisible;

        if (_hudCanvasGroup != null)
        {
            _hudCanvasGroup.alpha = isVisible ? 1f : 0f;
            _hudCanvasGroup.blocksRaycasts = false;
            _hudCanvasGroup.interactable = false;
        }
    }

    private void EnsureUi()
    {
        if (hudCanvas == null)
            hudCanvas = GetComponentInChildren<Canvas>(true);

        if (healthFillRect == null)
            healthFillRect = FindChildRect("HealthFill");

        if (healthValueText == null)
            healthValueText = FindChildText("HealthText");

        if (xpFillRect == null)
            xpFillRect = FindChildRect("XPFill");

        if (xpValueText == null)
            xpValueText = FindChildText("XPText");

        if (hudCanvas != null && _hudCanvasGroup == null)
            _hudCanvasGroup = hudCanvas.GetComponent<CanvasGroup>() ?? hudCanvas.gameObject.AddComponent<CanvasGroup>();

        if (!_cachedBaseSizes && healthFillRect != null && xpFillRect != null)
        {
            NormalizeFillRect(healthFillRect);
            NormalizeFillRect(xpFillRect);
            _healthFillBaseSize = healthFillRect.sizeDelta;
            _xpFillBaseSize = xpFillRect.sizeDelta;
            _healthFillAnchoredX = healthFillRect.anchoredPosition.x;
            _xpFillAnchoredX = xpFillRect.anchoredPosition.x;
            _cachedBaseSizes = true;
        }

        if (!_initializedDisplayedValues && _playerHealth != null && _playerStats != null)
        {
            float maxHealth = Mathf.Max(1f, _playerStats.MaxHealth);
            _displayedHealthNormalized = Mathf.Clamp01(_playerHealth.CurrentHealth / maxHealth);

            int totalXp = RunStatsTracker.Instance != null ? Mathf.Max(0, RunStatsTracker.Instance.TotalXP) : 0;
            int xpThreshold = GameplayHandler.Instance != null
                ? Mathf.Max(1, GameplayHandler.Instance.XPPerFloor)
                : Mathf.Max(1, GameplayHandler.LastPublishedXpPerFloor);
            _displayedXpNormalized = Mathf.Clamp01((float)(totalXp % xpThreshold) / xpThreshold);
            _initializedDisplayedValues = true;
        }

        if (hudCanvas == null)
            Debug.LogWarning("[PlayerHudUI] Missing child Canvas on the player prefab.");

        if (healthFillRect == null || healthValueText == null || xpFillRect == null || xpValueText == null)
            Debug.LogWarning("[PlayerHudUI] Missing HUD references. Expected child objects named HealthFill, HealthText, XPFill, and XPText.");
    }

    private void FixHudCanvasScaleIfBroken()
    {
        if (hudCanvas == null)
            return;

        var rt = hudCanvas.transform as RectTransform;
        if (rt == null)
            return;

        if (rt.localScale.x < 0.001f || rt.localScale.y < 0.001f || rt.localScale.z < 0.001f)
            rt.localScale = Vector3.one;
    }

    private RectTransform FindChildRect(string childName)
    {
        Transform child = FindDescendant(transform, childName);
        return child != null ? child.GetComponent<RectTransform>() : null;
    }

    private TextMeshProUGUI FindChildText(string childName)
    {
        Transform child = FindDescendant(transform, childName);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private Transform FindDescendant(Transform parent, string childName)
    {
        if (parent.name == childName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDescendant(parent.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static void NormalizeFillRect(RectTransform fillRect)
    {
        if (fillRect.pivot.x != 0f)
            fillRect.pivot = new Vector2(0f, fillRect.pivot.y);
    }

    private void SetFillWidth(RectTransform fillRect, Vector2 baseSize, float anchoredX, float normalized)
    {
        float widthFactor = Mathf.Lerp(BAR_FILL_MIN, BAR_FILL_MAX, Mathf.Clamp01(normalized));
        float targetWidth = baseSize.x * widthFactor;
        NormalizeFillRect(fillRect);
        fillRect.sizeDelta = new Vector2(targetWidth, baseSize.y);
        fillRect.anchoredPosition = new Vector2(anchoredX, fillRect.anchoredPosition.y);
    }
}
