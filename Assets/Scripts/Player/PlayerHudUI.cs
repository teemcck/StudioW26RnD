using UnityEngine;
using TMPro;

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

    private void Awake()
    {
        _playerHealth = GetComponent<PlayerHealth>();
        _playerStats = GetComponent<PlayerStats>();
        EnsureUi();
        _ = GetComponent<AppliedUpgradeStripUI>() ?? gameObject.AddComponent<AppliedUpgradeStripUI>();

        // Hide HUD initially - will show when gameplay starts
        SetHudVisibility(false);
    }

    private void LateUpdate()
    {
        // Skip updates if player components aren't initialized
        if (_playerHealth == null || _playerStats == null)
            return;

        // Check if we should be visible during gameplay
        bool isGameplayActive = IsGameplayActive();
        SetHudVisibility(isGameplayActive);

        if (isGameplayActive)
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
        int xpThreshold = GameplayHandler.Instance != null ? Mathf.Max(1, GameplayHandler.Instance.XPPerFloor) : 1;
        int displayXp = totalXp % xpThreshold;
        float xpNormalized = (float)displayXp / xpThreshold;
        _displayedXpNormalized = Mathf.Lerp(_displayedXpNormalized, xpNormalized, Time.deltaTime * fillLerpSpeed);

        // Update bar width
        if (xpFillRect)
        {
            SetFillWidth(xpFillRect, _xpFillBaseSize, _xpFillAnchoredX, _displayedXpNormalized);
        }

        // Update text display
        if (xpValueText)
        {
            xpValueText.text = $"XP {displayXp} / {xpThreshold}";
        }
    }

    /// <summary>
    /// Checks if gameplay is currently active (not in preview or between floors).
    /// </summary>
    private bool IsGameplayActive()
    {
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
            int xpThreshold = GameplayHandler.Instance != null ? Mathf.Max(1, GameplayHandler.Instance.XPPerFloor) : 1;
            _displayedXpNormalized = Mathf.Clamp01((float)(totalXp % xpThreshold) / xpThreshold);
            _initializedDisplayedValues = true;
        }

        if (hudCanvas == null)
            Debug.LogWarning("[PlayerHudUI] Missing child Canvas on the player prefab.");

        if (healthFillRect == null || healthValueText == null || xpFillRect == null || xpValueText == null)
            Debug.LogWarning("[PlayerHudUI] Missing HUD references. Expected child objects named HealthFill, HealthText, XPFill, and XPText.");
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
