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

    [Header("Sound Effects")]
    [SerializeField] private AudioClip playerHitSfx;
    [SerializeField] private AudioClip playerDashSfx;
    [SerializeField] private AudioClip playerMeleeSfx;
    [SerializeField] private AudioClip playerDeathSfx;
    [SerializeField] private AudioClip enemyHitSfx;
    [SerializeField] private AudioClip upgradeSelectedSfx;
    [SerializeField] private AudioClip uiButtonSfx;
    [SerializeField] private AudioClip teleporterEnteredSfx;

    private readonly List<AudioSource> _sfxPool = new();
    private readonly List<LoopingStem> _activeStems = new();

    private Transform _runtimeRoot;
    private AudioSource _introSource;
    private MusicState _currentMusicState = MusicState.None;
    private WorldBand _currentBand = WorldBand.WorldOne;
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
    public void PlayPlayerMelee() => PlaySfx(playerMeleeSfx);
    public void PlayPlayerDeath() => PlaySfx(playerDeathSfx);

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive)
            return;

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

        PlayEnemyHit();
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
        if (sceneName == "MenuScene" || sceneName == "Menu" || sceneName == "DeathScene")
            return MusicState.Menu;

        if (sceneName == "GameplayLoop" || sceneName == "BossGameplay")
        {
            if (ShouldHoldForWorldTwoStart())
                return MusicState.None;

            return MusicState.Gameplay;
        }

        return MusicState.None;
    }

    private bool ShouldHoldForWorldTwoStart()
    {
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

    private void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null)
            return;

        AudioSource src = GetAvailableSfxSource();
        src.pitch = 1f;
        src.volume = sfxVolume * Mathf.Clamp01(volumeScale);
        src.PlayOneShot(clip, sfxVolume * Mathf.Clamp01(volumeScale));
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
                    source.volume = _targetVolume;
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

            float currentVolume = _a != null ? _a.volume : _targetVolume;
            float fadeDuration = _targetVolume > currentVolume
                ? StemFadeInDurationSeconds
                : StemFadeOutDurationSeconds;
            float step = fadeDuration <= 0.0001f
                ? 1f
                : deltaTime / fadeDuration;
            float nextVolume = Mathf.MoveTowards(currentVolume, _targetVolume, step);
            if (_a != null) _a.volume = nextVolume;
            if (_b != null) _b.volume = nextVolume;
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
