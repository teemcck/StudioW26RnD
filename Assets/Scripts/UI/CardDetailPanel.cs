using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-40)]
public sealed class CardDetailPanel : MonoBehaviour
{
    public static CardDetailPanel Instance { get; private set; }

    [SerializeField] private float fullWidth = 220f;
    [SerializeField] private float compactWidth = 200f;

    private Canvas _canvas;
    private RectTransform _panelRt;
    private CanvasGroup _panelCg;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _rarityText;
    private TextMeshProUGUI _bodyText;

    private RectTransform _currentAnchor;
    private Coroutine _fadeRoutine;
    private UpgradeDisplaySO _currentData;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        AdoptHierarchy();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void AdoptHierarchy()
    {
        _canvas = GetComponentInChildren<Canvas>(true);
        if (_canvas == null) return;

        var panel = _canvas.transform.Find("Panel");
        if (panel == null) return;

        _panelRt = panel.GetComponent<RectTransform>();
        _panelCg = panel.GetComponent<CanvasGroup>();

        var titleT = panel.Find("Title");
        var rarityT = panel.Find("Rarity");
        var bodyT = panel.Find("Body");

        _titleText = titleT != null ? titleT.GetComponent<TextMeshProUGUI>() : null;
        _rarityText = rarityT != null ? rarityT.GetComponent<TextMeshProUGUI>() : null;
        _bodyText = bodyT != null ? bodyT.GetComponent<TextMeshProUGUI>() : null;

        panel.gameObject.SetActive(false);
        if (_panelCg != null)
            _panelCg.alpha = 0f;
    }

    public void Show(RectTransform anchor, UpgradeDisplaySO data, int stackCount = 1, CardHover.DetailAnchor placement = CardHover.DetailAnchor.Auto, bool compact = false)
    {
        if (_panelRt == null || _titleText == null || _rarityText == null || _bodyText == null)
            return;
        if (anchor == null || data == null)
            return;

        _currentAnchor = anchor;

        bool dataChanged = _currentData != data;
        _currentData = data;

        if (dataChanged)
        {
            _titleText.text = data.upgradeName;
            _rarityText.text = $"{data.rarity}" + (stackCount > 1 ? $"  ·  x{stackCount}" : "");
            _rarityText.color = RarityColor(data.rarity);
            _bodyText.text = StatusWordHighlighter.Highlight(data.upgradeDescription ?? string.Empty);
        }

        float w = compact ? compactWidth : fullWidth;
        _panelRt.sizeDelta = new Vector2(w, _panelRt.sizeDelta.y);
        _titleText.rectTransform.sizeDelta = new Vector2(w - 20f, _titleText.rectTransform.sizeDelta.y);
        _rarityText.rectTransform.sizeDelta = new Vector2(w - 20f, _rarityText.rectTransform.sizeDelta.y);
        _bodyText.rectTransform.sizeDelta = new Vector2(w - 20f, _bodyText.rectTransform.sizeDelta.y);

        Canvas.ForceUpdateCanvases();
        float bodyHeight = Mathf.Max(40f, _bodyText.preferredHeight + 10f);
        _bodyText.rectTransform.sizeDelta = new Vector2(w - 20f, bodyHeight);
        _panelRt.sizeDelta = new Vector2(w, 56f + bodyHeight);

        _panelRt.gameObject.SetActive(true);
        PositionRelative(anchor, placement);

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeTo(1f, 0.12f));
    }

    public void Hide(RectTransform anchor)
    {
        if (anchor != null && anchor != _currentAnchor)
            return;

        _currentAnchor = null;
        _currentData = null;
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeTo(0f, 0.08f, disableAtEnd: true));
    }

    public void HideImmediate()
    {
        _currentAnchor = null;
        _currentData = null;
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = null;
        if (_panelCg != null)
            _panelCg.alpha = 0f;
        if (_panelRt != null)
            _panelRt.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_currentAnchor == null && _panelCg != null && _panelCg.alpha > 0f)
            HideImmediate();
    }

    private System.Collections.IEnumerator FadeTo(float target, float duration, bool disableAtEnd = false)
    {
        if (_panelCg == null)
            yield break;

        float start = _panelCg.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _panelCg.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
            yield return null;
        }

        _panelCg.alpha = target;
        if (disableAtEnd && target <= 0f && _panelRt != null)
            _panelRt.gameObject.SetActive(false);
    }

    private void PositionRelative(RectTransform anchor, CardHover.DetailAnchor placement)
    {
        Vector3[] corners = new Vector3[4];
        anchor.GetWorldCorners(corners);

        Vector3 right = (corners[2] + corners[3]) * 0.5f;
        Vector3 left = (corners[0] + corners[1]) * 0.5f;
        Vector3 top = (corners[1] + corners[2]) * 0.5f;
        Vector3 bottom = (corners[0] + corners[3]) * 0.5f;

        Vector2 panelSize = _panelRt.sizeDelta * _canvas.scaleFactor;
        const float gap = 10f;

        Vector2 screenPos;
        Vector2 pivot;

        switch (placement)
        {
            case CardHover.DetailAnchor.BelowAnchor:
                screenPos = RectTransformUtility.WorldToScreenPoint(null, bottom);
                screenPos.y -= gap;
                pivot = new Vector2(0.5f, 1f);
                break;
            case CardHover.DetailAnchor.AboveAnchor:
                screenPos = RectTransformUtility.WorldToScreenPoint(null, top);
                screenPos.y += gap;
                pivot = new Vector2(0.5f, 0f);
                break;
            case CardHover.DetailAnchor.LeftOfAnchor:
                screenPos = RectTransformUtility.WorldToScreenPoint(null, left);
                screenPos.x -= gap;
                pivot = new Vector2(1f, 0.5f);
                break;
            case CardHover.DetailAnchor.RightOfAnchor:
                screenPos = RectTransformUtility.WorldToScreenPoint(null, right);
                screenPos.x += gap;
                pivot = new Vector2(0f, 0.5f);
                break;
            case CardHover.DetailAnchor.Auto:
            default:
                screenPos = RectTransformUtility.WorldToScreenPoint(null, right);
                screenPos.x += gap;
                pivot = new Vector2(0f, 0.5f);
                if (screenPos.x + panelSize.x > Screen.width - 8f)
                {
                    screenPos = RectTransformUtility.WorldToScreenPoint(null, left);
                    screenPos.x -= gap;
                    pivot = new Vector2(1f, 0.5f);
                }
                break;
        }

        _panelRt.pivot = pivot;
        _panelRt.position = new Vector3(screenPos.x, screenPos.y, 0f);
        ClampToScreen();
    }

    private void ClampToScreen()
    {
        Vector3[] panelCorners = new Vector3[4];
        _panelRt.GetWorldCorners(panelCorners);

        float leftX = panelCorners[0].x;
        float rightX = panelCorners[2].x;
        float topY = panelCorners[1].y;
        float bottomY = panelCorners[0].y;

        Vector2 offset = Vector2.zero;
        if (leftX < 8f) offset.x = 8f - leftX;
        else if (rightX > Screen.width - 8f) offset.x = (Screen.width - 8f) - rightX;
        if (bottomY < 8f) offset.y = 8f - bottomY;
        else if (topY > Screen.height - 8f) offset.y = (Screen.height - 8f) - topY;

        _panelRt.position += new Vector3(offset.x, offset.y, 0f);
    }

    private static Color RarityColor(UpgradeRarity rarity)
    {
        return rarity switch
        {
            UpgradeRarity.Common => new Color(0.8f, 0.8f, 0.8f),
            UpgradeRarity.Uncommon => new Color(0.45f, 0.95f, 0.55f),
            UpgradeRarity.Rare => new Color(0.4f, 0.72f, 1f),
            UpgradeRarity.Epic => new Color(0.78f, 0.45f, 1f),
            UpgradeRarity.Legendary => new Color(1f, 0.72f, 0.25f),
            _ => Color.white
        };
    }
}
