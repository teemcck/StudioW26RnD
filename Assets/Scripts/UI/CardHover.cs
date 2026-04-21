using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public sealed class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public enum DetailAnchor { Auto, RightOfAnchor, LeftOfAnchor, BelowAnchor, AboveAnchor }

    [SerializeField] private UpgradeDisplaySO data;
    [SerializeField] private int stackCount = 1;
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float animDuration = 0.12f;
    [SerializeField] private DetailAnchor detailAnchor = DetailAnchor.Auto;
    [SerializeField] private bool compactDetailPanel = false;

    private RectTransform _rt;
    private Vector3 _restScale;
    private Coroutine _animRoutine;
    private bool _hovered;

    public void Configure(UpgradeDisplaySO display, int stack = 1)
    {
        data = display;
        stackCount = Mathf.Max(1, stack);
    }

    public void ConfigureDetailPlacement(DetailAnchor anchor, bool compact = false)
    {
        detailAnchor = anchor;
        compactDetailPanel = compact;
    }

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _restScale = _rt.localScale;
    }

    private void OnEnable()
    {
        if (_rt != null) _restScale = _rt.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (data == null) return;

        _hovered = true;
        RectTransform anchor = _rt;
        DetailAnchor place = detailAnchor;
        if (AppliedUpgradesOverflowListPanel.ActiveInstance is { IsOpen: true } overflow &&
            overflow.DescriptionAnchor != null)
        {
            anchor = overflow.DescriptionAnchor;
            place = DetailAnchor.BelowAnchor;
        }

        CardDetailPanel.Instance?.Show(anchor, data, stackCount, place, compactDetailPanel);

        if (_animRoutine != null) StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(AnimateScaleTo(_restScale * hoverScale));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        CardDetailPanel.Instance?.Hide(_rt);

        if (_animRoutine != null) StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(AnimateScaleTo(_restScale));
    }

    private void OnDisable()
    {
        if (_hovered) CardDetailPanel.Instance?.Hide(_rt);
        _hovered = false;
        if (_rt != null) _rt.localScale = _restScale;
    }

    private void OnDestroy()
    {
        CardDetailPanel.Instance?.Hide(_rt);
    }

    private System.Collections.IEnumerator AnimateScaleTo(Vector3 targetScale)
    {
        Vector3 startScale = _rt.localScale;
        float t = 0f;
        while (t < animDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / animDuration));
            _rt.localScale = Vector3.Lerp(startScale, targetScale, u);
            yield return null;
        }
        _rt.localScale = targetScale;
        _animRoutine = null;
    }
}
