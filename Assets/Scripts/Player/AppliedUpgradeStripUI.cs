using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AppliedUpgradeStripUI : MonoBehaviour
{
    [SerializeField] private RectTransform stripRoot;
    [SerializeField] private GameObject iconPrefab;
    [SerializeField] private GameObject overflowChipPrefab;
    [SerializeField] private Vector2 iconSize = new(72f, 100f);
    [SerializeField] private Color iconTint = Color.white;
    [SerializeField] private float overflowFractionOfScreen = 0.6f;
    [SerializeField] private bool showOnlyDuringGameplay = true;

    private readonly List<GameObject> _spawnedIcons = new();
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
        if (!showOnlyDuringGameplay || stripRoot == null)
            return;

        bool visible = GameplayHandler.Instance != null &&
                       GameplayHandler.Instance.CurrentState == GameplayHandler.FloorState.Playing;
        if (stripRoot.gameObject.activeSelf != visible)
            stripRoot.gameObject.SetActive(visible);
    }

    private void OnUpgradeSelected(UpgradeSelectedEvent evt)
    {
        RebuildIcons();
    }

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

        _spawnedIcons.Add(chipGo);
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
