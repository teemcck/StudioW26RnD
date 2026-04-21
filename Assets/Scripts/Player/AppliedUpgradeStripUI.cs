using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class AppliedUpgradeStripUI : MonoBehaviour
{
    [SerializeField] private RectTransform stripRoot;
    [SerializeField] private GameObject iconPrefab;
    [SerializeField] private GameObject overflowChipPrefab;
    [SerializeField] private Vector2 iconSize = new(72f, 100f);
    [SerializeField] private Color iconTint = Color.white;
    [SerializeField] private float overflowFractionOfScreen = 0.42f;
    [SerializeField] private int maxIconsBeforeOverflowChip = 5;
    [SerializeField] private bool showOnlyDuringGameplay = true;

    private readonly List<GameObject> _spawnedIcons = new();
    private IEventBinding<UpgradeSelectedEvent> _upgradeSelectedBinding;
    private bool _bossCutsceneSuppressStrip;

    public void AssignDefaultPrefabsIfEmpty(GameObject iconPrefabOverride, GameObject overflowChipPrefabOverride)
    {
        if (iconPrefab == null && iconPrefabOverride != null)
            iconPrefab = iconPrefabOverride;
        if (overflowChipPrefab == null && overflowChipPrefabOverride != null)
            overflowChipPrefab = overflowChipPrefabOverride;
        if (stripRoot != null && iconPrefab != null)
            RebuildIcons();
    }

    public void EnsureStripRootUnderCanvas(Canvas hostCanvas)
    {
        if (stripRoot != null || hostCanvas == null)
            return;

        var rootGo = new GameObject("StripRoot", typeof(RectTransform));
        stripRoot = rootGo.GetComponent<RectTransform>();
        stripRoot.SetParent(hostCanvas.transform, false);
        stripRoot.anchorMin = new Vector2(1f, 1f);
        stripRoot.anchorMax = new Vector2(1f, 1f);
        stripRoot.pivot = new Vector2(1f, 1f);
        // Sit just under typical top-right HUD chrome; overflow popout positions below this row.
        stripRoot.anchoredPosition = new Vector2(-28f, -88f);
        stripRoot.sizeDelta = Vector2.zero;
        stripRoot.localScale = Vector3.one;

        var hlg = rootGo.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.UpperRight;
        hlg.spacing = 8f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var fitter = rootGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (iconPrefab != null)
            RebuildIcons();
    }

    public void SetBossCutsceneStripSuppressed(bool suppress)
    {
        _bossCutsceneSuppressStrip = suppress;
    }

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
        if (!showOnlyDuringGameplay || stripRoot == null)
            return;

        bool visible = ShouldShowStrip();
        if (stripRoot.gameObject.activeSelf != visible)
            stripRoot.gameObject.SetActive(visible);
    }

    private bool ShouldShowStrip()
    {
        if (_bossCutsceneSuppressStrip)
            return false;

        if (SceneManager.GetActiveScene().name == "BossGameplay")
            return true;

        return GameplayHandler.Instance != null &&
               GameplayHandler.Instance.CurrentState == GameplayHandler.FloorState.Playing;
    }

    public void RefreshFromManager()
    {
        RebuildIcons();
    }

    public RectTransform StripRoot => stripRoot;

    public Vector2 IconSize => iconSize;

    public float OverflowFractionOfScreen => overflowFractionOfScreen;

    public int MaxIconsBeforeOverflowChip => maxIconsBeforeOverflowChip;

    public GameObject StripIconPrefab => iconPrefab;

    public GameObject StripOverflowChipPrefab => overflowChipPrefab;

    /// <summary>Matches <see cref="RebuildIcons"/> so the overflow panel can mirror strip + overflow tail.</summary>
    public static void ComputeStripVisibility(
        Vector2 iconSize,
        float overflowFractionOfScreen,
        int maxIconsBeforeOverflowChip,
        int appliedCount,
        out int maxVisible,
        out int visibleIconCount,
        out int overflowBadgeCount)
    {
        float maxWidth = Screen.width * Mathf.Clamp01(overflowFractionOfScreen);
        float perIcon = iconSize.x + 8f;
        int byScreen = Mathf.Max(1, Mathf.FloorToInt(maxWidth / perIcon));
        int cap = Mathf.Max(1, maxIconsBeforeOverflowChip);
        maxVisible = Mathf.Min(byScreen, cap);
        int overflowCount = appliedCount > maxVisible ? appliedCount - (maxVisible - 1) : 0;
        visibleIconCount = overflowCount > 0 ? maxVisible - 1 : appliedCount;
        overflowBadgeCount = overflowCount;
    }

    public void OpenOverflowAllListPanel()
    {
        if (stripRoot == null)
            return;

        var canvas = stripRoot.GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        var panel = AppliedUpgradesOverflowListPanel.EnsureOnCanvas(canvas);
        panel?.Show(stripRoot);
    }

    private void OnUpgradeSelected(UpgradeSelectedEvent evt)
    {
        HideOverflowListPanelIfOpen();
        RebuildIcons();
    }

    private void HideOverflowListPanelIfOpen()
    {
        if (stripRoot == null)
            return;
        var canvas = stripRoot.GetComponentInParent<Canvas>();
        if (canvas == null)
            return;
        var panel = canvas.GetComponentInChildren<AppliedUpgradesOverflowListPanel>(true);
        if (panel != null && panel.IsOpen)
            panel.Hide();
    }

    private void RebuildIcons()
    {
        if (stripRoot == null || iconPrefab == null)
            return;

        ClearIcons();

        if (UpgradeManager.Instance == null)
            return;

        List<UpgradeDisplaySO> applied = UpgradeManager.Instance.GetAppliedUpgradeDisplays();

        ComputeStripVisibility(iconSize, overflowFractionOfScreen, maxIconsBeforeOverflowChip, applied.Count,
            out int maxVisible, out int visibleCount, out int overflowCount);

        for (int i = 0; i < visibleCount; i++)
        {
            var display = applied[i];
            if (display == null || display.cardImage == null)
                continue;

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
            image.sprite = display.cardImage;
            image.preserveAspect = false;
            image.color = iconTint;
            image.raycastTarget = true;
        }

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

        var chipImg = chipGo.GetComponent<Image>();
        if (chipImg == null)
        {
            chipImg = chipGo.AddComponent<Image>();
            chipImg.color = new Color(1f, 1f, 1f, 0.06f);
        }
        chipImg.raycastTarget = true;

        var opener = chipGo.GetComponent<OverflowChipOpenAppliedList>();
        if (opener == null)
            opener = chipGo.AddComponent<OverflowChipOpenAppliedList>();
        opener.Setup(this);

        _spawnedIcons.Add(chipGo);
    }

    public sealed class OverflowChipOpenAppliedList : MonoBehaviour, IPointerClickHandler
    {
        private AppliedUpgradeStripUI _host;

        public void Setup(AppliedUpgradeStripUI host) => _host = host;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            if (_host == null || _host.StripRoot == null)
                return;

            var canvas = _host.StripRoot.GetComponentInParent<Canvas>();
            if (canvas == null)
                return;

            var panel = AppliedUpgradesOverflowListPanel.EnsureOnCanvas(canvas);
            if (panel == null)
                return;

            if (panel.IsOpen)
                panel.Hide();
            else
                _host.OpenOverflowAllListPanel();
        }
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
