using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-40)]
public sealed class CinematicBars : MonoBehaviour
{
    public static CinematicBars Instance { get; private set; }

    [SerializeField] private float targetBarHeightPixels = 90f;

    private Canvas _canvas;
    private RectTransform _topBar;
    private RectTransform _bottomBar;
    private Coroutine _animRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        AdoptHierarchy();
        SetBarHeight(0f);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void AdoptHierarchy()
    {
        _canvas = GetComponentInChildren<Canvas>(true);
        if (_canvas == null) return;
        _topBar = _canvas.transform.Find("TopBar")?.GetComponent<RectTransform>();
        _bottomBar = _canvas.transform.Find("BottomBar")?.GetComponent<RectTransform>();
    }

    public void Show(float duration = 0.3f)
    {
        if (_topBar == null) return;
        if (_animRoutine != null) StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(AnimateBars(targetBarHeightPixels, duration));
    }

    public void Hide(float duration = 0.25f)
    {
        if (_topBar == null) return;
        if (_animRoutine != null) StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(AnimateBars(0f, duration));
    }

    private IEnumerator AnimateBars(float targetHeight, float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        float startHeight = _topBar.sizeDelta.y;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            SetBarHeight(Mathf.Lerp(startHeight, targetHeight, u));
            yield return null;
        }
        SetBarHeight(targetHeight);
        _animRoutine = null;
    }

    private void SetBarHeight(float height)
    {
        if (_topBar != null) _topBar.sizeDelta = new Vector2(0f, height);
        if (_bottomBar != null) _bottomBar.sizeDelta = new Vector2(0f, height);
    }
}
