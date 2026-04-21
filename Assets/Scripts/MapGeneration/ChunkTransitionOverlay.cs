using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-60)]
public sealed class ChunkTransitionOverlay : MonoBehaviour
{
    public static ChunkTransitionOverlay Instance { get; private set; }

    private Canvas _canvas;
    private Image _image;
    private CanvasGroup _cg;

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
        var fade = _canvas.transform.Find("BlackFade");
        if (fade == null) return;
        _image = fade.GetComponent<Image>();
        _cg = fade.GetComponent<CanvasGroup>();
        if (_cg != null) _cg.alpha = 0f;
    }

    public IEnumerator Play(Action midpoint, float fadeInDuration = 0.12f, float fadeOutDuration = 0.18f, float peakAlpha = 0.85f)
    {
        if (_cg == null)
        {
            midpoint?.Invoke();
            yield break;
        }
        yield return FadeTo(peakAlpha, fadeInDuration);
        midpoint?.Invoke();
        yield return FadeTo(0f, fadeOutDuration);
    }

    private IEnumerator FadeTo(float target, float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        float start = _cg.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _cg.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
            yield return null;
        }
        _cg.alpha = target;
    }
}
