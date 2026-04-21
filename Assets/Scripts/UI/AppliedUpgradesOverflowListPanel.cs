using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// “All applied upgrades” popout placed under the upgrade strip (top-right). Single scroll area lists
/// every applied upgrade in a responsive grid (no duplicate of the strip row). Closes via chip, Escape,
/// close button, or pointer leaving the panel (with a short delay so tooltips still work).
/// </summary>
public sealed class AppliedUpgradesOverflowListPanel : MonoBehaviour
{
    public static AppliedUpgradesOverflowListPanel ActiveInstance { get; private set; }

    private const int CurrentLayoutVersion = 4;

    [SerializeField] private float panelMinWidth = 280f;
    [SerializeField] private float panelMaxWidth = 520f;
    [SerializeField] private float panelHeight = 320f;
    [SerializeField] private Color panelBgColor = new(0.08f, 0.08f, 0.11f, 0.96f);
    [SerializeField] private Vector2 gridCellSize = new(72f, 96f);

    private CanvasGroup _rootCg;
    private RectTransform _rootRt;
    private RectTransform _panelRt;
    private RectTransform _scrollViewportRt;
    private ScrollRect _scrollRect;
    private RectTransform _gridContent;
    private GridLayoutGroup _gridLayout;
    private RectTransform _scrollRt;
    private LayoutElement _scrollLayoutElement;
    private int _layoutVersion;
    private bool _built;
    private bool _open;
    private Coroutine _leaveCheckRoutine;

    /// <summary>Hover tooltips use this anchor when the list is open so text sits below the popout.</summary>
    public RectTransform DescriptionAnchor { get; private set; }

    public bool IsOpen => _open;

    public static AppliedUpgradesOverflowListPanel EnsureOnCanvas(Canvas canvas)
    {
        if (canvas == null)
            return null;

        var existing = canvas.GetComponentInChildren<AppliedUpgradesOverflowListPanel>(true);
        if (existing != null)
            return existing;

        var go = new GameObject("AppliedUpgradesOverflowList", typeof(RectTransform), typeof(CanvasGroup), typeof(AppliedUpgradesOverflowListPanel));
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24f, -24f);
        rt.sizeDelta = new Vector2(280f, 10f);
        go.transform.SetAsLastSibling();
        return go.GetComponent<AppliedUpgradesOverflowListPanel>();
    }

    public void Show(Transform hostStripRoot)
    {
        if (hostStripRoot == null)
            return;

        CardDetailPanel.Instance?.HideImmediate();

        var canvas = hostStripRoot.GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        if (!_built || _layoutVersion != CurrentLayoutVersion)
        {
            ClearBuiltUi();
            BuildUi();
        }

        _open = true;
        ActiveInstance = this;
        gameObject.SetActive(true);
        if (_rootCg != null)
        {
            _rootCg.alpha = 1f;
            _rootCg.blocksRaycasts = true;
        }

        transform.SetParent(canvas.transform, false);
        transform.SetAsLastSibling();

        var stripRt = hostStripRoot as RectTransform;
        if (stripRt != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(stripRt);
            _rootRt.anchorMin = _rootRt.anchorMax = new Vector2(1f, 1f);
            _rootRt.pivot = new Vector2(1f, 1f);
            _rootRt.SetParent(stripRt.parent, false);
            float w = Mathf.Clamp(Mathf.Max(panelMinWidth, stripRt.rect.width + 16f), panelMinWidth, panelMaxWidth);
            _rootRt.sizeDelta = new Vector2(w, panelHeight);
            float stripH = Mathf.Max(stripRt.rect.height, stripRt.sizeDelta.y);
            const float gapBelowStrip = 10f;
            _rootRt.anchoredPosition = stripRt.anchoredPosition + new Vector2(0f, -stripH - gapBelowStrip);
            ConfigureGridForPanelWidth(w, hostStripRoot);
        }

        Canvas.ForceUpdateCanvases();
        PopulateFromStrip(hostStripRoot);
        Canvas.ForceUpdateCanvases();
        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;

        if (_leaveCheckRoutine != null)
        {
            StopCoroutine(_leaveCheckRoutine);
            _leaveCheckRoutine = null;
        }
    }

    public void Hide()
    {
        _open = false;
        if (ActiveInstance == this)
            ActiveInstance = null;

        CardDetailPanel.Instance?.HideImmediate();

        if (_leaveCheckRoutine != null)
        {
            StopCoroutine(_leaveCheckRoutine);
            _leaveCheckRoutine = null;
        }

        gameObject.SetActive(false);
        if (_rootCg != null)
        {
            _rootCg.alpha = 0f;
            _rootCg.blocksRaycasts = false;
        }
    }

    private void Awake()
    {
        _rootRt = GetComponent<RectTransform>();
        _panelRt = _rootRt;
        _rootCg = GetComponent<CanvasGroup>();
        if (_rootCg == null)
            _rootCg = gameObject.AddComponent<CanvasGroup>();
    }

    private void OnDestroy()
    {
        if (ActiveInstance == this)
            ActiveInstance = null;
    }

    private void Update()
    {
        if (!_open)
            return;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Hide();
#else
        if (Input.GetKeyDown(KeyCode.Escape))
            Hide();
#endif
    }

    private void ClearBuiltUi()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        _scrollRect = null;
        _gridContent = null;
        _gridLayout = null;
        _scrollViewportRt = null;
        _scrollRt = null;
        _scrollLayoutElement = null;
        DescriptionAnchor = null;
        _built = false;
        _layoutVersion = 0;
    }

    private void BuildUi()
    {
        _built = true;
        _layoutVersion = CurrentLayoutVersion;
        if (_rootRt == null)
            _rootRt = GetComponent<RectTransform>();
        _panelRt = _rootRt;

        var bg = gameObject.GetComponent<Image>();
        if (bg == null)
            bg = gameObject.AddComponent<Image>();
        bg.color = panelBgColor;
        bg.raycastTarget = true;
        gameObject.AddComponent<OverflowPanelPointerRelay>().Host = this;

        var vlg = gameObject.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
            vlg = gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.spacing = 4f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;

        var headerGo = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        headerGo.transform.SetParent(transform, false);
        var headerRt = headerGo.GetComponent<RectTransform>();
        headerGo.GetComponent<LayoutElement>().preferredHeight = 36f;
        var headerH = headerGo.GetComponent<HorizontalLayoutGroup>();
        headerH.padding = new RectOffset(10, 6, 4, 4);
        headerH.childAlignment = TextAnchor.MiddleLeft;
        headerH.childControlWidth = true;
        headerH.childForceExpandWidth = true;

        var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        titleGo.transform.SetParent(headerGo.transform, false);
        var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "Applied upgrades";
        titleTmp.fontSize = 18f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = Color.white;
        titleTmp.alignment = TextAlignmentOptions.Left;
        if (TMP_Settings.defaultFontAsset != null)
            titleTmp.font = TMP_Settings.defaultFontAsset;
        titleGo.GetComponent<LayoutElement>().flexibleWidth = 1f;

        var closeGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        closeGo.transform.SetParent(headerGo.transform, false);
        closeGo.GetComponent<LayoutElement>().preferredWidth = 32f;
        var closeRt = closeGo.GetComponent<RectTransform>();
        closeRt.sizeDelta = new Vector2(32f, 28f);
        var closeImg = closeGo.GetComponent<Image>();
        closeImg.color = new Color(0.22f, 0.22f, 0.26f, 1f);
        closeImg.raycastTarget = true;
        closeGo.GetComponent<Button>().onClick.AddListener(Hide);
        var closeLabelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        closeLabelGo.transform.SetParent(closeGo.transform, false);
        StretchFull(closeLabelGo.GetComponent<RectTransform>());
        var closeLabel = closeLabelGo.GetComponent<TextMeshProUGUI>();
        closeLabel.text = "×";
        closeLabel.fontSize = 22f;
        closeLabel.alignment = TextAlignmentOptions.Center;
        closeLabel.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
            closeLabel.font = TMP_Settings.defaultFontAsset;

        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(LayoutElement));
        scrollGo.transform.SetParent(transform, false);
        _scrollRt = scrollGo.GetComponent<RectTransform>();
        StretchFull(_scrollRt);
        _scrollLayoutElement = scrollGo.GetComponent<LayoutElement>();
        _scrollLayoutElement.flexibleHeight = 1f;
        _scrollLayoutElement.minHeight = 140f;
        _scrollRect = scrollGo.GetComponent<ScrollRect>();

        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        _scrollViewportRt = viewportGo.GetComponent<RectTransform>();
        StretchFull(_scrollViewportRt);
        viewportGo.GetComponent<Image>().color = new Color(0.04f, 0.04f, 0.06f, 0.92f);
        viewportGo.GetComponent<Image>().raycastTarget = true;

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewportGo.transform, false);
        _gridContent = contentGo.GetComponent<RectTransform>();
        _gridContent.anchorMin = new Vector2(1f, 1f);
        _gridContent.anchorMax = new Vector2(1f, 1f);
        _gridContent.pivot = new Vector2(1f, 1f);
        _gridContent.anchoredPosition = Vector2.zero;
        _gridLayout = contentGo.GetComponent<GridLayoutGroup>();
        var grid = _gridLayout;
        grid.cellSize = gridCellSize;
        grid.spacing = new Vector2(8f, 8f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        grid.startCorner = GridLayoutGroup.Corner.UpperRight;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperRight;
        grid.padding = new RectOffset(8, 8, 8, 8);
        var fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _scrollRect.viewport = _scrollViewportRt;
        _scrollRect.content = _gridContent;
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.scrollSensitivity = 28f;

        var descGo = new GameObject("DescAnchor", typeof(RectTransform), typeof(LayoutElement));
        descGo.transform.SetParent(transform, false);
        descGo.GetComponent<LayoutElement>().ignoreLayout = true;
        DescriptionAnchor = descGo.GetComponent<RectTransform>();
        DescriptionAnchor.anchorMin = new Vector2(0f, 0f);
        DescriptionAnchor.anchorMax = new Vector2(1f, 0f);
        DescriptionAnchor.pivot = new Vector2(0.5f, 0f);
        DescriptionAnchor.sizeDelta = new Vector2(0f, 4f);
        DescriptionAnchor.anchoredPosition = Vector2.zero;

        gameObject.SetActive(false);
    }

    /// <summary>Fit column count to panel width using the same icon size as the strip.</summary>
    private void ConfigureGridForPanelWidth(float panelWidth, Transform hostStripRoot)
    {
        if (_gridLayout == null)
            return;

        var stripUi = hostStripRoot != null ? hostStripRoot.GetComponentInParent<AppliedUpgradeStripUI>() : null;
        Vector2 iconSz = stripUi != null ? stripUi.IconSize : gridCellSize;
        float inner = Mathf.Max(160f, panelWidth - 40f);
        float spacing = 8f;
        float cellW = Mathf.Max(48f, iconSz.x);
        float cellH = Mathf.Max(56f, iconSz.y);
        int cols = Mathf.Clamp(Mathf.FloorToInt((inner + spacing) / (cellW + spacing)), 3, 6);
        _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _gridLayout.constraintCount = cols;
        _gridLayout.cellSize = new Vector2(cellW, cellH);
        _gridLayout.spacing = new Vector2(spacing, spacing);
    }

    private void PopulateFromStrip(Transform hostStripRoot)
    {
        if (_gridContent == null || UpgradeManager.Instance == null)
            return;

        for (int i = _gridContent.childCount - 1; i >= 0; i--)
            Destroy(_gridContent.GetChild(i).gameObject);

        Vector2 cellSz = _gridLayout != null ? _gridLayout.cellSize : gridCellSize;

        List<UpgradeDisplaySO> applied = UpgradeManager.Instance.GetAppliedUpgradeDisplays();
        for (int i = 0; i < applied.Count; i++)
        {
            var display = applied[i];
            if (display == null || display.cardImage == null)
                continue;
            int stacks = UpgradeManager.Instance.GetStack(display.upgradeID);
            if (stacks <= 0)
                continue;
            AddCardCellOnly(_gridContent, display, stacks, cellSz);
        }

        if (_scrollLayoutElement != null)
            _scrollLayoutElement.minHeight = 140f;

        if (_scrollRt != null)
            _scrollRt.gameObject.SetActive(true);
    }

    private static void AddCardCellOnly(Transform parent, UpgradeDisplaySO display, int stacks, Vector2 cellSize)
    {
        var cell = new GameObject($"{display.upgradeID}_Cell", typeof(RectTransform), typeof(Image));
        cell.transform.SetParent(parent, false);
        var rt = cell.GetComponent<RectTransform>();
        rt.sizeDelta = cellSize;
        var img = cell.GetComponent<Image>();
        img.sprite = display.cardImage;
        img.preserveAspect = false;
        img.color = Color.white;
        img.raycastTarget = true;

        var hover = cell.AddComponent<CardHover>();
        hover.Configure(display, stacks);
        hover.ConfigureDetailPlacement(CardHover.DetailAnchor.BelowAnchor, compact: true);

        if (stacks >= 2)
        {
            var badge = new GameObject("Badge", typeof(RectTransform), typeof(TextMeshProUGUI));
            badge.transform.SetParent(cell.transform, false);
            var badgeRt = badge.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(1f, 1f);
            badgeRt.anchorMax = new Vector2(1f, 1f);
            badgeRt.pivot = new Vector2(1f, 1f);
            badgeRt.anchoredPosition = new Vector2(-4f, -4f);
            badgeRt.sizeDelta = new Vector2(36f, 22f);
            var badgeTmp = badge.GetComponent<TextMeshProUGUI>();
            badgeTmp.text = $"x{stacks}";
            badgeTmp.fontSize = 14f;
            badgeTmp.fontStyle = FontStyles.Bold;
            badgeTmp.alignment = TextAlignmentOptions.Center;
            badgeTmp.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null)
                badgeTmp.font = TMP_Settings.defaultFontAsset;
        }
    }

    private static void AddCardCellOnly(Transform parent, UpgradeDisplaySO display, int stacks)
    {
        AddCardCellOnly(parent, display, stacks, new Vector2(72f, 96f));
    }

    internal void OnPointerExitPanel()
    {
        if (!_open)
            return;
        if (_leaveCheckRoutine != null)
            StopCoroutine(_leaveCheckRoutine);
        _leaveCheckRoutine = StartCoroutine(CoDelayedLeaveCheck());
    }

    private IEnumerator CoDelayedLeaveCheck()
    {
        yield return new WaitForSecondsRealtime(0.12f);
        _leaveCheckRoutine = null;
        if (!_open)
            yield break;

        Vector2 pos = PointerScreenPos();
        if (_panelRt != null &&
            RectTransformUtility.RectangleContainsScreenPoint(_panelRt, pos, GetCanvasWorldCamera()))
            yield break;

        if (CardDetailPanel.Instance != null && CardDetailPanel.Instance.ContainsScreenPoint(pos))
            yield break;

        Hide();
    }

    private Camera GetCanvasWorldCamera()
    {
        var c = GetComponentInParent<Canvas>();
        return c != null ? c.worldCamera : null;
    }

    private static Vector2 PointerScreenPos()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();
#endif
        return Input.mousePosition;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private sealed class OverflowPanelPointerRelay : MonoBehaviour, UnityEngine.EventSystems.IPointerExitHandler
    {
        public AppliedUpgradesOverflowListPanel Host { get; set; }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            Host?.OnPointerExitPanel();
        }
    }
}
