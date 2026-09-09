using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class GameplayHandler : MonoBehaviour
{
    public static GameplayHandler Instance { get; private set; }
    public static int LastPublishedXpPerLevel { get; private set; } = 1000;
    private static int? s_debugForcedFloorIndex;
    private static bool s_debugPreserveRunState;

    [Header("References")]
    [SerializeField] private MapSpawner mapSpawner;
    [SerializeField] private LevelUI floorUI;
    [SerializeField] private GameObject playerPrefab;
    [FormerlySerializedAs("camera")]
    [SerializeField] private CinemachineCamera cinemachineCamera;

    [Header("XP Settings")]
    [SerializeField] private int baseXP = 100;
    [SerializeField] private float killXPMultiplier = 1.5f;
    [SerializeField] private float avoidXPMultiplier = 0.5f;
    [Tooltip("XP required per player level. Levels are distinct from floors: a floor is a map run, a level is an upgrade threshold.")]
    [FormerlySerializedAs("xpPerLevel")]
    [SerializeField] private int xpPerLevel = 1000;

    [Header("XP — time bonus (speed)")]
    [Tooltip("Seconds per chunk toward par time.")]
    [SerializeField] private float parSecondsPerChunk = 28f;
    [Tooltip("Extra par time at max difficulty.")]
    [SerializeField] private float parStretchAtMaxDifficulty = 0.22f;
    [Tooltip("Cap: time XP vs this floor kill+avoid when fast.")]
    [SerializeField] private float maxTimeBonusAsCombatFraction = 0.24f;
    [Tooltip("Baseline time XP from baseXP.")]
    [SerializeField] private float timeBonusFlatVsBase = 0.55f;
    [Tooltip("Chunk count exponent for flat part.")]
    [SerializeField] private float timeBonusChunkLengthExponent = 0.5f;
    [Tooltip("Flat part multiplier per floor index.")]
    [SerializeField] private float timeBonusPerFloorIndex = 0.08f;
    [Tooltip("Time decay exponent; higher = softer when slow.")]
    [SerializeField] private float timeQualityDecayExponent = 1.1f;

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

    private int _nextDifficulty;
    private int _nextChunkCount;

    private IEventBinding<EnemyKilledEvent> _enemyKilledBinding;
    private IEventBinding<PlayerReachedEndpointEvent> _endpointBinding;
    private IEventBinding<PlayerDiedEvent> _playerDiedBinding;

    public enum FloorState { Idle, Preview, Playing, FloorEnd }
    public FloorState CurrentState { get; private set; } = FloorState.Idle;
    public int CurrentFloorIndex => _floorIndex;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        LastPublishedXpPerLevel = Mathf.Max(1, xpPerLevel);
        ApplyDebugFloorOverrideIfPresent();
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

    private IEnumerator FloorSequence()
    {
        yield return ShowWorldIntroIfNeeded();

        CurrentState = FloorState.Preview;

        if (_playerObject != null)
        {
            _playerObject.SetActive(false);
        }

        floorUI.ShowFloorPreview(_nextDifficulty, _floorIndex);
        yield return new WaitUntil(() => floorUI.PlayerConfirmedStart);

        if (_playerObject == null)
        {
            _playerObject = Instantiate(playerPrefab);
            InitializeNewRunState();
            EnsurePlayerHud();
            EnablePlayerMovement(true);
            ChangeCameraTracking(_playerObject.transform);
        }
        else
        {
            _playerObject.SetActive(true);
            EnsurePlayerHud();
            EnablePlayerMovement(true);
        }

        _currentDifficulty = _nextDifficulty;
        _currentChunkCount = _nextChunkCount;

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

        CurrentState = FloorState.Playing;
        _enemiesKilled = 0;
        _floorStartTime = Time.time;

        _playerObject.transform.position = mapSpawner.SpawnPosition;
        PlayerHealth spawnHealth = _playerObject.GetComponent<PlayerHealth>();
        if (spawnHealth != null)
            spawnHealth.BeginTeleporterArrivalGrace();

        yield return new WaitUntil(() => CurrentState == FloorState.FloorEnd);

        EnablePlayerMovement(false);
        if (_playerObject != null)
            _playerObject.SetActive(false);

        DisableAllEnemies();

        if (_currentChunks != null)
        {
            foreach (GameObject chunk in _currentChunks)
            {
                if (chunk != null)
                    Destroy(chunk);
            }
            _currentChunks.Clear();
        }

        yield return AwardFloorXpAndRewards();

        if (WorldProgression.IsBossFloor(_floorIndex))
        {
            PlayerHealth ph = _playerObject != null ? _playerObject.GetComponent<PlayerHealth>() : null;
            PlayerStats pst = _playerObject != null ? _playerObject.GetComponent<PlayerStats>() : null;
            if (pst != null && ph != null)
                PlayerCombatTransitionState.Capture(pst, ph, Mathf.Max(1, xpPerLevel));

            SceneManager.LoadScene(bossSceneName);
            yield break;
        }

        RollNextFloor();
        StartCoroutine(FloorSequence());
    }


    /// <summary>
    /// Scores the floor that just ended, plays the XP summary and bar animation, then runs one upgrade
    /// selection per level threshold crossed. Advances <see cref="_floorIndex"/> once the summary is confirmed.
    /// </summary>
    private IEnumerator AwardFloorXpAndRewards()
    {
        float elapsed = Time.time - _floorStartTime;
        FloorXPBreakdown xpBreakdown = GetFloorXPBreakdown(_enemiesKilled, _totalEnemies, elapsed);
        int floorXP = xpBreakdown.TotalXP;

        int previousTotalXP = RunStatsTracker.Instance.TotalXP;

        RunStatsTracker.Instance.AddXP(floorXP);

        floorUI.ShowXPSummary(_enemiesKilled, _totalEnemies, elapsed, xpBreakdown);
        yield return new WaitUntil(() => floorUI.SummaryConfirmed);

        floorUI.ShowXPBarAnimation(RunStatsTracker.Instance.TotalXP - floorXP, floorXP, _enemiesKilled, _totalEnemies, elapsed);
        yield return new WaitUntil(() => floorUI.XPBarAnimationComplete);

        _floorIndex++;
        CurrentState = FloorState.Idle;

        int upgradeSelectionsEarned = CountUpgradeThresholdsCrossed(previousTotalXP, RunStatsTracker.Instance.TotalXP);
        if (upgradeSelectionsEarned <= 0)
            yield break;

        if (_playerObject != null)
            _playerObject.SetActive(true);

        for (int i = 0; i < upgradeSelectionsEarned; i++)
        {
            floorUI.ResetRewardConfirmed();

            EventBus<UpgradeScreenOpenedEvent>.Raise(new UpgradeScreenOpenedEvent
            {
                OfferedCount = 3
            });

            UpgradeManager.Instance.OpenUpgradeSelection(3);
            yield return new WaitUntil(() => floorUI.RewardConfirmed);
        }
    }

    private void RollNextFloor()
    {
        int nextFloor = _floorIndex + 1;
        bool preBoss = WorldProgression.IsBossFloor(nextFloor);
        bool preWorldTwo = WorldProgression.IsWorldTwoTransition(nextFloor);
        int bias = (preBoss || preWorldTwo) ? 1 : 0;

        _nextDifficulty = TriangularRoll(GameConstants.MinDifficulty, GameConstants.MaxDifficulty, mode: 3) + bias;
        _nextDifficulty = Mathf.Clamp(_nextDifficulty, GameConstants.MinDifficulty, GameConstants.MaxDifficulty);

        _nextChunkCount = TriangularRoll(GameConstants.MinChunkCount, GameConstants.MaxChunkCount, mode: (GameConstants.MinChunkCount + GameConstants.MaxChunkCount) / 2);
    }

    /// <summary>
    /// Samples an integer from a triangular distribution peaked at <paramref name="mode"/>, so mid values are
    /// rolled more often than the extremes. See https://en.wikipedia.org/wiki/Triangular_distribution
    /// (inverse-CDF sampling from the "Generating random variates" section).
    /// </summary>
    private static int TriangularRoll(int min, int max, int mode)
    {
        int clampedMode = Mathf.Clamp(mode, min, max);
        float u = (Random.value + Random.value) * 0.5f;
        float range = max - min;
        if (range <= 0f)
            return min;

        float modeT = (clampedMode - min) / range;
        float sample;
        if (u < modeT)
            sample = min + Mathf.Sqrt(u * modeT) * range;
        else
            sample = max - Mathf.Sqrt((1f - u) * (1f - modeT)) * range;

        return Mathf.Clamp(Mathf.RoundToInt(sample), min, max);
    }

    public readonly struct FloorXPBreakdown
    {
        public readonly int KillXP;
        public readonly int AvoidXP;
        public readonly int TimeXP;
        public readonly int TotalXP;

        public FloorXPBreakdown(int killXP, int avoidXP, int timeXP, int totalXP)
        {
            KillXP = killXP;
            AvoidXP = avoidXP;
            TimeXP = timeXP;
            TotalXP = totalXP;
        }
    }

    private FloorXPBreakdown GetFloorXPBreakdown(int killed, int total, float elapsed)
    {
        int avoided = Mathf.Max(0, total - killed);
        float killXP  = killed * baseXP * killXPMultiplier;
        float avoidXP = avoided * baseXP * avoidXPMultiplier;
        float combatSub = killXP + avoidXP;

        int chunks = Mathf.Max(1, _currentChunkCount);
        float diffNorm = Mathf.InverseLerp(GameConstants.MinDifficulty, GameConstants.MaxDifficulty, _currentDifficulty);
        float parSeconds = parSecondsPerChunk * chunks * Mathf.Lerp(1f, 1f + parStretchAtMaxDifficulty, diffNorm);
        parSeconds = Mathf.Max(8f, parSeconds);

        float pace = Mathf.Clamp01(elapsed / parSeconds);
        float quality = Mathf.Pow(Mathf.Max(0f, 1f - pace), Mathf.Max(0.01f, timeQualityDecayExponent));

        float sharePool = combatSub * Mathf.Max(0f, maxTimeBonusAsCombatFraction) * quality;

        float lenFactor = Mathf.Pow(chunks, timeBonusChunkLengthExponent);
        float depthMul = 1f + _floorIndex * Mathf.Max(0f, timeBonusPerFloorIndex);
        float flatPool = baseXP * Mathf.Max(0f, timeBonusFlatVsBase) * lenFactor * depthMul * quality;

        float timeBonus = sharePool + flatPool;

        int rk = Mathf.RoundToInt(killXP);
        int ra = Mathf.RoundToInt(avoidXP);
        int rt = Mathf.RoundToInt(timeBonus);
        int totalXP = Mathf.RoundToInt(killXP + avoidXP + timeBonus);

        int drift = totalXP - (rk + ra + rt);
        if (drift != 0)
            rt += drift;

        return new FloorXPBreakdown(rk, ra, rt, totalXP);
    }

    public int XPPerLevel => xpPerLevel;

    public static void DebugJumpToGameplayFloor(int floorIndex, bool preserveRunState = true)
    {
        PlayerCombatTransitionState.Clear();
        s_debugForcedFloorIndex = Mathf.Max(0, floorIndex);
        s_debugPreserveRunState = preserveRunState;
        SceneManager.LoadScene("GameplayLoop");
    }

    public static void DebugJumpToBoss(bool preserveRunState = true)
    {
        PlayerCombatTransitionState.Clear();
        s_debugForcedFloorIndex = WorldProgression.BossFloorIndex;
        s_debugPreserveRunState = preserveRunState;
        SceneManager.LoadScene("BossGameplay");
    }

    private int CountUpgradeThresholdsCrossed(int previousTotalXP, int currentTotalXP)
    {
        int threshold = Mathf.Max(1, xpPerLevel);
        return Mathf.Max(0, (currentTotalXP / threshold) - (previousTotalXP / threshold));
    }

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
        if (newTracking != null)
        {
            CameraLookaheadTarget lookahead = newTracking.GetComponent<CameraLookaheadTarget>();
            if (lookahead == null)
                Debug.LogError("[GameplayHandler] Player prefab is missing CameraLookaheadTarget; camera will follow the root transform.", newTracking);
            else if (lookahead.Anchor != null)
                newTracking = lookahead.Anchor;
        }

        cinemachineCamera.Follow = newTracking;
    }

    private void EnsurePlayerHud()
    {
        if (_playerObject == null)
            return;

        if (!_playerObject.GetComponent<PlayerHudUI>())
        {
            Debug.LogWarning("[GameplayHandler] Player prefab is missing PlayerHudUI. Add it to the prefab and wire the HUD child references there.");
        }
    }

    private void InitializeNewRunState()
    {
        PlayerController playerController = _playerObject ? _playerObject.GetComponent<PlayerController>() : null;
        if (ConsumeDebugPreserveRunState())
            return;

        PlayerCombatTransitionState.Clear();
        UpgradeManager.Instance?.ResetRun(playerController);
        RunStatsTracker.Instance?.ResetRunStats();
        ApplyStartupDebugUpgrades(playerController);
    }

    private static void ApplyStartupDebugUpgrades(PlayerController playerController)
    {
        if (playerController == null || UpgradeManager.Instance == null)
            return;

        foreach (StartupUpgradeDebugState.ConfiguredUpgrade configured in StartupUpgradeDebugState.GetConfiguredUpgrades())
        {
            for (int i = 0; i < configured.StackCount; i++)
                UpgradeManager.Instance.ApplyUpgrade(configured.UpgradeId, playerController);
        }
    }

    private void ApplyDebugFloorOverrideIfPresent()
    {
        if (!s_debugForcedFloorIndex.HasValue)
            return;

        _floorIndex = s_debugForcedFloorIndex.Value;
        _hasShownWorldOneIntro = _floorIndex > 0;
        _hasShownWorldTwoIntro = _floorIndex >= WorldProgression.WorldTwoStartFloorIndex;
        s_debugForcedFloorIndex = null;
    }

    private static bool ConsumeDebugPreserveRunState()
    {
        bool preserve = s_debugPreserveRunState;
        s_debugPreserveRunState = false;
        return preserve;
    }

    private void EnablePlayerMovement(bool enable)
    {
        if (_playerObject != null)
        {
            PlayerController playerController = _playerObject.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.enabled = enable;
            }

            Rigidbody2D rb = _playerObject.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.simulated = enable;
            }

            UnityEngine.InputSystem.PlayerInput playerInput = _playerObject.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null)
            {
                playerInput.enabled = enable;
                if (enable && !string.IsNullOrEmpty(playerInput.defaultActionMap))
                    playerInput.SwitchCurrentActionMap(playerInput.defaultActionMap);
            }
        }
    }

    private void DisableAllEnemies()
    {
        EnemyBase[] enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        foreach (EnemyBase enemy in enemies)
        {
            enemy.gameObject.SetActive(false);
        }
    }

    private void OnEnemyKilled(EnemyKilledEvent evt)
    {
        if (CurrentState != FloorState.Playing) return;
        if (evt.CountsTowardEnemyStats)
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
