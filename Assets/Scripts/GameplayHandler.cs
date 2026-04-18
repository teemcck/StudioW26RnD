using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameplayHandler : MonoBehaviour
{
    public static GameplayHandler Instance { get; private set; }

    [Header("References")]
    [SerializeField] private MapSpawner mapSpawner;
    [SerializeField] private LevelUI floorUI;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private CinemachineCamera camera;

    [Header("XP Settings")]
    [SerializeField] private int baseXP = 100;
    [SerializeField] private float killXPMultiplier = 1.5f;
    [SerializeField] private float avoidXPMultiplier = 0.5f;
    [SerializeField] private float timeBonusMax = 50f;
    [SerializeField] private float timeBonusWindow = 60f;
    [SerializeField] private int xpPerFloor = 100;

    [Header("World Progression")]
    [SerializeField] private string bossSceneName = "BossGameplay";
    [SerializeField] private string deathSceneName = "DeathScene";

    private GameObject _playerObject;
    private int _currentDifficulty;
    private int _currentChunkCount;
    private int _totalEnemies;
    private int _enemiesKilled;
    private float _floorStartTime;
    private int _floorIndex = 0;
    private List<GameObject> _currentChunks;
    private bool _hasShownWorldOneIntro;
    private bool _hasShownWorldTwoIntro;

    // Pre-rolled values for the upcoming floor, shown on the preview screen.
    private int _nextDifficulty;
    private int _nextChunkCount;

    private IEventBinding<EnemyKilledEvent> _enemyKilledBinding;
    private IEventBinding<PlayerReachedEndpointEvent> _endpointBinding;
    private IEventBinding<PlayerDiedEvent> _playerDiedBinding;

    public enum FloorState { Idle, Preview, Playing, FloorEnd, Reward }
    public FloorState CurrentState { get; private set; } = FloorState.Idle;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        // Don't instantiate player here - wait for floor start
        // _playerObject = Instantiate(playerPrefab);
        // ChangeCameraTracking(_playerObject.transform);
    }

    private void OnEnable()
    {
        _enemyKilledBinding = EventBus<EnemyKilledEvent>.Register(OnEnemyKilled);
        _endpointBinding    = EventBus<PlayerReachedEndpointEvent>.Register(OnPlayerReachedEndpoint);
        _playerDiedBinding  = EventBus<PlayerDiedEvent>.Register(OnPlayerDied);
    }

    private void OnDisable()
    {
        EventBus<EnemyKilledEvent>.Unsubscribe(_enemyKilledBinding);
        EventBus<PlayerReachedEndpointEvent>.Unsubscribe(_endpointBinding);
        EventBus<PlayerDiedEvent>.Unsubscribe(_playerDiedBinding);
    }

    private void Start()
    {
        floorUI.Activate();
        RollNextFloor();

        StartCoroutine(FloorSequence());
    }

    // Core loop.

    private IEnumerator FloorSequence()
    {
        yield return ShowWorldIntroIfNeeded();

        // Preview: show rolled difficulty and length before generating.
        CurrentState = FloorState.Preview;

        // Ensure player is not visible during preview
        if (_playerObject != null)
        {
            _playerObject.SetActive(false);
        }

        floorUI.ShowFloorPreview(_nextDifficulty, _floorIndex);
        yield return new WaitUntil(() => floorUI.PlayerConfirmedStart);

        // Instantiate player when floor starts
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

        // Generate and load floor.
        _currentChunks = mapSpawner.GenerateRandomSequence(
            _currentDifficulty,
            _currentChunkCount,
            WorldProgression.GetBandForFloor(_floorIndex));
        _totalEnemies  = mapSpawner.LastSpawnedEnemyCount;

        EventBus<FloorLoadedEvent>.Raise(new FloorLoadedEvent
        {
            FloorIndex   = _floorIndex,
            IsFirstFloor = _floorIndex == 0
        });

        // Playing.
        CurrentState = FloorState.Playing;
        _enemiesKilled = 0;
        _floorStartTime = Time.time;

        // Move player to the start of the floor.
        _playerObject.transform.position = mapSpawner.SpawnPosition;

        yield return new WaitUntil(() => CurrentState == FloorState.FloorEnd);

        // Disable player movement but keep active for upgrades
        EnablePlayerMovement(false);
        // Don't deactivate player yet - wait until after upgrades are selected

        // Disable all enemies
        DisableAllEnemies();

        // Deinitialize chunks after floor end (moved from before next floor)
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
        float elapsed = Time.time - _floorStartTime;
        int floorXP = CalculateXP(_enemiesKilled, _totalEnemies, elapsed);

        // Add XP to run total
        RunStatsTracker.Instance.AddXP(floorXP);

        // Show old XP summary first
        floorUI.ShowXPSummary(_enemiesKilled, _totalEnemies, floorXP);
        yield return new WaitUntil(() => floorUI.SummaryConfirmed);

        // Then show XP bar animation
        floorUI.ShowXPBarAnimation(RunStatsTracker.Instance.TotalXP - floorXP, floorXP, _enemiesKilled, _totalEnemies, elapsed);
        yield return new WaitUntil(() => floorUI.XPBarAnimationComplete);

        // After XP bar completes, show upgrades (threshold always met for now)
        CurrentState = FloorState.Reward;
        _floorIndex++;

        // Reset the reward confirmation flag for the new upgrade selection
        floorUI.ResetRewardConfirmed();

        Debug.Log("[GameplayHandler] XP bar animation complete, opening upgrade selection...");

        EventBus<UpgradeScreenOpenedEvent>.Raise(new UpgradeScreenOpenedEvent
        {
            OfferedCount = 3
        });

        UpgradeManager.Instance.OpenUpgradeSelection(3);
        Debug.Log("[GameplayHandler] Waiting for upgrade selection...");
        yield return new WaitUntil(() => 
        {
            if (floorUI.RewardConfirmed)
            {
                Debug.Log("[GameplayHandler] Upgrade selected!");
                return true;
            }
            return false;
        });

        Debug.Log("[GameplayHandler] Rolling next floor and looping...");
        if (WorldProgression.IsBossFloor(_floorIndex))
        {
            SceneManager.LoadScene(bossSceneName);
            yield break;
        }

        RollNextFloor();
        StartCoroutine(FloorSequence());
    }

    // Helpers.

    /// <summary>
    /// Rolls difficulty and chunk count for the upcoming floor.
    /// Done before the preview screen so the player sees real values.
    /// </summary>
    private void RollNextFloor()
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

    public int XPPerFloor => xpPerFloor;

    private IEnumerator ShowWorldIntroIfNeeded()
    {
        if (_floorIndex == 0 && !_hasShownWorldOneIntro)
        {
            _hasShownWorldOneIntro = true;
            floorUI.ShowWorldTransition(
                "World 1: Upper Strata",
                "This place seems unstable. You'd better be quick.",
                "Enter");
            yield return new WaitUntil(() => floorUI.TransitionConfirmed);
        }

        if (WorldProgression.IsWorldTwoTransition(_floorIndex) && !_hasShownWorldTwoIntro)
        {
            _hasShownWorldTwoIntro = true;
            floorUI.ShowWorldTransition(
                "World 2: Lower Strata",
                "You feel an ominous presence. What could be at the bottom?",
                "Descend");
            yield return new WaitUntil(() => floorUI.TransitionConfirmed);
        }
    }

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
        var enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            enemy.gameObject.SetActive(false);
        }
    }

    // Event handlers.

    private void OnEnemyKilled(EnemyKilledEvent evt)
    {
        if (CurrentState != FloorState.Playing) return;
        _enemiesKilled++;
    }

    private void OnPlayerReachedEndpoint(PlayerReachedEndpointEvent evt)
    {
        if (CurrentState != FloorState.Playing) return;
        CurrentState = FloorState.FloorEnd;
    }

    private void OnPlayerDied(PlayerDiedEvent evt)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(deathSceneName);
    }
}
