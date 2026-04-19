using UnityEngine;

public class RunStatsTracker : MonoBehaviour
{
    public static RunStatsTracker Instance { get; private set; }

    public int TotalKillsThisRun     { get; private set; }
    public float TotalTimeSeconds    { get; private set; }
    public int TotalXP               { get; private set; }

    private IEventBinding<EnemyKilledEvent>     _killBinding;
    private IEventBinding<FloorCompletedEvent>  _floorBinding;
    private IEventBinding<PlayerDiedEvent>      _deathBinding;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        _killBinding  = EventBus<EnemyKilledEvent>.Register(OnEnemyKilled);
        _floorBinding = EventBus<FloorCompletedEvent>.Register(OnFloorCompleted);
        _deathBinding = EventBus<PlayerDiedEvent>.Register(OnPlayerDied);
    }

    private void OnDisable()
    {
        EventBus<EnemyKilledEvent>.Unsubscribe(_killBinding);
        EventBus<FloorCompletedEvent>.Unsubscribe(_floorBinding);
        EventBus<PlayerDiedEvent>.Unsubscribe(_deathBinding);
    }

    private void Update()
    {
        // Only tracks time while alive.
        // Pause awareness can be added later by listening to GamePausedEvent.
        TotalTimeSeconds += Time.deltaTime;
    }

    private void OnEnemyKilled(EnemyKilledEvent evt) => TotalKillsThisRun++;

    public void AddXP(int amount)
    {
        TotalXP += amount;
        Debug.Log($"XP added: {amount}, Total XP: {TotalXP}");
    }

    private void OnFloorCompleted(FloorCompletedEvent evt)
    {
        Debug.Log($"Floor complete - kills this run: {TotalKillsThisRun}, " +
                  $"time: {TotalTimeSeconds:F1}s, total XP: {TotalXP}");
    }

    private void OnPlayerDied(PlayerDiedEvent evt)
    {
        Debug.Log($"Run ended - kills: {TotalKillsThisRun}, " +
                  $"survived: {evt.SurvivedForSeconds:F1}s");
    }
}
