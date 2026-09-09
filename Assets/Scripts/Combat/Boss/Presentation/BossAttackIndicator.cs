using System.Collections;
using UnityEngine;

/// <summary>
/// Runtime attack warning with rectangular or circular sizing and timed warning/imminent phases.
/// This component only draws the warning; attack routines own damage and normal object cleanup.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BossAttackIndicator : MonoBehaviour
{

    /// <summary>
    /// Visual lifecycle of a warning. Finished indicators are hidden until their owner destroys them.
    /// </summary>
    public enum Phase
    {
        Idle,
        Warning,
        Imminent,
        Finished,
    }

    /// <summary>
    /// Sizing mode used to restore dimensions when switching between warning sprites.
    /// </summary>
    public enum Shape
    {
        /// <summary>Tiled rectangle; <c>size</c> is width/height in world units and <c>angleDegrees</c> rotates it.</summary>
        Rect,
        /// <summary>Circle; <c>size.x</c> is the radius in world units and <c>angleDegrees</c> is ignored.</summary>
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

    /// <summary>
    /// The current visual phase, read by the laser routine when advancing warning rows.
    /// </summary>
    public Phase CurrentPhase => _phase;

    /// <summary>
    /// Hides or shows a warning without resetting its absolute start time or destroying the object.
    /// </summary>
    public void SetVisualEnabled(bool enabled)
    {
        _visualEnabled = enabled;
        if (_renderer) _renderer.enabled = enabled;
    }

    /// <summary>
    /// Caches the renderer required by both warning shapes.
    /// </summary>
    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// Starts a warning of the given <paramref name="shape"/>. Center is in world units. For rectangles
    /// <paramref name="size"/> is width/height and <paramref name="angleDegrees"/> rotates about Z; for circles
    /// only <c>size.x</c> is used as the radius.
    /// </summary>
    public void Begin(Shape shape, Sprite baseSprite, Sprite imminentSprite, Color baseColor, Color imminentColor,
        Vector2 center, Vector2 size, float angleDegrees,
        float duration, float imminentFraction, float pulseSpeed,
        int sortingOrder, int sortingLayerId)
    {
        _shape = shape;
        transform.position = new Vector3(center.x, center.y, transform.position.z);
        transform.localScale = Vector3.one;

        if (shape == Shape.Rect)
        {
            _rectSize = new Vector2(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y));
            _rectAngleDeg = angleDegrees;
            transform.rotation = Quaternion.Euler(0f, 0f, angleDegrees);
        }
        else
        {
            _circleRadius = Mathf.Max(0.05f, size.x);
            transform.rotation = Quaternion.identity;
        }

        ConfigureCommon(baseSprite, imminentSprite, baseColor, imminentColor,
            duration, imminentFraction, pulseSpeed, sortingOrder, sortingLayerId);

        if (shape == Shape.Rect) ApplyRectSize(_rectSize);
        else ApplyCircleRadius(_circleRadius);
    }

    /// <summary>
    /// Starts the shared timer and styling; imminentFraction is the final fraction of the warning duration.
    /// </summary>
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

    /// <summary>
    /// Uses renderer tiling instead of Transform scaling so rectangle dimensions survive sprite changes.
    /// </summary>
    private void ApplyRectSize(Vector2 size)
    {
        if (!_renderer || _renderer.sprite == null) return;
        _renderer.drawMode = SpriteDrawMode.Tiled;
        _renderer.tileMode = SpriteTileMode.Continuous;
        _renderer.size = size;
        transform.localScale = Vector3.one;
    }

    /// <summary>
    /// Scales the current sprite from its natural width to the requested world diameter.
    /// </summary>
    private void ApplyCircleRadius(float radius)
    {
        if (!_renderer || _renderer.sprite == null) return;
        _renderer.drawMode = SpriteDrawMode.Simple;
        float naturalWidth = Mathf.Max(0.01f, _renderer.sprite.bounds.size.x);
        float scale = (radius * 2f) / naturalWidth;
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>
    /// Moves a tracked warning while preserving its render depth.
    /// </summary>
    public void UpdateCenter(Vector2 center)
    {
        transform.position = new Vector3(center.x, center.y, transform.position.z);
    }

    /// <summary>
    /// Updates a rectangular laser row's geometry without restarting its warning timer.
    /// </summary>
    public void UpdateRect(Vector2 center, Vector2 size, float angleDegrees)
    {
        if (_shape != Shape.Rect) return;
        _rectSize = new Vector2(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y));
        _rectAngleDeg = angleDegrees;
        transform.position = new Vector3(center.x, center.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0f, 0f, angleDegrees);
        ApplyRectSize(_rectSize);
    }

    /// <summary>
    /// Hands alpha ownership to one explicit fade, preventing the normal pulse from overwriting it.
    /// </summary>
    public void FadeOutAndDestroy(float duration)
    {
        if (_externalFade) return;
        _externalFade = true;
        _running = false;
        StartCoroutine(FadeOutRoutine(duration));
    }

    /// <summary>
    /// Fades the current alpha over scaled time, then releases the warning object.
    /// </summary>
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
            Color c = c0;
            c.a = Mathf.Lerp(c0.a, 0f, u);
            _renderer.color = c;
            yield return null;
        }
        Destroy(gameObject);
    }

    /// <summary>
    /// Immediately switches to imminent styling; callers remain responsible for the warning's timer and cleanup.
    /// </summary>
    public void ForceImminent()
    {
        if (_phase == Phase.Imminent) return;
        _phase = Phase.Imminent;
        if (_renderer && _imminentSprite) _renderer.sprite = _imminentSprite;
        if (_shape == Shape.Rect) ApplyRectSize(_rectSize);
        else ApplyCircleRadius(_circleRadius);
    }

    /// <summary>
    /// Finishes and hides the warning; the attack owner still owns its GameObject.
    /// </summary>
    public void Complete()
    {
        _running = false;
        _phase = Phase.Finished;
        if (_renderer) _renderer.enabled = false;
    }

    /// <summary>
    /// Stops visual playback when the warning object is disabled.
    /// </summary>
    private void OnDisable()
    {
        Complete();
    }

    /// <summary>
    /// Derives visual phase from elapsed scaled time, pulses opacity and size, and hides expired warnings.
    /// </summary>
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
