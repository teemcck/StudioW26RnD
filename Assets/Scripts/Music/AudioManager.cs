using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class AudioManager : MonoBehaviour
{
    private const float MenuLoopPointSeconds = 91.2f;
    private const float WorldOneIntroDelaySeconds = 6.3f;
    private const float WorldOneLoopPointSeconds = 50.4f;
    private const float WorldTwoIntroDelaySeconds = 0.75f;
    private const float WorldTwoLoopPointSeconds = 44.625f;
    private const double ScheduleLeadSeconds = 1.0;
    private const float ThreatPollInterval = 0.35f;
    private const float DefaultThreatRadius = 18f;
    private const float StemFadeInDurationSeconds = 5f;
    private const float StemFadeOutDurationSeconds = 6f;
    private const float ThreatChangeHoldSeconds = 1.5f;

    public static AudioManager Instance { get; private set; }

    [Header("Source Template")]
    [SerializeField] private AudioSource sourceTemplate;
    [SerializeField] private int initialSfxPoolSize = 10;
    [SerializeField] private float musicVolume = 1f;
    [SerializeField] private float sfxVolume = 1f;
    [SerializeField] private float threatRadiusMultiplier = 1f;
    [SerializeField] private float minimumThreatRadius = DefaultThreatRadius;

    [Header("SFX variety & space")]
    [Tooltip("Half-range for random pitch around 1.0 (e.g. 0.012 ≈ ±1.2%). Reduces machine-gun repetition.")]
    [SerializeField] private float sfxPitchJitterHalfRange = 0.042f;
    [Tooltip("Extra pitch variance for enemy attack one-shots (e.g. 0.03 ≈ ±3%).")]
    [SerializeField] private float enemyAttackSfxPitchHalfRange = 0.05f;
    [Tooltip("Default volume scale when playing UFO attack SFX at a world position.")]
    [SerializeField] [Range(0.15f, 1f)] private float ufoAttackWorldVolumeScale = 0.68f;
    [Tooltip("Default volume scale when playing plague attack SFX at a world position.")]
    [SerializeField] [Range(0.15f, 1f)] private float plagueAttackWorldVolumeScale = 0.7f;
    [Tooltip("Within this horizontal distance from the player, world SFX stay at full relative volume.")]
    [SerializeField] private float worldSfxNearFullDistance = 2f;
    [Tooltip("Beyond this distance, world SFX reach the minimum volume multiplier (still audible).")]
    [SerializeField] private float worldSfxFarBlendDistance = 28f;
    [Tooltip("Floor for distance-based volume (0–1). Nothing goes fully silent.")]
    [Range(0.08f, 0.6f)]
    [SerializeField] private float worldSfxMinVolumeMultiplier = 0.28f;

    [Header("Menu Music")]
    [SerializeField] private AudioClip menuMusic;

    [Header("World 1 Music")]
    [SerializeField] private AudioClip world1Start;
    [SerializeField] private AudioClip world1CalmChords;
    [SerializeField] private AudioClip world1CalmKeys;
    [SerializeField] private AudioClip world1Chords;
    [SerializeField] private AudioClip world1Keys;
    [SerializeField] private AudioClip world1Sidebreak;
    [SerializeField] private AudioClip world1Mutebreak;
    [SerializeField] private AudioClip world1Breakdown;

    [Header("World 2 Music")]
    [SerializeField] private AudioClip world2Start;
    [SerializeField] private AudioClip world2Chords;
    [SerializeField] private AudioClip world2Keys;
    [SerializeField] private AudioClip world2Arp;
    [SerializeField] private AudioClip world2Crazy;
    [SerializeField] private AudioClip world2Sidebreak;
    [SerializeField] private AudioClip world2Breakdown;
    [SerializeField] private AudioClip world2Waah;

    [Header("Death Music")]
    [SerializeField] private AudioClip deathMusic;
    
    [Header("Sound Effects")]
    [SerializeField] private AudioClip playerHitSfx;
    [SerializeField] private AudioClip playerDashSfx;
    [SerializeField] private AudioClip playerMeleeSfx;
    [SerializeField] private AudioClip[] playerMeleeSfxVariants;
    [SerializeField] private AudioClip playerDeathSfx;
    [SerializeField] private AudioClip playerStepSfx;
    [SerializeField] private AudioClip enemyHitSfx;
    [SerializeField] private AudioClip upgradeSelectedSfx;
    [SerializeField] private AudioClip uiButtonSfx;
    [SerializeField] private AudioClip teleporterEnteredSfx;
    [SerializeField] private AudioClip menuHoverSfx;
    [SerializeField] private AudioClip menuStartSfx;
    [SerializeField] private AudioClip plagueAmbientSfx;
    [SerializeField] private AudioClip plagueAttackSfx;
    [SerializeField] private AudioClip[] plagueLaughSfxVariants;
    [SerializeField] private AudioClip ufoAttackSfx;

    [Header("XP Summary UI")]
    [SerializeField] private AudioClip xpSummaryBlockLandSfx;
    [SerializeField] private AudioClip xpSummaryCountTickSfx;
    [SerializeField] private AudioClip xpSummaryTotalCompleteSfx;

    [Header("Game Feel SFX")]
    [SerializeField] private AudioClip perfectDodgeSfx;
    [SerializeField] private AudioClip critHitSfx;
    [SerializeField] private AudioClip eliteSpawnSfx;
    [SerializeField] private AudioClip chunkTransitionSfx;
    [SerializeField] private AudioClip continueReadySfx;

    private readonly List<AudioSource> _sfxPool = new();
    private readonly List<LoopingStem> _activeStems = new();
    private readonly List<AudioLowPassFilter> _musicLowPassFilters = new();

    private Coroutine _duckRoutine;
    private float _duckMultiplier = 1f;
    private Coroutine _lowHpFilterRoutine;
    private float _lowHpFilterTarget = 22000f;
    private const float LowHpFilterMinCutoff = 900f;
    private const float LowHpFilterMaxCutoff = 22000f;

    private Transform _runtimeRoot;
    private AudioSource _introSource;
    private MusicState _currentMusicState = MusicState.None;
    private WorldBand _currentBand = WorldBand.WorldOne;
    private bool _worldTwoPlaybackStarted;
    private int _currentThreatLevel = 1;
    private int _pendingThreatLevel = 1;
    private float _pendingThreatSince = -1f;
    private float _nextThreatPollTime;
    private Transform _playerTransform;

    private IEventBinding<PlayerDamagedEvent> _playerDamagedBinding;
    private IEventBinding<PlayerDashedEvent> _playerDashedBinding;
    private IEventBinding<PlayerMeleeAttackEvent> _playerMeleeBinding;
    private IEventBinding<PlayerDiedEvent> _playerDiedBinding;
    private IEventBinding<EnemyDamagedEvent> _enemyDamagedBinding;
    private IEventBinding<UpgradeSelectedEvent> _upgradeSelectedBinding;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (sourceTemplate == null)
            sourceTemplate = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        sourceTemplate.playOnAwake = false;
        sourceTemplate.loop = false;
        sourceTemplate.Stop();

        EnsureRuntimeRoot();
        EnsureIntroSource();
        WarmSfxPool();
        RegisterEventBindings();

        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplySceneMusic(forceRestart: true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnregisterEventBindings();
        ClearMusic();
    }

    private void Update()
    {
        double now = AudioSettings.dspTime;
        for (int i = 0; i < _activeStems.Count; i++)
            _activeStems[i].Tick(now, ScheduleLeadSeconds, Time.unscaledDeltaTime);

        if (Time.unscaledTime >= _nextThreatPollTime)
        {
            _nextThreatPollTime = Time.unscaledTime + ThreatPollInterval;
            ApplySceneMusic(forceRestart: false);
        }
    }

    public void PlayPlayerHit() => PlaySfx(playerHitSfx);
    public void PlayEnemyHit() => PlaySfx(enemyHitSfx);
    public void PlayUpgradeSelected() => PlaySfx(upgradeSelectedSfx);
    public void PlayUiButton() => PlaySfx(uiButtonSfx);
    public void PlayTeleporterEntered() => PlaySfx(teleporterEnteredSfx);
    public void PlayPlayerDash() => PlaySfx(playerDashSfx);
    public void PlayPlayerMelee() => PlaySfx(GetRandomClip(playerMeleeSfxVariants, playerMeleeSfx));
    public void PlayPlayerDeath() => PlaySfx(playerDeathSfx);
    public void PlayPlayerStep(float volumeScale = 0.8f) => PlaySfx(playerStepSfx, volumeScale);
    public void PlayMenuHover() => PlaySfx(menuHoverSfx);
    public void PlayMenuStart() => PlaySfx(menuStartSfx ? menuStartSfx : uiButtonSfx);
    public void PlayPlagueAmbient(float volumeScale = 0.55f) => PlaySfx(plagueAmbientSfx, volumeScale);
    public void PlayPlagueAttack(float volumeScale = 1f) => PlaySfx(plagueAttackSfx, volumeScale);
    /// <summary>Plague attack at world position: distance falloff, pan, stronger pitch variance, tuned default volume.</summary>
    public void PlayPlagueAttackAt(Vector3 worldPos, float volumeScale = -1f)
    {
        if (volumeScale < 0f)
            volumeScale = plagueAttackWorldVolumeScale;
        PlaySfxAtWorld(plagueAttackSfx, worldPos, volumeScale, enemyAttackSfxPitchHalfRange);
    }

    public void PlayPlagueLaugh(float volumeScale = 0.9f) => PlaySfx(GetRandomClip(plagueLaughSfxVariants, plagueAttackSfx), volumeScale);
    public void PlayUfoAttack(float volumeScale = 0.95f) => PlaySfx(ufoAttackSfx, volumeScale);
    /// <summary>UFO / generic enemy attack at world position: distance falloff, pan, stronger pitch variance.</summary>
    public void PlayUfoAttackAt(Vector3 worldPos, float volumeScale = -1f)
    {
        if (volumeScale < 0f)
            volumeScale = ufoAttackWorldVolumeScale;
        PlaySfxAtWorld(ufoAttackSfx, worldPos, volumeScale, enemyAttackSfxPitchHalfRange);
    }

    public void PlayXpSummaryBlockLand(float volumeScale = 1f) => PlaySfx(xpSummaryBlockLandSfx, volumeScale);

    public void PlayXpSummaryCountTick(float volumeScale = 0.35f, float pitch = 1f) => PlaySfx(xpSummaryCountTickSfx, volumeScale, pitch);

    public void PlayXpSummaryTotalComplete(float volumeScale = 1f) => PlaySfx(xpSummaryTotalCompleteSfx, volumeScale);

    public void PlayPerfectDodge(float volumeScale = 1f) => PlaySfx(perfectDodgeSfx, volumeScale);
    public void PlayCritHit(float volumeScale = 1f) => PlaySfx(critHitSfx, volumeScale);
    public void PlayEliteSpawn(float volumeScale = 1f) => PlaySfx(eliteSpawnSfx, volumeScale);
    public void PlayChunkTransition(float volumeScale = 1f) => PlaySfx(chunkTransitionSfx, volumeScale);
    public void PlayContinueReady(float volumeScale = 0.8f) => PlaySfx(continueReadySfx, volumeScale);

    /// <summary>
    /// Ducks all music stems (and intro) by multiplying their effective volume by
    /// <paramref name="toVolume"/> for <paramref name="seconds"/> unscaled seconds,
    /// then restores. Safe to call overlapping — later calls override earlier ones.
    /// </summary>
    public void DuckMusic(float seconds, float toVolume = 0f)
    {
        if (seconds <= 0f)
            return;

        if (_duckRoutine != null)
            StopCoroutine(_duckRoutine);

        _duckRoutine = StartCoroutine(DuckMusicRoutine(seconds, Mathf.Clamp01(toVolume)));
    }

    private IEnumerator DuckMusicRoutine(float seconds, float toVolume)
    {
        _duckMultiplier = toVolume;
        ApplyDuckMultiplierNow();
        yield return new WaitForSecondsRealtime(seconds);
        _duckMultiplier = 1f;
        ApplyDuckMultiplierNow();
        _duckRoutine = null;
    }

    private void ApplyDuckMultiplierNow()
    {
        for (int i = 0; i < _activeStems.Count; i++)
            _activeStems[i].SetDuckMultiplier(_duckMultiplier);

        if (_introSource != null)
            _introSource.volume = musicVolume * _duckMultiplier;
    }

    /// <summary>
    /// Play a one-shot SFX panned based on its world position relative to the main
    /// camera, giving a soft sense of direction without full 3D spatialization.
    /// Applies slight pitch variation and soft distance falloff from the player (never fully silent).
    /// </summary>
    /// <param name="pitchJitterHalfRange">If &lt; 0, uses <see cref="sfxPitchJitterHalfRange"/>.</param>
    public void PlaySfxAtWorld(AudioClip clip, Vector3 worldPos, float volumeScale = 1f, float pitchJitterHalfRange = -1f)
    {
        if (clip == null)
            return;

        float pan = 0f;
        Camera cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float halfWidth = Mathf.Max(0.01f, cam.orthographicSize * cam.aspect);
            pan = Mathf.Clamp((worldPos.x - cam.transform.position.x) / halfWidth, -1f, 1f) * 0.85f;
        }
        else if (cam != null)
        {
            Vector3 vp = cam.WorldToViewportPoint(worldPos);
            pan = Mathf.Clamp((vp.x - 0.5f) * 2f, -1f, 1f) * 0.85f;
        }

        float distanceMul = EvaluateWorldDistanceVolumeMultiplier(worldPos);
        float pitch = GetMicroPitchMultiplier(pitchJitterHalfRange);
        PlaySfxPooled(clip, Mathf.Clamp01(volumeScale) * distanceMul, pitch, pan);
    }

    /// <summary>
    /// Engages/disengages the low-HP low-pass filter on music stems. When
    /// <paramref name="active"/>, cutoff lerps toward LowHpFilterMinCutoff as t01
    /// approaches 1; when inactive, it ramps back to max.
    /// </summary>
    public void SetLowHpFilter(bool active, float t01)
    {
        float target = active
            ? Mathf.Lerp(LowHpFilterMaxCutoff, LowHpFilterMinCutoff, Mathf.Clamp01(t01))
            : LowHpFilterMaxCutoff;

        _lowHpFilterTarget = target;
        if (_lowHpFilterRoutine == null)
            _lowHpFilterRoutine = StartCoroutine(LowHpFilterRoutine());
    }

    private IEnumerator LowHpFilterRoutine()
    {
        const float rampSpeed = 20000f;

        while (true)
        {
            float current = _musicLowPassFilters.Count > 0 && _musicLowPassFilters[0] != null
                ? _musicLowPassFilters[0].cutoffFrequency
                : LowHpFilterMaxCutoff;

            if (Mathf.Approximately(current, _lowHpFilterTarget))
            {
                if (Mathf.Approximately(_lowHpFilterTarget, LowHpFilterMaxCutoff))
                    break;

                yield return null;
                continue;
            }

            float next = Mathf.MoveTowards(current, _lowHpFilterTarget, rampSpeed * Time.unscaledDeltaTime);
            for (int i = _musicLowPassFilters.Count - 1; i >= 0; i--)
            {
                var f = _musicLowPassFilters[i];
                if (f == null)
                {
                    _musicLowPassFilters.RemoveAt(i);
                    continue;
                }

                f.cutoffFrequency = next;
            }

            yield return null;
        }

        _lowHpFilterRoutine = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive)
            return;

        if (scene.name == "GameplayLoop" || scene.name == "MenuScene" || scene.name == "DeathScene")
            _worldTwoPlaybackStarted = false;

        StartCoroutine(RefreshAfterSceneLoad());
    }

    private IEnumerator RefreshAfterSceneLoad()
    {
        yield return null;
        RegisterEventBindings();
        ApplySceneMusic(forceRestart: true);
    }

    private void RegisterEventBindings()
    {
        UnregisterEventBindings();
        _playerDamagedBinding = EventBus<PlayerDamagedEvent>.Register(OnPlayerDamaged);
        _playerDashedBinding = EventBus<PlayerDashedEvent>.Register(OnPlayerDashed);
        _playerMeleeBinding = EventBus<PlayerMeleeAttackEvent>.Register(OnPlayerMeleeAttack);
        _playerDiedBinding = EventBus<PlayerDiedEvent>.Register(OnPlayerDied);
        _enemyDamagedBinding = EventBus<EnemyDamagedEvent>.Register(OnEnemyDamaged);
        _upgradeSelectedBinding = EventBus<UpgradeSelectedEvent>.Register(OnUpgradeSelected);
    }

    private void UnregisterEventBindings()
    {
        EventBus<PlayerDamagedEvent>.Unsubscribe(_playerDamagedBinding);
        EventBus<PlayerDashedEvent>.Unsubscribe(_playerDashedBinding);
        EventBus<PlayerMeleeAttackEvent>.Unsubscribe(_playerMeleeBinding);
        EventBus<PlayerDiedEvent>.Unsubscribe(_playerDiedBinding);
        EventBus<EnemyDamagedEvent>.Unsubscribe(_enemyDamagedBinding);
        EventBus<UpgradeSelectedEvent>.Unsubscribe(_upgradeSelectedBinding);
    }

    private void OnPlayerDamaged(PlayerDamagedEvent evt) => PlayPlayerHit();
    private void OnPlayerDashed(PlayerDashedEvent evt) => PlayPlayerDash();
    private void OnPlayerMeleeAttack(PlayerMeleeAttackEvent evt) => PlayPlayerMelee();
    private void OnPlayerDied(PlayerDiedEvent evt) => PlayPlayerDeath();

    private void OnEnemyDamaged(EnemyDamagedEvent evt)
    {
        if (evt.Context.IsStatusEffect)
            return;

        PlaySfxAtWorld(enemyHitSfx, evt.Position);
    }

    private void OnUpgradeSelected(UpgradeSelectedEvent evt) => PlayUpgradeSelected();

    private void ApplySceneMusic(bool forceRestart)
    {
        MusicState desiredState = DetermineDesiredMusicState();
        WorldBand desiredBand = DetermineDesiredWorldBand();

        if (desiredState != _currentMusicState || (desiredState == MusicState.Gameplay && desiredBand != _currentBand) || forceRestart)
        {
            _currentMusicState = desiredState;
            _currentBand = desiredBand;
            RestartMusicState(desiredState, desiredBand);
        }

        if (desiredState != MusicState.Gameplay)
            return;

        int threatLevel = CalculateThreatLevel();
        if (forceRestart)
        {
            _currentThreatLevel = threatLevel;
            _pendingThreatLevel = threatLevel;
            _pendingThreatSince = -1f;
            ApplyThreatVolumes(desiredBand, threatLevel);
            return;
        }

        if (threatLevel == _currentThreatLevel)
        {
            _pendingThreatLevel = threatLevel;
            _pendingThreatSince = -1f;
            return;
        }

        if (threatLevel != _pendingThreatLevel)
        {
            _pendingThreatLevel = threatLevel;
            _pendingThreatSince = Time.unscaledTime;
            return;
        }

        if (_pendingThreatSince >= 0f && Time.unscaledTime - _pendingThreatSince >= ThreatChangeHoldSeconds)
        {
            _currentThreatLevel = _pendingThreatLevel;
            _pendingThreatSince = -1f;
            ApplyThreatVolumes(desiredBand, _currentThreatLevel);
        }
    }

    private MusicState DetermineDesiredMusicState()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "MenuScene" || sceneName == "Menu")
            return MusicState.Menu;
        if (sceneName == "DeathScene")
            return MusicState.Death;
        if (sceneName == "BossGameplay")
            return MusicState.None;
        if (sceneName == "GameplayLoop")
        {
            if (ShouldHoldForWorldTwoStart())
                return MusicState.None;

            return MusicState.Gameplay;
        }

        return MusicState.None;
    }

    private bool ShouldHoldForWorldTwoStart()
    {
        if (_worldTwoPlaybackStarted)
            return false;

        if (GameplayHandler.Instance == null)
            return false;

        if (!WorldProgression.IsWorldTwoTransition(GameplayHandler.Instance.CurrentFloorIndex))
            return false;

        return GameplayHandler.Instance.CurrentState != GameplayHandler.FloorState.Playing;
    }

    private WorldBand DetermineDesiredWorldBand()
    {
        if (GameplayHandler.Instance == null)
            return WorldBand.WorldOne;

        return WorldProgression.GetBandForFloor(GameplayHandler.Instance.CurrentFloorIndex);
    }

    private int CalculateThreatLevel()
    {
        Transform player = GetPlayerTransform();
        if (player == null)
            return 1;

        float radius = GetThreatRadius(player.position);
        float radiusSqr = radius * radius;
        int nearbyEnemyCount = 0;
        var enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyBase enemy = enemies[i];
            if (!enemy || enemy.IsDead)
                continue;

            Vector2 offset = (Vector2)(enemy.transform.position - player.position);
            if (offset.sqrMagnitude <= radiusSqr)
                nearbyEnemyCount++;
        }

        return nearbyEnemyCount switch
        {
            <= 0 => 1,
            1 => 2,
            2 => 3,
            3 => 4,
            4 => 5,
            _ => 6
        };
    }

    private Transform GetPlayerTransform()
    {
        if (_playerTransform != null && _playerTransform.gameObject.activeInHierarchy)
            return _playerTransform;

        var playerController = FindFirstObjectByType<PlayerController>();
        _playerTransform = playerController ? playerController.transform : null;
        return _playerTransform;
    }

    private float GetThreatRadius(Vector2 playerPosition)
    {
        float radius = Mathf.Max(0.1f, minimumThreatRadius);
        if (MapSpawner.Instance != null && MapSpawner.Instance.TryGetChunkWorldBoundsAtWorldPosition(playerPosition, out Bounds chunkBounds))
        {
            float chunkExtent = Mathf.Max(chunkBounds.extents.x, chunkBounds.extents.y);
            radius = Mathf.Max(radius, chunkExtent * Mathf.Max(0.1f, threatRadiusMultiplier));
        }

        return radius;
    }

    private void RestartMusicState(MusicState state, WorldBand band)
    {
        ClearMusic();

        double startDsp = AudioSettings.dspTime + 0.05;
        switch (state)
        {
            case MusicState.Menu:
                CreateStem(menuMusic, "Menu", startDsp, MenuLoopPointSeconds);
                SetStemVolume("Menu", musicVolume);
                break;

            case MusicState.Death:
                PlayIntro(deathMusic, startDsp);
                break;

            case MusicState.Gameplay:
                if (band == WorldBand.WorldOne)
                {
                    PlayIntro(world1Start, startDsp);
                    double layerStart = startDsp + WorldOneIntroDelaySeconds;
                    CreateStem(world1CalmChords, "W1_CalmChords", layerStart, WorldOneLoopPointSeconds);
                    CreateStem(world1CalmKeys, "W1_CalmKeys", layerStart, WorldOneLoopPointSeconds);
                    CreateStem(world1Chords, "W1_Chords", layerStart, WorldOneLoopPointSeconds);
                    CreateStem(world1Keys, "W1_Keys", layerStart, WorldOneLoopPointSeconds);
                    CreateStem(world1Sidebreak, "W1_Sidebreak", layerStart, WorldOneLoopPointSeconds);
                    CreateStem(world1Mutebreak, "W1_Mutebreak", layerStart, WorldOneLoopPointSeconds);
                    CreateStem(world1Breakdown, "W1_Breakdown", layerStart, WorldOneLoopPointSeconds);
                }
                else
                {
                    PlayIntro(world2Start, startDsp);
                    double layerStart = startDsp + WorldTwoIntroDelaySeconds;
                    CreateStem(world2Chords, "W2_Chords", layerStart, WorldTwoLoopPointSeconds);
                    CreateStem(world2Keys, "W2_Keys", layerStart, WorldTwoLoopPointSeconds);
                    CreateStem(world2Arp, "W2_Arp", layerStart, WorldTwoLoopPointSeconds);
                    CreateStem(world2Crazy, "W2_Crazy", layerStart, WorldTwoLoopPointSeconds);
                    CreateStem(world2Sidebreak, "W2_Sidebreak", layerStart, WorldTwoLoopPointSeconds);
                    CreateStem(world2Breakdown, "W2_Breakdown", layerStart, WorldTwoLoopPointSeconds);
                    CreateStem(world2Waah, "W2_Waah", layerStart, WorldTwoLoopPointSeconds);
                    _worldTwoPlaybackStarted = true;
                }

                _currentThreatLevel = CalculateThreatLevel();
                _pendingThreatLevel = _currentThreatLevel;
                _pendingThreatSince = -1f;
                ApplyThreatVolumes(band, _currentThreatLevel);
                break;
        }
    }

    private void ApplyThreatVolumes(WorldBand band, int threatLevel)
    {
        if (band == WorldBand.WorldOne)
        {
            SetStemVolume("W1_CalmChords", threatLevel <= 2 ? musicVolume : 0f);
            SetStemVolume("W1_CalmKeys", threatLevel == 1 ? musicVolume : 0f);
            SetStemVolume("W1_Chords", threatLevel >= 3 ? musicVolume : 0f);
            SetStemVolume("W1_Keys", threatLevel >= 2 ? musicVolume : 0f);
            SetStemVolume("W1_Sidebreak", threatLevel is 4 or 5 ? musicVolume : 0f);
            SetStemVolume("W1_Mutebreak", threatLevel == 5 ? musicVolume : 0f);
            SetStemVolume("W1_Breakdown", threatLevel == 6 ? musicVolume : 0f);
            return;
        }

        SetStemVolume("W2_Chords", musicVolume);
        SetStemVolume("W2_Keys", musicVolume);
        SetStemVolume("W2_Arp", threatLevel >= 2 ? musicVolume : 0f);
        SetStemVolume("W2_Crazy", threatLevel >= 3 ? musicVolume : 0f);
        SetStemVolume("W2_Sidebreak", threatLevel >= 4 ? musicVolume : 0f);
        SetStemVolume("W2_Breakdown", threatLevel >= 5 ? musicVolume : 0f);
        SetStemVolume("W2_Waah", threatLevel >= 6 ? musicVolume : 0f);
    }

    private void SetStemVolume(string stemName, float volume)
    {
        for (int i = 0; i < _activeStems.Count; i++)
        {
            if (_activeStems[i].Name == stemName)
                _activeStems[i].SetVolume(volume);
        }
    }

    private void PlayIntro(AudioClip clip, double dspTime)
    {
        if (clip == null || _introSource == null)
            return;

        _introSource.clip = clip;
        _introSource.volume = musicVolume;
        _introSource.Stop();
        _introSource.PlayScheduled(dspTime);
    }

    private void CreateStem(AudioClip clip, string stemName, double firstStartDsp, float loopPointSeconds)
    {
        if (clip == null)
            return;

        var stem = new LoopingStem(
            stemName,
            CreateMusicSource($"{stemName}_A"),
            CreateMusicSource($"{stemName}_B"),
            clip,
            firstStartDsp,
            loopPointSeconds);
        _activeStems.Add(stem);
    }

    private AudioSource CreateMusicSource(string sourceName)
    {
        EnsureRuntimeRoot();

        var go = new GameObject(sourceName);
        go.transform.SetParent(_runtimeRoot, false);
        var src = go.AddComponent<AudioSource>();
        CopySourceSettings(sourceTemplate, src);
        src.playOnAwake = false;
        src.loop = false;
        src.volume = 0f;

        var filter = go.AddComponent<AudioLowPassFilter>();
        filter.cutoffFrequency = _lowHpFilterTarget;
        filter.lowpassResonanceQ = 1f;
        _musicLowPassFilters.Add(filter);

        return src;
    }

    private void ClearMusic()
    {
        if (_introSource != null)
            _introSource.Stop();

        for (int i = 0; i < _activeStems.Count; i++)
            _activeStems[i].Dispose();

        _activeStems.Clear();
    }

    private void WarmSfxPool()
    {
        if (sourceTemplate == null)
            return;

        while (_sfxPool.Count < Mathf.Max(1, initialSfxPoolSize))
            _sfxPool.Add(CreateSfxSource($"Sfx_{_sfxPool.Count}"));
    }

    private void PlaySfx(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
    {
        if (clip == null)
            return;

        float p = pitch * GetMicroPitchMultiplier(-1f);
        PlaySfxPooled(clip, Mathf.Clamp01(volumeScale), p, 0f);
    }

    /// <summary>
    /// Shared path: pan is stereo (-1..1), pitch includes any caller bias × micro-jitter for world SFX.
    /// </summary>
    private void PlaySfxPooled(AudioClip clip, float volumeScale, float pitch, float panStereo)
    {
        AudioSource src = GetAvailableSfxSource();
        src.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        src.panStereo = Mathf.Clamp(panStereo, -1f, 1f);
        float v = sfxVolume * Mathf.Clamp01(volumeScale);
        src.volume = v;
        src.PlayOneShot(clip, v);
    }

    /// <param name="pitchJitterHalfRange">If &lt; 0, uses default <see cref="sfxPitchJitterHalfRange"/>.</param>
    private float GetMicroPitchMultiplier(float pitchJitterHalfRange = -1f)
    {
        float h = pitchJitterHalfRange >= 0f ? pitchJitterHalfRange : sfxPitchJitterHalfRange;
        h = Mathf.Max(0f, h);
        if (h <= 0f)
            return 1f;
        return 1f + Random.Range(-h, h);
    }

    /// <summary>
    /// Softer presence when far, but always at least <see cref="worldSfxMinVolumeMultiplier"/>.
    /// Uses horizontal distance in 2D from the player (falls back to main camera if no player).
    /// </summary>
    private float EvaluateWorldDistanceVolumeMultiplier(Vector3 worldPos)
    {
        Transform listener = GetPlayerTransform();
        if (listener == null && Camera.main != null)
            listener = Camera.main.transform;
        if (listener == null)
            return 1f;

        float d = Vector2.Distance(new Vector2(worldPos.x, worldPos.y), new Vector2(listener.position.x, listener.position.y));
        float near = Mathf.Max(0.05f, worldSfxNearFullDistance);
        float far = Mathf.Max(near + 0.5f, worldSfxFarBlendDistance);
        float t = Mathf.InverseLerp(near, far, d);
        t = Mathf.SmoothStep(0f, 1f, t);
        float floorMul = Mathf.Clamp(worldSfxMinVolumeMultiplier, 0.05f, 1f);
        return Mathf.Lerp(1f, floorMul, t);
    }

    private AudioSource GetAvailableSfxSource()
    {
        for (int i = 0; i < _sfxPool.Count; i++)
        {
            if (!_sfxPool[i].isPlaying)
                return _sfxPool[i];
        }

        AudioSource created = CreateSfxSource($"Sfx_{_sfxPool.Count}");
        _sfxPool.Add(created);
        return created;
    }

    private AudioSource CreateSfxSource(string sourceName)
    {
        EnsureRuntimeRoot();

        var go = new GameObject(sourceName);
        go.transform.SetParent(_runtimeRoot, false);
        var src = go.AddComponent<AudioSource>();
        CopySourceSettings(sourceTemplate, src);
        src.playOnAwake = false;
        src.loop = false;
        src.volume = sfxVolume;
        return src;
    }

    private static AudioClip GetRandomClip(AudioClip[] variants, AudioClip fallback = null)
    {
        if (variants != null && variants.Length > 0)
        {
            var valid = new List<AudioClip>(variants.Length);
            for (int i = 0; i < variants.Length; i++)
            {
                if (variants[i] != null)
                    valid.Add(variants[i]);
            }

            if (valid.Count > 0)
                return valid[Random.Range(0, valid.Count)];
        }

        return fallback;
    }

    private void EnsureRuntimeRoot()
    {
        if (_runtimeRoot != null)
            return;

        var go = new GameObject("AudioRuntime");
        go.transform.SetParent(transform, false);
        _runtimeRoot = go.transform;
    }

    private void EnsureIntroSource()
    {
        if (_introSource != null)
            return;

        _introSource = CreateMusicSource("MusicIntro");
    }

    private static void CopySourceSettings(AudioSource source, AudioSource target)
    {
        if (source == null || target == null)
            return;

        target.outputAudioMixerGroup = source.outputAudioMixerGroup;
        target.priority = source.priority;
        target.pitch = 1f;
        target.panStereo = source.panStereo;
        target.spatialBlend = 0f;
        target.reverbZoneMix = source.reverbZoneMix;
        target.dopplerLevel = 0f;
        target.spread = source.spread;
        target.rolloffMode = source.rolloffMode;
        target.minDistance = source.minDistance;
        target.maxDistance = source.maxDistance;
        target.bypassEffects = source.bypassEffects;
        target.bypassListenerEffects = source.bypassListenerEffects;
        target.bypassReverbZones = source.bypassReverbZones;
        target.ignoreListenerPause = true;
        target.ignoreListenerVolume = false;
    }

    private enum MusicState
    {
        None,
        Menu,
        Death,
        Gameplay
    }

    private sealed class LoopingStem
    {
        public string Name { get; }

        private readonly AudioSource _a;
        private readonly AudioSource _b;
        private readonly AudioClip _clip;
        private readonly double _loopPointSeconds;

        private double _nextScheduledDsp;
        private bool _useA = true;
        private float _targetVolume;
        private float _currentLevel;
        private float _duckMultiplier = 1f;

        public LoopingStem(string name, AudioSource a, AudioSource b, AudioClip clip, double firstStartDsp, double loopPointSeconds)
        {
            Name = name;
            _a = a;
            _b = b;
            _clip = clip;
            _loopPointSeconds = loopPointSeconds;
            _nextScheduledDsp = firstStartDsp;
            _targetVolume = 0f;
        }

        public void SetVolume(float volume)
        {
            _targetVolume = Mathf.Clamp01(volume);
        }

        public void SetDuckMultiplier(float multiplier)
        {
            _duckMultiplier = Mathf.Clamp01(multiplier);

            float applied = _currentLevel * _duckMultiplier;
            if (_a != null) _a.volume = applied;
            if (_b != null) _b.volume = applied;
        }

        public void Tick(double dspTime, double scheduleLeadSeconds, float deltaTime)
        {
            if (_clip == null)
                return;

            UpdateVolume(deltaTime);

            while (_nextScheduledDsp <= dspTime + scheduleLeadSeconds)
            {
                AudioSource source = _useA ? _a : _b;
                if (source != null)
                {
                    source.clip = _clip;
                    source.volume = _currentLevel * _duckMultiplier;
                    source.PlayScheduled(_nextScheduledDsp);
                }

                _nextScheduledDsp += _loopPointSeconds;
                _useA = !_useA;
            }
        }

        private void UpdateVolume(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            float fadeDuration = _targetVolume > _currentLevel
                ? StemFadeInDurationSeconds
                : StemFadeOutDurationSeconds;
            float step = fadeDuration <= 0.0001f
                ? 1f
                : deltaTime / fadeDuration;
            _currentLevel = Mathf.MoveTowards(_currentLevel, _targetVolume, step);

            float applied = _currentLevel * _duckMultiplier;
            if (_a != null) _a.volume = applied;
            if (_b != null) _b.volume = applied;
        }

        public void Dispose()
        {
            if (_a != null)
                Destroy(_a.gameObject);

            if (_b != null)
                Destroy(_b.gameObject);
        }
    }
}
