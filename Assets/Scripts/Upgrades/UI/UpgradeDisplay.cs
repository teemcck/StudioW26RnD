using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UpgradeDisplay : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image cardImage;
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float hoverLerpSpeed = 10f;

    private UpgradeDisplaySO _data;
    private CardFloatAnimation _float;
    private RectTransform _rt;
    private Vector3 _restScale = Vector3.one;
    private Quaternion _restRotation = Quaternion.identity;
    private bool _isHovered;
    private bool _slamming;

    public Action OnClicked { get; set; }
    public UpgradeDisplaySO Data => _data;

    private void Awake()
    {
        _rt = (RectTransform)transform;
        _restScale = _rt.localScale;
        _restRotation = _rt.localRotation;
    }

    public void UpdateDisplay(UpgradeDisplaySO display)
    {
        _data = display;
        cardImage.sprite = _data.cardImage;

        if (_float == null)
            _float = GetComponent<CardFloatAnimation>();
    }

    private void LateUpdate()
    {
        if (_slamming || _rt == null || !_isHovered) return;

        Vector3 targetScale = _restScale * hoverScale;
        Quaternion targetRot = _restRotation;
        float t = 1f - Mathf.Exp(-hoverLerpSpeed * Time.unscaledDeltaTime);
        _rt.localScale = Vector3.Lerp(_rt.localScale, targetScale, t);
        _rt.localRotation = Quaternion.Slerp(_rt.localRotation, targetRot, t);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        if (_float != null) _float.enabled = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;

        if (_rt != null)
        {
            _rt.localScale = _restScale;
            _rt.localRotation = _restRotation;
        }

        if (_float != null) _float.enabled = true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        StartCoroutine(ConfirmSlamAndFire());
    }

    private System.Collections.IEnumerator ConfirmSlamAndFire()
    {
        _slamming = true;
        if (_float != null) _float.enabled = false;

        Vector3 baseScale = _restScale;
        const float dur = 0.18f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            float pulse = 1f + Mathf.Sin(u * Mathf.PI) * 0.08f - (u > 0.6f ? (u - 0.6f) * 0.15f : 0f);
            _rt.localScale = baseScale * pulse;
            yield return null;
        }

        _rt.localScale = baseScale;
        _slamming = false;
        OnClicked?.Invoke();
    }
}
