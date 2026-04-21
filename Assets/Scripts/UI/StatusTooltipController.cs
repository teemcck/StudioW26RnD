using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-50)]
public sealed class StatusTooltipController : MonoBehaviour
{
    public static StatusTooltipController Instance { get; private set; }

    private Canvas _canvas;
    private RectTransform _panelRt;
    private CanvasGroup _panelCg;
    private Image _accentBar;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _bodyText;
    private string _currentKey;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        AdoptHierarchy();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void AdoptHierarchy()
    {
        _canvas = GetComponentInChildren<Canvas>(true);
        if (_canvas == null) return;

        var panel = _canvas.transform.Find("Panel");
        if (panel == null) return;

        _panelRt = panel.GetComponent<RectTransform>();
        _panelCg = panel.GetComponent<CanvasGroup>();

        _accentBar = panel.Find("Accent")?.GetComponent<Image>();
        _titleText = panel.Find("Title")?.GetComponent<TextMeshProUGUI>();
        _bodyText = panel.Find("Body")?.GetComponent<TextMeshProUGUI>();

        panel.gameObject.SetActive(false);
    }

    public void Show(string key, Vector2 screenPos)
    {
        if (_panelRt == null || _titleText == null || _bodyText == null || _accentBar == null) return;
        if (!StatusEffectCatalog.TryGet(key, out var entry))
        {
            Hide();
            return;
        }

        if (_currentKey != key)
        {
            _currentKey = key;
            _titleText.text = entry.Title;
            _bodyText.text = entry.Body;
            _accentBar.color = entry.Accent;
            _panelRt.sizeDelta = new Vector2(_panelRt.sizeDelta.x, Mathf.Max(80f, _bodyText.preferredHeight + 46f));
        }

        _panelRt.gameObject.SetActive(true);
        _panelCg.alpha = 1f;
        PositionAt(screenPos);
    }

    public void Hide()
    {
        if (_currentKey == null) return;
        _currentKey = null;
        if (_panelCg != null) _panelCg.alpha = 0f;
        if (_panelRt != null) _panelRt.gameObject.SetActive(false);
    }

    private void PositionAt(Vector2 screenPos)
    {
        Vector2 pos = screenPos + new Vector2(14f, -14f);
        Vector2 size = _panelRt.sizeDelta * _canvas.scaleFactor;
        if (pos.x + size.x > Screen.width) pos.x = screenPos.x - size.x - 14f;
        if (pos.y - size.y < 0f) pos.y = size.y + 14f;
        _panelRt.position = new Vector3(pos.x, pos.y, 0f);
    }
}
