using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class AppliedUpgradeStripUI : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private Vector2 anchorMin = new(1f, 1f);
    [SerializeField] private Vector2 anchorMax = new(1f, 1f);
    [SerializeField] private Vector2 pivot = new(1f, 1f);
    [SerializeField] private Vector2 anchoredPosition = new(-28f, -24f);
    [SerializeField] private Vector2 iconSize = new(140f, 196f);
    [SerializeField] private float spacing = 12f;

    [Header("Visual")]
    [SerializeField] private Color iconTint = Color.white;
    [SerializeField] private bool showOnlyDuringGameplay = true;

    private readonly List<GameObject> _spawnedIcons = new();

    private Canvas _hudCanvas;
    private CanvasGroup _canvasGroup;
    private RectTransform _stripRoot;
    private HorizontalLayoutGroup _layoutGroup;

    private IEventBinding<UpgradeSelectedEvent> _upgradeSelectedBinding;

    private void Awake()
    {
        _hudCanvas = GetComponentInChildren<Canvas>(true);
        EnsureUi();
    }

    private void OnEnable()
    {
        EnsureUi();
        _upgradeSelectedBinding = EventBus<UpgradeSelectedEvent>.Register(OnUpgradeSelected);
        RebuildIcons();
    }

    private void OnDisable()
    {
        EventBus<UpgradeSelectedEvent>.Unsubscribe(_upgradeSelectedBinding);
    }

    private void LateUpdate()
    {
        if (!showOnlyDuringGameplay || _stripRoot == null)
            return;

        bool visible = GameplayHandler.Instance != null &&
                       GameplayHandler.Instance.CurrentState == GameplayHandler.FloorState.Playing;
        _stripRoot.gameObject.SetActive(visible);
    }

    private void OnUpgradeSelected(UpgradeSelectedEvent evt)
    {
        RebuildIcons();
    }

    private void EnsureUi()
    {
        if (_hudCanvas == null)
            _hudCanvas = GetComponentInChildren<Canvas>(true);

        if (_hudCanvas == null)
            return;

        _canvasGroup = _hudCanvas.GetComponent<CanvasGroup>() ?? _hudCanvas.gameObject.AddComponent<CanvasGroup>();

        if (_stripRoot != null)
            return;

        var root = new GameObject("AppliedUpgradeStrip", typeof(RectTransform));
        root.transform.SetParent(_hudCanvas.transform, false);
        _stripRoot = root.GetComponent<RectTransform>();
        _stripRoot.anchorMin = anchorMin;
        _stripRoot.anchorMax = anchorMax;
        _stripRoot.pivot = pivot;
        _stripRoot.anchoredPosition = anchoredPosition;
        _stripRoot.sizeDelta = Vector2.zero;

        _layoutGroup = root.AddComponent<HorizontalLayoutGroup>();
        _layoutGroup.childAlignment = TextAnchor.UpperRight;
        _layoutGroup.childControlWidth = false;
        _layoutGroup.childControlHeight = false;
        _layoutGroup.childForceExpandWidth = false;
        _layoutGroup.childForceExpandHeight = false;
        _layoutGroup.spacing = spacing;

        var fitter = root.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void RebuildIcons()
    {
        EnsureUi();
        if (_stripRoot == null)
            return;

        ClearIcons();

        if (UpgradeManager.Instance == null)
            return;

        List<UpgradeDisplaySO> applied = UpgradeManager.Instance.GetAppliedUpgradeDisplays();
        foreach (var display in applied)
        {
            if (display == null || display.cardImage == null)
                continue;

            GameObject iconGo = new GameObject($"{display.upgradeID}_Mini", typeof(RectTransform), typeof(LayoutElement), typeof(Image));
            iconGo.transform.SetParent(_stripRoot, false);

            var rt = iconGo.GetComponent<RectTransform>();
            rt.sizeDelta = iconSize;

            var layout = iconGo.GetComponent<LayoutElement>();
            layout.preferredWidth = iconSize.x;
            layout.preferredHeight = iconSize.y;
            layout.minWidth = iconSize.x;
            layout.minHeight = iconSize.y;

            var image = iconGo.GetComponent<Image>();
            image.sprite = display.cardImage;
            image.preserveAspect = false;
            image.color = iconTint;
            image.raycastTarget = false;

            _spawnedIcons.Add(iconGo);
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
