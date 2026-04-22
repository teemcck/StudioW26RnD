using UnityEngine;

public class RunStatsTracker : MonoBehaviour
{
    public static RunStatsTracker Instance { get; private set; }

    public int TotalKillsThisRun     { get; private set; }
    public float TotalTimeSeconds    { get; private set; }
    public int TotalXP               { get; private set; }
    // HUD XP bar divisor override when GameplayHandler is not loaded (e.g. boss scene).
    public int XpPerFloorHudOverride { get; private set; }

    public int HighestFloorIndex     { get; private set; } = -1;
    public float BiggestHit          { get; private set; }

    private IEventBinding<EnemyKilledEvent>  _killBinding;
    private IEventBinding<FloorLoadedEvent>  _floorLoadedBinding;
    private IEventBinding<EnemyDamagedEvent> _enemyDamagedBinding;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        _killBinding = EventBus<EnemyKilledEvent>.Register(OnEnemyKilled);
        _floorLoadedBinding = EventBus<FloorLoadedEvent>.Register(OnFloorLoaded);
        _enemyDamagedBinding = EventBus<EnemyDamagedEvent>.Register(OnEnemyDamaged);
    }

    private void OnDisable()
    {
        EventBus<EnemyKilledEvent>.Unsubscribe(_killBinding);
        EventBus<FloorLoadedEvent>.Unsubscribe(_floorLoadedBinding);
        EventBus<EnemyDamagedEvent>.Unsubscribe(_enemyDamagedBinding);
    }

    private void OnFloorLoaded(FloorLoadedEvent evt)
    {
        if (evt.FloorIndex > HighestFloorIndex)
            HighestFloorIndex = evt.FloorIndex;
    }

    private void OnEnemyDamaged(EnemyDamagedEvent evt)
    {
        if (!evt.Context.WasCausedByPlayer)
            return;
        if (evt.DamageDealt > BiggestHit)
            BiggestHit = evt.DamageDealt;
    }

    private void Update()
    {
        TotalTimeSeconds += Time.deltaTime;
    }

    private void OnEnemyKilled(EnemyKilledEvent evt)
    {
        if (evt.CountsTowardEnemyStats)
            TotalKillsThisRun++;
    }

    public void AddXP(int amount)
    {
        TotalXP += amount;
    }

    public void SetXpPerFloorHudOverride(int value)
    {
        XpPerFloorHudOverride = Mathf.Max(1, value);
    }

    public void ClearXpPerFloorHudOverride()
    {
        XpPerFloorHudOverride = 0;
    }

    public void ResetRunStats()
    {
        TotalKillsThisRun = 0;
        TotalTimeSeconds = 0f;
        TotalXP = 0;
        ClearXpPerFloorHudOverride();
    }
}
