using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class GameplayHandler : MonoBehaviour
{
    public static GameplayHandler Instance { get; private set; }

    [Header("References")]
    [SerializeField] private MapSpawner mapSpawner;
    [SerializeField] private LevelUI levelUI;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private CinemachineCamera camera;

    [Header("Difficulty Settings")]
    [SerializeField] private int minDifficulty = 1;
    [SerializeField] private int maxDifficulty = 5;

    [Header("XP Settings")]
    [SerializeField] private int baseXP = 100;
    [SerializeField] private float killXPMultiplier = 1.5f;
    [SerializeField] private float avoidXPMultiplier = 0.5f;
    [SerializeField] private float timeBonusMax = 50f;
    [SerializeField] private float timeBonusWindow = 60f;

    private GameObject _playerObject;
    private int _currentDifficulty;
    private int _currentChunkCount;
    private int _totalEnemies;
    private int _enemiesKilled;
    private float _levelStartTime;
    private int _levelIndex = 0;
    private List<GameObject> _currentChunks;

    // Pre-rolled values for the upcoming level, shown on the preview screen.
    private int _nextDifficulty;
    private int _nextChunkCount;

    private IEventBinding<EnemyKilledEvent> _enemyKilledBinding;
    private IEventBinding<PlayerReachedEndpointEvent> _endpointBinding;

    public enum LevelState { Idle, Preview, Playing, LevelEnd, Reward }
    public LevelState CurrentState { get; private set; } = LevelState.Idle;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        // Instantiate player.
        _playerObject = Instantiate(playerPrefab);
        // Force camera to track player.
        ChangeCameraTracking(_playerObject.transform);
    }

    private void OnEnable()
    {
        _enemyKilledBinding = EventBus<EnemyKilledEvent>.Register(OnEnemyKilled);
        _endpointBinding    = EventBus<PlayerReachedEndpointEvent>.Register(OnPlayerReachedEndpoint);
    }

    private void OnDisable()
    {
        EventBus<EnemyKilledEvent>.Unsubscribe(_enemyKilledBinding);
        EventBus<PlayerReachedEndpointEvent>.Unsubscribe(_endpointBinding);
    }

    private void Start()
    {
        levelUI.Activate();
        RollNextLevel();

        StartCoroutine(LevelSequence());
    }

    // Core loop.

    private IEnumerator LevelSequence()
    {
        // Preview: show rolled difficulty and length before generating.
        CurrentState = LevelState.Preview;

        levelUI.ShowLevelPreview(_nextDifficulty, _nextChunkCount, _levelIndex);
        yield return new WaitUntil(() => levelUI.PlayerConfirmedStart);

        // Commit the pre-rolled values.
        _currentDifficulty = _nextDifficulty;
        _currentChunkCount = _nextChunkCount;

        // Generate and load level.
        _currentChunks = mapSpawner.GenerateSequence(_currentDifficulty);

        EventBus<LevelLoadedEvent>.Raise(new LevelLoadedEvent
        {
            LevelIndex   = _levelIndex,
            IsFirstLevel = _levelIndex == 0
        });

        // Playing.
        CurrentState = LevelState.Playing;
        _enemiesKilled = 0;
        _totalEnemies  = 0; // EnemySpawner will set this once implemented.
        _levelStartTime = Time.time;

        // Move player to the start of the level.
        _playerObject.transform.position = mapSpawner.SpawnPosition;

        yield return new WaitUntil(() => CurrentState == LevelState.LevelEnd);

        // XP Summary.
        float elapsed = Time.time - _levelStartTime;
        int xp = CalculateXP(_enemiesKilled, _totalEnemies, elapsed);

        EventBus<LevelCompletedEvent>.Raise(new LevelCompletedEvent
        {
            LevelLength           = _currentChunks.Count,
            LevelDifficulty       = _currentDifficulty,
            CompletionTimeSeconds = elapsed
        });

        levelUI.ShowXPSummary(_enemiesKilled, _totalEnemies, xp);
        yield return new WaitUntil(() => levelUI.SummaryConfirmed);

        // Reward.
        CurrentState = LevelState.Reward;
        _levelIndex++;

        EventBus<UpgradeScreenOpenedEvent>.Raise(new UpgradeScreenOpenedEvent
        {
            OfferedCount = 3
        });

        UpgradeManager.Instance.OpenUpgradeSelection(3);
        yield return new WaitUntil(() => levelUI.RewardConfirmed);

        // Roll next level and loop.
        RollNextLevel();
        StartCoroutine(LevelSequence());
    }

    // Helpers.

    /// <summary>
    /// Rolls difficulty and chunk count for the upcoming level.
    /// Done before the preview screen so the player sees real values.
    /// </summary>
    private void RollNextLevel()
    {
        _nextDifficulty  = Random.Range(minDifficulty, maxDifficulty + 1);
        _nextChunkCount  = Random.Range(mapSpawner.MinNumChunks, mapSpawner.MaxNumChunks + 1);
    }

    private int CalculateXP(int killed, int total, float elapsed)
    {
        float killXP    = killed * baseXP * killXPMultiplier;
        float avoidXP   = (total - killed) * baseXP * avoidXPMultiplier;
        float timeBonus = Mathf.Lerp(timeBonusMax, 0f, elapsed / timeBonusWindow);

        int totalXP = Mathf.RoundToInt(killXP + avoidXP + timeBonus);
        Debug.Log($"XP - Kills: {killXP}, Avoided: {avoidXP}, Time bonus: {timeBonus}, Total: {totalXP}");
        return totalXP;
    }

    private void ChangeCameraTracking(Transform newTracking)
    {
        camera.Follow = newTracking;
    }

    // Event handlers.

    private void OnEnemyKilled(EnemyKilledEvent evt)
    {
        if (CurrentState != LevelState.Playing) return;
        _enemiesKilled++;
    }

    private void OnPlayerReachedEndpoint(PlayerReachedEndpointEvent evt)
    {
        if (CurrentState != LevelState.Playing) return;
        CurrentState = LevelState.LevelEnd;
    }
}