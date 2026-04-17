using System.Collections;
using UnityEngine;

public class DamageFlash : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.08f;

    private Color _originalColor;
    private Coroutine _flashCo;
    private Coroutine _blinkCo;
    private bool _isBlinking;

    private void Awake()
    {
        if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer) _originalColor = spriteRenderer.color;
    }

    public void Play()
    {
        if (!spriteRenderer || !gameObject.activeInHierarchy) return;

        if (_flashCo != null) StopCoroutine(_flashCo);
        _flashCo = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        spriteRenderer.color = flashColor;
        yield return new WaitForSecondsRealtime(flashDuration);
        if (!_isBlinking)
            spriteRenderer.color = _originalColor;
        _flashCo = null;
    }

    public void PlayInvulnerabilityBlink(float duration, int blinkCount = 5, float startDelay = 0f)
    {
        if (!spriteRenderer) return;
        if (duration <= 0f) return;

        if (_blinkCo != null) StopCoroutine(_blinkCo);
        _blinkCo = StartCoroutine(BlinkRoutine(duration, blinkCount, startDelay));
    }

    private IEnumerator BlinkRoutine(float duration, int blinkCount, float startDelay)
    {
        _isBlinking = true;

        if (startDelay > 0f)
            yield return new WaitForSecondsRealtime(startDelay);

        int toggles = Mathf.Max(2, blinkCount * 2);
        float interval = Mathf.Max(0.01f, duration / toggles);
        bool flashPhase = true;

        for (int i = 0; i < toggles; i++)
        {
            spriteRenderer.color = flashPhase ? flashColor : _originalColor;
            flashPhase = !flashPhase;
            yield return new WaitForSecondsRealtime(interval);
        }

        _isBlinking = false;
        spriteRenderer.color = _originalColor;
        _blinkCo = null;
    }
}