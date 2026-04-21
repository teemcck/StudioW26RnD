using System.Collections;
using UnityEngine;

public sealed class Hitstop : MonoBehaviour
{
    public static Hitstop Instance { get; private set; }

    /// <summary>Time scales in this range are treated as accidental micro slow-mo (e.g. boss shield break) and reset to 1 after hitstop.</summary>
    private const float AmbiguousSlowMoMax = 0.35f;

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
            float r = _savedTimeScale;
            if (r > 0.02f && r < AmbiguousSlowMoMax)
                r = 1f;
            Time.timeScale = r;
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
            float interruptedRestore = _savedTimeScale;
            if (interruptedRestore > 0.02f && interruptedRestore < AmbiguousSlowMoMax)
                interruptedRestore = 1f;
            Time.timeScale = interruptedRestore;
            Time.fixedDeltaTime = _savedFixedDelta;
        }
        else
        {
            float s = Time.timeScale;
            if (s <= 0.02f || (s > 0.02f && s < AmbiguousSlowMoMax))
                s = 1f;
            _savedTimeScale = s;
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
        float restore = _savedTimeScale;
        if (restore > 0.02f && restore < AmbiguousSlowMoMax)
            restore = 1f;
        Time.timeScale = restore;
        Time.fixedDeltaTime = _savedFixedDelta;
        _activeRoutine = null;
        _activePriority = -1;
    }
}
