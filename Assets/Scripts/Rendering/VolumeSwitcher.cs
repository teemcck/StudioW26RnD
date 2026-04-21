using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Volume))]
public sealed class VolumeSwitcher : MonoBehaviour
{
    public static VolumeSwitcher Instance { get; private set; }

    [SerializeField] private VolumeProfile worldOneProfile;
    [SerializeField] private VolumeProfile worldTwoProfile;
    [SerializeField] private VolumeProfile bossProfile;
    [SerializeField] private float crossFadeDuration = 0.8f;
    [SerializeField] private float prewarmWeight = 0.0001f;

    private Volume _hostVolume;
    private Volume _worldOneSub;
    private Volume _worldTwoSub;
    private Volume _bossSub;
    private Volume _activeSub;
    private bool _pendingDescendToWorldTwo;

    private IEventBinding<FloorLoadedEvent> _binding;
    private Coroutine _crossFade;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _hostVolume = GetComponent<Volume>();
        _hostVolume.enabled = false;

        _worldOneSub = CreateSubVolume("SubVolume_WorldOne", worldOneProfile);
        _worldTwoSub = CreateSubVolume("SubVolume_WorldTwo", worldTwoProfile);
        _bossSub = CreateSubVolume("SubVolume_Boss", bossProfile);

        StartCoroutine(PrewarmShaders());
    }

    private void OnEnable()
    {
        _binding = EventBus<FloorLoadedEvent>.Register(OnFloorLoaded);
    }

    private void OnDisable()
    {
        if (_binding != null)
            EventBus<FloorLoadedEvent>.Unsubscribe(_binding);
        _binding = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private Volume CreateSubVolume(string name, VolumeProfile profile)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var v = go.AddComponent<Volume>();
        v.isGlobal = _hostVolume.isGlobal;
        v.priority = _hostVolume.priority;
        v.sharedProfile = profile;
        v.weight = 0f;
        return v;
    }

    private IEnumerator PrewarmShaders()
    {
        float w = Mathf.Max(0.00001f, prewarmWeight);
        _worldOneSub.weight = w;
        _worldTwoSub.weight = w;
        _bossSub.weight = w;

        yield return null;
        yield return null;
        yield return null;

        ApplyForCurrentScene(instant: true);
    }

    private void OnFloorLoaded(FloorLoadedEvent evt)
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "BossGameplay")
        {
            FadeTo(_bossSub);
            return;
        }

        WorldBand band = WorldProgression.GetBandForFloor(evt.FloorIndex);
        Volume target = band == WorldBand.WorldTwo ? _worldTwoSub : _worldOneSub;
        FadeTo(target);

        if (_pendingDescendToWorldTwo && band == WorldBand.WorldTwo)
            _pendingDescendToWorldTwo = false;
    }

    private void ApplyForCurrentScene(bool instant)
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Volume target = sceneName == "BossGameplay" ? _bossSub : _worldOneSub;

        if (instant)
            SetInstant(target);
        else
            FadeTo(target);
    }

    private void SetInstant(Volume target)
    {
        if (target == null) return;
        _worldOneSub.weight = target == _worldOneSub ? 1f : 0f;
        _worldTwoSub.weight = target == _worldTwoSub ? 1f : 0f;
        _bossSub.weight = target == _bossSub ? 1f : 0f;
        _activeSub = target;
    }

    private void FadeTo(Volume target)
    {
        if (target == null || target == _activeSub) return;

        if (_crossFade != null) StopCoroutine(_crossFade);
        _crossFade = StartCoroutine(CrossFadeRoutine(target));
    }

    private IEnumerator CrossFadeRoutine(Volume target)
    {
        float dur = Mathf.Max(0.01f, crossFadeDuration);
        float w1Start = _worldOneSub.weight;
        float w2Start = _worldTwoSub.weight;
        float w3Start = _bossSub.weight;

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            _worldOneSub.weight = Mathf.Lerp(w1Start, target == _worldOneSub ? 1f : 0f, u);
            _worldTwoSub.weight = Mathf.Lerp(w2Start, target == _worldTwoSub ? 1f : 0f, u);
            _bossSub.weight = Mathf.Lerp(w3Start, target == _bossSub ? 1f : 0f, u);
            yield return null;
        }

        _worldOneSub.weight = target == _worldOneSub ? 1f : 0f;
        _worldTwoSub.weight = target == _worldTwoSub ? 1f : 0f;
        _bossSub.weight = target == _bossSub ? 1f : 0f;
        _activeSub = target;
        _crossFade = null;
    }

    public void NotifyWorldDescendConfirmed()
    {
        _pendingDescendToWorldTwo = true;
    }
}
