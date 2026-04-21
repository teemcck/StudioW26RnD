using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class AppliedUpgradeStripUI : MonoBehaviour
{
<<<<<<< Updated upstream
    [SerializeField] private RectTransform stripRoot;
    [SerializeField] private GameObject iconPrefab;
    [SerializeField] private GameObject overflowChipPrefab;
    [SerializeField] private Vector2 iconSize = new(72f, 100f);
=======
    [Header("Layout")]
    [SerializeField] private Vector2 anchorMin = new(1f, 1f);
    [SerializeField] private Vector2 anchorMax = new(1f, 1f);
    [SerializeField] private Vector2 pivot = new(1f, 1f);
    [SerializeField] private Vector2 anchoredPosition = new(-28f, -24f);
    [SerializeField] private Vector2 iconSize = new(140f, 196f);
    [SerializeField] private float spacing = 30f;
    [SerializeField] private float maxScreenWidthFraction = 0.5f;

    [Header("Visual")]
>>>>>>> Stashed changes
    [SerializeField] private Color iconTint = Color.white;
    [SerializeField] private float overflowFractionOfScreen = 0.6f;
    [SerializeField] private bool showOnlyDuringGameplay = true;

    private readonly List<GameObject> _spawnedIcons = new();
<<<<<<< Updated upstream
=======

    private Canvas _hudCanvas;
    private RectTransform _stripRoot;
>>>>>>> Stashed changes
    private IEventBinding<UpgradeSelectedEvent> _upgradeSelectedBinding;

    private void OnEnable()
    {
        _upgradeSelectedBinding = EventBus<UpgradeSelectedEvent>.Register(OnUpgradeSelected);
        RebuildIcons();
    }

    private void OnDisable()
    {
        EventBus<UpgradeSelectedEvent>.Unsubscribe(_upgradeSelectedBinding);
    }

    private void LateUpdate()
    {
<<<<<<< Updated upstream
        if (!showOnlyDuringGameplay || stripRoot == null)
            return;

        bool visible = GameplayHandler.Instance != null &&
                       GameplayHandler.Instance.CurrentState == GameplayHandler.FloorState.Playing;
        if (stripRoot.gameObject.activeSelf != visible)
            stripRoot.gameObject.SetActive(visible);
=======
        if (_stripRoot == null)
            return;

        if (showOnlyDuringGameplay)
            _stripRoot.gameObject.SetActive(ShouldShowHud());

        RefreshLayout();
>>>>>>> Stashed changes
    }

    private void OnUpgradeSelected(UpgradeSelectedEvent evt)
    {
        RebuildIcons();
    }

<<<<<<< Updated upstream
=======
    private void EnsureUi()
    {
        if (_hudCanvas == null)
            _hudCanvas = GetComponentInChildren<Canvas>(true);

        if (_hudCanvas == null || _stripRoot != null)
            return;

        var root = new GameObject("AppliedUpgradeStrip", typeof(RectTransform));
        root.transform.SetParent(_hudCanvas.transform, false);
        _stripRoot = root.GetComponent<RectTransform>();
        _stripRoot.anchorMin = anchorMin;
        _stripRoot.anchorMax = anchorMax;
        _stripRoot.pivot = pivot;
        _stripRoot.anchoredPosition = anchoredPosition;
        _stripRoot.sizeDelta = Vector2.zero;
    }

>>>>>>> Stashed changes
    private void RebuildIcons()
    {
        if (stripRoot == null || iconPrefab == null)
            return;

        ClearIcons();

        if (UpgradeManager.Instance == null)
            return;

        List<UpgradeDisplaySO> applied = UpgradeManager.Instance.GetAppliedUpgradeDisplays();

        float maxWidth = Screen.width * Mathf.Clamp01(overflowFractionOfScreen);
        float perIcon = iconSize.x + 8f;
        int maxVisible = Mathf.Max(1, Mathf.FloorToInt(maxWidth / perIcon));
        int overflowCount = applied.Count > maxVisible ? applied.Count - (maxVisible - 1) : 0;
        int visibleCount = overflowCount > 0 ? maxVisible - 1 : applied.Count;

        for (int i = 0; i < visibleCount; i++)
        {
            var display = applied[i];
            if (display == null || display.cardImage == null)
                continue;

<<<<<<< Updated upstream
            int stacks = UpgradeManager.Instance.GetStack(display.upgradeID);
            SpawnIcon(display, stacks);
        }

        if (overflowCount > 0 && overflowChipPrefab != null)
            SpawnOverflowChip(overflowCount);
    }

    private void SpawnIcon(UpgradeDisplaySO display, int stackCount)
    {
        var iconGo = Instantiate(iconPrefab, stripRoot);
        iconGo.name = $"{display.upgradeID}_Mini";

        var rt = iconGo.GetComponent<RectTransform>();
        if (rt != null)
            rt.sizeDelta = iconSize;

        var layout = iconGo.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.preferredWidth = iconSize.x;
            layout.preferredHeight = iconSize.y;
            layout.minWidth = iconSize.x;
            layout.minHeight = iconSize.y;
        }

        var image = iconGo.GetComponent<Image>();
        if (image != null)
        {
=======
            GameObject iconGo = new GameObject($"{display.upgradeID}_Mini", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(_stripRoot, false);

            RectTransform rt = iconGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = iconSize;

            Image image = iconGo.GetComponent<Image>();
>>>>>>> Stashed changes
            image.sprite = display.cardImage;
            image.preserveAspect = false;
            image.color = iconTint;
            image.raycastTarget = true;
        }

<<<<<<< Updated upstream
        var hover = iconGo.GetComponent<CardHover>() ?? iconGo.AddComponent<CardHover>();
        hover.Configure(display, stackCount);
        hover.ConfigureDetailPlacement(CardHover.DetailAnchor.BelowAnchor, compact: true);

        var badge = iconGo.transform.Find("Badge");
        if (badge != null)
        {
            badge.gameObject.SetActive(stackCount >= 2);
            var badgeText = badge.Find("Text")?.GetComponent<TMP_Text>();
            if (badgeText != null)
                badgeText.text = $"x{stackCount}";
        }

        _spawnedIcons.Add(iconGo);
    }

    private void SpawnOverflowChip(int overflowCount)
    {
        var chipGo = Instantiate(overflowChipPrefab, stripRoot);
        chipGo.name = "OverflowChip";

        var rt = chipGo.GetComponent<RectTransform>();
        if (rt != null)
            rt.sizeDelta = new Vector2(iconSize.x * 0.6f, iconSize.y);

        var layout = chipGo.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.preferredWidth = iconSize.x * 0.6f;
            layout.preferredHeight = iconSize.y;
            layout.minWidth = iconSize.x * 0.6f;
            layout.minHeight = iconSize.y;
        }

        var text = chipGo.transform.Find("Text")?.GetComponent<TMP_Text>();
        if (text != null)
            text.text = $"+{overflowCount}";

        _spawnedIcons.Add(chipGo);
=======
        RefreshLayout();
    }

    private void RefreshLayout()
    {
        if (_stripRoot == null)
            return;

        int count = _spawnedIcons.Count;
        if (count == 0)
        {
            _stripRoot.sizeDelta = Vector2.zero;
            return;
        }

        float fullStep = iconSize.x + spacing;
        float maxWidth = Mathf.Max(iconSize.x, Screen.width * Mathf.Clamp01(maxScreenWidthFraction));
        float step = fullStep;

        if (count > 1)
        {
            float maxStepToFit = (maxWidth - iconSize.x) / (count - 1);
            step = Mathf.Min(fullStep, Mathf.Max(0f, maxStepToFit));
        }

        float totalWidth = iconSize.x + Mathf.Max(0, count - 1) * step;
        _stripRoot.sizeDelta = new Vector2(totalWidth, iconSize.y);

        for (int i = 0; i < count; i++)
        {
            if (_spawnedIcons[i] == null)
                continue;

            RectTransform rt = _spawnedIcons[i].GetComponent<RectTransform>();
            rt.sizeDelta = iconSize;
            rt.anchoredPosition = new Vector2(-i * step, 0f);
            rt.SetSiblingIndex(i);
        }
    }

    private bool ShouldShowHud()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "BossGameplay")
            return true;

        return GameplayHandler.Instance != null &&
               GameplayHandler.Instance.CurrentState == GameplayHandler.FloorState.Playing;
>>>>>>> Stashed changes
    }

    private void ClearIcons()
    {
        foreach (var icon in _spawnedIcons)
        {
            if (icon != null)
                Destroy(icon);
        }

        _spawnedIcons.Clear();
    }
}
