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

    [Header("XP Settings")]
    [SerializeField] private int baseXP = 100;
    [SerializeField] private float killXPMultiplier = 1.5f;
    [SerializeField] private float avoidXPMultiplier = 0.5f;
    [SerializeField] private float timeBonusMax = 50f;
    [SerializeField] private float timeBonusWindow = 60f;
    [SerializeField] private int xpPerLevel = 100;

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

        // Don't instantiate player here - wait for level start
        // _playerObject = Instantiate(playerPrefab);
        // ChangeCameraTracking(_playerObject.transform);
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

        // Ensure player is not visible during preview
        if (_playerObject != null)
        {
            _playerObject.SetActive(false);
        }

        levelUI.ShowLevelPreview(_nextDifficulty, _nextChunkCount, _levelIndex);
        yield return new WaitUntil(() => levelUI.PlayerConfirmedStart);

        // Instantiate player when level starts
        if (_playerObject == null)
        {
            _playerObject = Instantiate(playerPrefab);
            ChangeCameraTracking(_playerObject.transform);
        }
        else
        {
            // Re-enable player if it was disabled
            _playerObject.SetActive(true);
            EnablePlayerMovement(true);
        }

        // Commit the pre-rolled values.
        _currentDifficulty = _nextDifficulty;
        _currentChunkCount = _nextChunkCount;

        // Generate and load level.
        _currentChunks = mapSpawner.GenerateRandomSequence(_currentDifficulty);
        _totalEnemies  = mapSpawner.LastSpawnedEnemyCount;

        EventBus<LevelLoadedEvent>.Raise(new LevelLoadedEvent
        {
            LevelIndex   = _levelIndex,
            IsFirstLevel = _levelIndex == 0
        });

        // Playing.
        CurrentState = LevelState.Playing;
        _enemiesKilled = 0;
        _levelStartTime = Time.time;

        // Move player to the start of the level.
        _playerObject.transform.position = mapSpawner.SpawnPosition;

        yield return new WaitUntil(() => CurrentState == LevelState.LevelEnd);

        // Disable player movement but keep active for upgrades
        EnablePlayerMovement(false);
        // Don't deactivate player yet - wait until after upgrades are selected

        // Disable all enemies
        DisableAllEnemies();

        // Deinitialize chunks after level end (moved from before next level)
        if (_currentChunks != null)
        {
            foreach (GameObject chunk in _currentChunks)
            {
                if (chunk != null)
                    Destroy(chunk);
            }
            _currentChunks.Clear();
        }

        // XP Summary with animated XP bar
        float elapsed = Time.time - _levelStartTime;
        int levelXP = CalculateXP(_enemiesKilled, _totalEnemies, elapsed);

        // Add XP to run total
        RunStatsTracker.Instance.AddXP(levelXP);

        // Show old XP summary first
        levelUI.ShowXPSummary(_enemiesKilled, _totalEnemies, levelXP);
        yield return new WaitUntil(() => levelUI.SummaryConfirmed);

        // Then show XP bar animation
        levelUI.ShowXPBarAnimation(RunStatsTracker.Instance.TotalXP - levelXP, levelXP, _enemiesKilled, _totalEnemies, elapsed);
        yield return new WaitUntil(() => levelUI.XPBarAnimationComplete);

        // After XP bar completes, show upgrades (threshold always met for now)
        CurrentState = LevelState.Reward;
        _levelIndex++;

        // Reset the reward confirmation flag for the new upgrade selection
        levelUI.ResetRewardConfirmed();

        Debug.Log("[GameplayHandler] XP bar animation complete, opening upgrade selection...");

        EventBus<UpgradeScreenOpenedEvent>.Raise(new UpgradeScreenOpenedEvent
        {
            OfferedCount = 3
        });

        UpgradeManager.Instance.OpenUpgradeSelection(3);
        Debug.Log("[GameplayHandler] Waiting for upgrade selection...");
        yield return new WaitUntil(() => 
        {
            if (levelUI.RewardConfirmed)
            {
                Debug.Log("[GameplayHandler] Upgrade selected!");
                return true;
            }
            return false;
        });

        Debug.Log("[GameplayHandler] Rolling next level and looping...");
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
        _nextDifficulty  = Random.Range(GameConstants.MinDifficulty, GameConstants.MaxDifficulty + 1);
        _nextChunkCount  = Random.Range(GameConstants.MinChunkCount, GameConstants.MaxChunkCount + 1);
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

    public int XPPerLevel => xpPerLevel;

    private void ChangeCameraTracking(Transform newTracking)
    {
        camera.Follow = newTracking;
    }

    private void EnablePlayerMovement(bool enable)
    {
        if (_playerObject != null)
        {
            // Disable/enable player controller
            var playerController = _playerObject.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.enabled = enable;
            }

            // Disable/enable rigidbody
            var rb = _playerObject.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.simulated = enable;
            }
        }
    }

    private void DisableAllEnemies()
    {
        // Find all enemy objects and disable them
        var enemies = FindObjectsOfType<EnemyBase>();
        foreach (var enemy in enemies)
        {
            enemy.gameObject.SetActive(false);
        }
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