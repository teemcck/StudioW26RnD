using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class BossAttackIndicator : MonoBehaviour
{
    public enum Phase
    {
        Idle,
        Warning,
        Imminent,
        Finished,
    }

    private enum Shape
    {
        Rect,
        Circle,
    }

    private SpriteRenderer _renderer;
    private Sprite _baseSprite;
    private Sprite _imminentSprite;
    private Color _baseColor;
    private Color _imminentColor;
    private float _duration;
    private float _imminentFraction;
    private float _pulseSpeed;
    private float _startTime;
    private bool _running;
    private Phase _phase;
    private Shape _shape;

    private Vector2 _rectSize;
    private float _rectAngleDeg;

    private float _circleRadius;

    private bool _visualEnabled = true;
    private bool _externalFade;

    public Phase CurrentPhase => _phase;

    public void SetVisualEnabled(bool enabled)
    {
        _visualEnabled = enabled;
        if (_renderer) _renderer.enabled = enabled;
    }

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }

    public void BeginRect(Sprite baseSprite, Sprite imminentSprite, Color baseColor, Color imminentColor,
        Vector2 center, Vector2 size, float angleDegrees,
        float duration, float imminentFraction, float pulseSpeed,
        int sortingOrder, int sortingLayerId)
    {
        _shape = Shape.Rect;
        _rectSize = new Vector2(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y));
        _rectAngleDeg = angleDegrees;
        transform.position = new Vector3(center.x, center.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0f, 0f, angleDegrees);
        transform.localScale = Vector3.one;

        ConfigureCommon(baseSprite, imminentSprite, baseColor, imminentColor,
            duration, imminentFraction, pulseSpeed, sortingOrder, sortingLayerId);

        ApplyRectSize(_rectSize);
    }

    public void BeginCircle(Sprite baseSprite, Sprite imminentSprite, Color baseColor, Color imminentColor,
        Vector2 center, float radius,
        float duration, float imminentFraction, float pulseSpeed,
        int sortingOrder, int sortingLayerId)
    {
        _shape = Shape.Circle;
        _circleRadius = Mathf.Max(0.05f, radius);
        transform.position = new Vector3(center.x, center.y, transform.position.z);
        transform.rotation = Quaternion.identity;

        ConfigureCommon(baseSprite, imminentSprite, baseColor, imminentColor,
            duration, imminentFraction, pulseSpeed, sortingOrder, sortingLayerId);

        ApplyCircleRadius(_circleRadius);
    }

    private void ConfigureCommon(Sprite baseSprite, Sprite imminentSprite, Color baseColor, Color imminentColor,
        float duration, float imminentFraction, float pulseSpeed,
        int sortingOrder, int sortingLayerId)
    {
        if (!_renderer) _renderer = GetComponent<SpriteRenderer>();
        _baseSprite = baseSprite;
        _imminentSprite = imminentSprite ? imminentSprite : baseSprite;
        _baseColor = baseColor;
        _imminentColor = imminentColor;
        _duration = Mathf.Max(0.05f, duration);
        _imminentFraction = Mathf.Clamp01(imminentFraction);
        _pulseSpeed = Mathf.Max(0.25f, pulseSpeed);
        _startTime = Time.time;
        _phase = Phase.Warning;
        _running = true;

        _renderer.sprite = _baseSprite;
        _renderer.color = _baseColor;
        _renderer.sortingOrder = sortingOrder;
        if (sortingLayerId != 0) _renderer.sortingLayerID = sortingLayerId;
    }

    private void ApplyRectSize(Vector2 size)
    {
        if (!_renderer || _renderer.sprite == null) return;
        _renderer.drawMode = SpriteDrawMode.Tiled;
        _renderer.tileMode = SpriteTileMode.Continuous;
        _renderer.size = size;
        transform.localScale = Vector3.one;
    }

    private void ApplyCircleRadius(float radius)
    {
        if (!_renderer || _renderer.sprite == null) return;
        _renderer.drawMode = SpriteDrawMode.Simple;
        float naturalWidth = Mathf.Max(0.01f, _renderer.sprite.bounds.size.x);
        float scale = (radius * 2f) / naturalWidth;
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    public void UpdateCenter(Vector2 center)
    {
        transform.position = new Vector3(center.x, center.y, transform.position.z);
    }

    public void UpdateRect(Vector2 center, Vector2 size, float angleDegrees)
    {
        if (_shape != Shape.Rect) return;
        _rectSize = new Vector2(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y));
        _rectAngleDeg = angleDegrees;
        transform.position = new Vector3(center.x, center.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0f, 0f, angleDegrees);
        ApplyRectSize(_rectSize);
    }

    public void FadeOutAndDestroy(float duration)
    {
        if (_externalFade) return;
        _externalFade = true;
        _running = false;
        StartCoroutine(FadeOutRoutine(duration));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        duration = Mathf.Max(0.02f, duration);
        if (!_renderer) yield break;
        Color c0 = _renderer.color;
        float t = 0f;
        while (t < duration && _renderer)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            var c = c0;
            c.a = Mathf.Lerp(c0.a, 0f, u);
            _renderer.color = c;
            yield return null;
        }
        Destroy(gameObject);
    }

    public void ForceImminent()
    {
        if (_phase == Phase.Imminent) return;
        _phase = Phase.Imminent;
        if (_renderer && _imminentSprite) _renderer.sprite = _imminentSprite;
        if (_shape == Shape.Rect) ApplyRectSize(_rectSize);
        else ApplyCircleRadius(_circleRadius);
    }

    public void Complete()
    {
        _running = false;
        _phase = Phase.Finished;
        if (_renderer) _renderer.enabled = false;
    }

    private void OnDisable()
    {
        Complete();
    }

    private void Update()
    {
        if (_externalFade) return;
        if (!_visualEnabled) return;
        if (!_running || _duration <= 0.01f) return;
        if (!_renderer || !_renderer.sprite) return;

        float elapsed = Time.time - _startTime;
        float normalized = Mathf.Clamp01(elapsed / _duration);
        float imminentStart = 1f - _imminentFraction;

        Phase newPhase = normalized >= imminentStart ? Phase.Imminent : Phase.Warning;
        if (newPhase != _phase)
        {
            _phase = newPhase;
            if (newPhase == Phase.Imminent && _imminentSprite)
                _renderer.sprite = _imminentSprite;
            else if (newPhase == Phase.Warning && _baseSprite)
                _renderer.sprite = _baseSprite;

            if (_shape == Shape.Rect) ApplyRectSize(_rectSize);
            else ApplyCircleRadius(_circleRadius);
        }

        Color baseC = _phase == Phase.Imminent ? _imminentColor : _baseColor;
        float pulse = 0.75f + 0.25f * Mathf.Sin(elapsed * _pulseSpeed);
        Color c = baseC;
        c.a = baseC.a * pulse;
        _renderer.color = c;

        if (_phase == Phase.Imminent)
        {
            float scalePulse = Mathf.Lerp(1f, 1.08f, 0.5f + 0.5f * Mathf.Sin(elapsed * _pulseSpeed * 1.6f));
            if (_shape == Shape.Rect)
            {
                _renderer.size = _rectSize * scalePulse;
            }
            else
            {
                float naturalWidth = Mathf.Max(0.01f, _renderer.sprite.bounds.size.x);
                float scale = (_circleRadius * 2f * scalePulse) / naturalWidth;
                transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        if (normalized >= 1f) Complete();
    }
}
