using UnityEngine;

[DefaultExecutionOrder(-30)]
public sealed class WorldTwoAmbience : MonoBehaviour
{
    public static WorldTwoAmbience Instance { get; private set; }

    private ParticleSystem _ps;
    private IEventBinding<FloorLoadedEvent> _binding;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _ps = GetComponent<ParticleSystem>() ?? GetComponentInChildren<ParticleSystem>(true);
        if (_ps != null)
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnEnable()
    {
        _binding = EventBus<FloorLoadedEvent>.Register(OnFloorLoaded);
    }

    private void OnDisable()
    {
        if (_binding != null) EventBus<FloorLoadedEvent>.Unsubscribe(_binding);
        _binding = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnFloorLoaded(FloorLoadedEvent evt)
    {
        if (_ps == null) return;

        bool isWorldTwo = WorldProgression.GetBandForFloor(evt.FloorIndex) == WorldBand.WorldTwo;
        if (isWorldTwo)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) transform.position = player.transform.position;
            if (!_ps.isPlaying) _ps.Play();
        }
        else if (_ps.isPlaying)
        {
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void LateUpdate()
    {
        if (_ps == null || !_ps.isPlaying) return;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) transform.position = player.transform.position;
    }
}
