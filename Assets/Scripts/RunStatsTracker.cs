using UnityEngine;

public class RunStatsTracker : MonoBehaviour
{
    public static RunStatsTracker Instance { get; private set; }

    public int TotalKillsThisRun     { get; private set; }
    public float TotalTimeSeconds    { get; private set; }
    public int TotalXP               { get; private set; }

    private IEventBinding<EnemyKilledEvent>     _killBinding;
    private IEventBinding<LevelCompletedEvent>  _levelBinding;
    private IEventBinding<PlayerDiedEvent>      _deathBinding;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        _killBinding  = EventBus<EnemyKilledEvent>.Register(OnEnemyKilled);
        _levelBinding = EventBus<LevelCompletedEvent>.Register(OnLevelCompleted);
        _deathBinding = EventBus<PlayerDiedEvent>.Register(OnPlayerDied);
    }

    private void OnDisable()
    {
        EventBus<EnemyKilledEvent>.Unsubscribe(_killBinding);
        EventBus<LevelCompletedEvent>.Unsubscribe(_levelBinding);
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

    private void OnLevelCompleted(LevelCompletedEvent evt)
    {
        Debug.Log($"Level complete - kills this run: {TotalKillsThisRun}, " +
                  $"time: {TotalTimeSeconds:F1}s, total XP: {TotalXP}");
    }

    private void OnPlayerDied(PlayerDiedEvent evt)
    {
        Debug.Log($"Run ended - kills: {TotalKillsThisRun}, " +
                  $"survived: {evt.SurvivedForSeconds:F1}s");
    }
}