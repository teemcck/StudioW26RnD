using System.Collections;
using UnityEngine;

public sealed class Hitstop : MonoBehaviour
{
    public static Hitstop Instance { get; private set; }

    private Coroutine _activeRoutine;
    private int _activePriority = -1;
    private float _activeEndUnscaledTime;
    private float _savedTimeScale = 1f;
    private float _savedFixedDelta = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (_activeRoutine != null)
        {
            Time.timeScale = _savedTimeScale;
            Time.fixedDeltaTime = _savedFixedDelta;
        }
        if (Instance == this) Instance = null;
    }

    public void Freeze(float unscaledSeconds, int priority = 0)
    {
        if (unscaledSeconds <= 0f) return;

        float requestedEnd = Time.unscaledTime + unscaledSeconds;

        if (_activeRoutine != null)
        {
            if (priority < _activePriority) return;
            if (priority == _activePriority && requestedEnd <= _activeEndUnscaledTime) return;

            StopCoroutine(_activeRoutine);
            Time.timeScale = _savedTimeScale;
            Time.fixedDeltaTime = _savedFixedDelta;
        }
        else
        {
            _savedTimeScale = Time.timeScale;
            _savedFixedDelta = Time.fixedDeltaTime;
        }

        _activePriority = priority;
        _activeEndUnscaledTime = requestedEnd;
        _activeRoutine = StartCoroutine(FreezeRoutine(unscaledSeconds));
    }

    public void FreezeFrames(int frames, int priority = 0) => Freeze(frames / 60f, priority);

    private IEnumerator FreezeRoutine(float unscaledSeconds)
    {
        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0f;
        yield return new WaitForSecondsRealtime(unscaledSeconds);
        Time.timeScale = _savedTimeScale;
        Time.fixedDeltaTime = _savedFixedDelta;
        _activeRoutine = null;
        _activePriority = -1;
    }
}
