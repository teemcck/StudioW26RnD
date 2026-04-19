using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public sealed class BossAudioManager : MonoBehaviour
{
    private const int BossMusicSourcePriority = 16;
    private const int BossSfxSourcePriority = 44;

    [Header("Clips")]
    [SerializeField] private AudioClip bossStart;
    [SerializeField] private AudioClip bossP1;
    [SerializeField] private AudioClip bossP2;
    [SerializeField] private AudioClip bossP3;
    [SerializeField] private AudioClip bossEscape;
    [SerializeField] private AudioClip bossGrowl;

    [Header("Music")]
    [SerializeField] private AudioMixerGroup musicOutput;

    [Header("SFX")]
    [SerializeField] private AudioClip sfxPhaseTransitionToPhase2;
    [SerializeField] private AudioClip sfxPhaseTransitionToPhase3;
    [SerializeField] private AudioClip sfxLaserShoot;
    [SerializeField] private AudioClip sfxRangedShoot;
    [SerializeField] private AudioClip sfxMelee;
    [SerializeField] private AudioClip sfxDig;
    [SerializeField] private AudioClip sfxRise;
    [SerializeField] private AudioClip sfxDeath;
    [SerializeField] private AudioClip sfxBossDamage;
    [SerializeField] private AudioClip sfxShieldDamage;
    [SerializeField] private AudioClip sfxShieldBreak;
    [SerializeField] [Range(0f, 2f)] private float bossSfxVolume = 1f;
    [SerializeField] [Range(0.65f, 1f)] private float deathSfxPitchLean = 0.92f;
    [SerializeField] private AudioMixerGroup sfxOutput;

    [Header("Growl")]
    [SerializeField] private float growlIntroLeadSeconds;
    [SerializeField] private bool stopGrowlWhenRoarAnimEnds = true;

    [Header("Timing")]
    [SerializeField] private float layerEntryDelaySeconds = 11.885f;
    [SerializeField] private float layerLoopLengthSeconds = 30.222f;
    [SerializeField] private float layerScheduleAheadSeconds = 0.08f;
    [SerializeField] private float phaseFadeSeconds = 1.5f;
    [SerializeField] private float escapeFadeOutSeconds = 2f;

    [Header("Levels")]
    [SerializeField] [Range(0f, 1f)] private float maxStemVolume = 0.55f;

    private AudioSource _startSource;
    private AudioSource _p1Source;
    private AudioSource _p2Source;
    private AudioSource _p3Source;
    private AudioSource _escapeSource;
    private AudioSource _growlSource;
    private AudioSource _sfxSource;
    private AudioSource _pitchedSfxSource;

    private Coroutine _playbackRoutine;
    private Coroutine _growlRoutine;
    private Coroutine _phaseFadeRoutine;
    private Coroutine _deathSfxStopRoutine;
    private int _bossPhase = 1;
    private int _pendingPhase = -1;
    private bool _playbackBegun;
    private bool _layersRunning;
    private bool _escapeActive;

    private int _loopSamplesP1;
    private int _loopSamplesP2;
    private int _loopSamplesP3;
    private bool _layerLoopCacheReady;

    private void Awake()
    {
        _startSource = NewStem("Boss_Start");
        _p1Source = NewStem("Boss_P1");
        _p2Source = NewStem("Boss_P2");
        _p3Source = NewStem("Boss_P3");
        _escapeSource = NewStem("Boss_Escape");
        _growlSource = NewStem("Boss_Growl");

        ApplySharedMusicSourceSettings(_startSource);
        ApplySharedMusicSourceSettings(_p1Source);
        ApplySharedMusicSourceSettings(_p2Source);
        ApplySharedMusicSourceSettings(_p3Source);
        ApplySharedMusicSourceSettings(_escapeSource);
        ApplySharedMusicSourceSettings(_growlSource);

        _sfxSource = NewSfxSource("Boss_SFX");
        _pitchedSfxSource = NewSfxSource("Boss_SFX_Pitched");
    }

    private AudioSource NewSfxSource(string goName)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = false;
        s.spatialBlend = 0f;
        s.dopplerLevel = 0f;
        s.priority = BossSfxSourcePriority;
        if (sfxOutput)
            s.outputAudioMixerGroup = sfxOutput;
        return s;
    }

    private void PlayBossSfxClip(AudioClip clip, float volumeScale = 1f)
    {
        if (!clip || !_sfxSource) return;
        _sfxSource.PlayOneShot(clip, Mathf.Clamp01(bossSfxVolume * volumeScale));
    }

    public void PlayPhaseTransitionToPhase2Sfx() => PlayBossSfxClip(sfxPhaseTransitionToPhase2);
    public void PlayPhaseTransitionToPhase3Sfx() => PlayBossSfxClip(sfxPhaseTransitionToPhase3);
    public void PlayLaserShootSfx() => PlayBossSfxClip(sfxLaserShoot);
    public void PlayRangedShootSfx() => PlayBossSfxClip(sfxRangedShoot);
    public void PlayMeleeSfx() => PlayBossSfxClip(sfxMelee);
    public void PlayDigSfx() => PlayBossSfxClip(sfxDig);
    public void PlayRiseSfx() => PlayBossSfxClip(sfxRise);
    public void PlayDeathSfx() => PlayBossSfxClip(sfxDeath);

    public void PlayDeathSfxForAnimationDuration(float durationSeconds)
    {
        if (!sfxDeath || !_pitchedSfxSource)
            return;
        if (_deathSfxStopRoutine != null)
        {
            StopCoroutine(_deathSfxStopRoutine);
            _deathSfxStopRoutine = null;
        }

        float T = Mathf.Max(0.05f, durationSeconds);
        float pitch = (sfxDeath.length / T) * deathSfxPitchLean;
        pitch = Mathf.Clamp(pitch, 0.18f, 1f);
        _pitchedSfxSource.Stop();
        _pitchedSfxSource.clip = sfxDeath;
        _pitchedSfxSource.pitch = pitch;
        _pitchedSfxSource.volume = Mathf.Clamp01(bossSfxVolume);
        _pitchedSfxSource.time = 0f;
        _pitchedSfxSource.Play();
        _deathSfxStopRoutine = StartCoroutine(CoStopDeathSfxAfter(T));
    }

    private IEnumerator CoStopDeathSfxAfter(float durationWorldSeconds)
    {
        yield return new WaitForSecondsRealtime(durationWorldSeconds);
        if (_pitchedSfxSource && _pitchedSfxSource.isPlaying && _pitchedSfxSource.clip == sfxDeath)
            _pitchedSfxSource.Stop();
        if (_pitchedSfxSource)
            _pitchedSfxSource.pitch = 1f;
        _deathSfxStopRoutine = null;
    }

    public void PlayBossDamageSfx() => PlayBossSfxClip(sfxBossDamage);
    public void PlayShieldDamageSfx() => PlayBossSfxClip(sfxShieldDamage);
    public void PlayShieldBreakSfx() => PlayBossSfxClip(sfxShieldBreak);

    private void OnEnable()
    {
        AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
    }

    private void OnDisable()
    {
        AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        if (_deathSfxStopRoutine != null)
        {
            StopCoroutine(_deathSfxStopRoutine);
            _deathSfxStopRoutine = null;
        }

        if (_pitchedSfxSource)
        {
            _pitchedSfxSource.Stop();
            _pitchedSfxSource.pitch = 1f;
        }
    }

    private void OnAudioConfigurationChanged(bool deviceWasChanged)
    {
        if (_layersRunning && !_escapeActive)
            MaintainLayerStemLockstep();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus && _layersRunning && !_escapeActive)
            MaintainLayerStemLockstep();
    }

    private void ApplySharedMusicSourceSettings(AudioSource s)
    {
        s.priority = BossMusicSourcePriority;
        if (musicOutput)
            s.outputAudioMixerGroup = musicOutput;
    }

    private void Start()
    {
        BeginBossPlayback();
    }

    private AudioSource NewStem(string goName)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = false;
        s.spatialBlend = 0f;
        s.dopplerLevel = 0f;
        return s;
    }

    public void BeginBossPlayback()
    {
        if (_playbackBegun)
            return;
        _playbackBegun = true;
        _bossPhase = 1;
        _pendingPhase = -1;
        _escapeActive = false;
        if (_playbackRoutine != null)
            StopCoroutine(_playbackRoutine);
        _playbackRoutine = StartCoroutine(CoBossPlayback());
    }

    public void NotifyBossPhase(int phase)
    {
        if (_escapeActive)
            return;
        _bossPhase = Mathf.Clamp(phase, 1, 3);
        if (!_layersRunning)
        {
            _pendingPhase = _bossPhase;
            return;
        }

        if (_phaseFadeRoutine != null)
            StopCoroutine(_phaseFadeRoutine);
        _phaseFadeRoutine = StartCoroutine(CoFadePhaseStems(_bossPhase));
    }

    public void BeginEscapeMusic()
    {
        if (_escapeActive)
            return;
        _escapeActive = true;
        if (_growlRoutine != null)
        {
            StopCoroutine(_growlRoutine);
            _growlRoutine = null;
        }

        if (_growlSource && _growlSource.isPlaying)
            _growlSource.Stop();

        if (_playbackRoutine != null)
        {
            StopCoroutine(_playbackRoutine);
            _playbackRoutine = null;
        }

        if (_phaseFadeRoutine != null)
        {
            StopCoroutine(_phaseFadeRoutine);
            _phaseFadeRoutine = null;
        }

        StartCoroutine(CoEscapePlayback());
    }

    public void NotifyIntroGrowlStarted(float roarAnimLengthSeconds)
    {
        if (!bossGrowl || roarAnimLengthSeconds <= 0.0001f)
            return;
        float roarStartTime = Time.time;
        if (_growlRoutine != null)
            StopCoroutine(_growlRoutine);
        _growlRoutine = StartCoroutine(CoIntroGrowl(roarStartTime, roarAnimLengthSeconds));
    }

    private IEnumerator CoIntroGrowl(float roarStartedTime, float roarAnimLengthSeconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, growlIntroLeadSeconds));
        if (!bossGrowl)
            yield break;

        _growlSource.clip = bossGrowl;
        _growlSource.loop = false;
        _growlSource.volume = 0.6f;
        _growlSource.time = 0f;
        _growlSource.Play();

        float roarEndsAt = roarStartedTime + roarAnimLengthSeconds;
        if (stopGrowlWhenRoarAnimEnds)
        {
            while (Time.time < roarEndsAt && _growlSource.isPlaying)
                yield return null;
            _growlSource.Stop();
        }
        else
        {
            while (_growlSource.isPlaying)
                yield return null;
        }

        _growlRoutine = null;
    }

    private IEnumerator CoBossPlayback()
    {
        if (bossStart)
        {
            _startSource.clip = bossStart;
            _startSource.loop = false;
            _startSource.volume = 1f;
            _startSource.Play();
        }

        yield return new WaitForSeconds(layerEntryDelaySeconds);

        if (_escapeActive)
            yield break;

        if (!bossP1 || !bossP2 || !bossP3)
            yield break;

        CacheLayerLoopSamples();
        if (!_layerLoopCacheReady)
            yield break;

        double dsp = AudioSettings.dspTime + Math.Max(0.0, layerScheduleAheadSeconds);
        _p1Source.clip = bossP1;
        _p2Source.clip = bossP2;
        _p3Source.clip = bossP3;
        _p1Source.loop = false;
        _p2Source.loop = false;
        _p3Source.loop = false;
        _p1Source.volume = 0f;
        _p2Source.volume = 0f;
        _p3Source.volume = 0f;

        _p1Source.PlayScheduled(dsp);
        _p2Source.PlayScheduled(dsp);
        _p3Source.PlayScheduled(dsp);
        _layersRunning = true;

        int fadePhase = _pendingPhase > 0 ? _pendingPhase : 1;
        _pendingPhase = -1;
        _bossPhase = fadePhase;
        yield return StartCoroutine(CoFadePhaseStems(fadePhase));
    }

    private void LateUpdate()
    {
        if (!_layersRunning || _escapeActive || !_layerLoopCacheReady)
            return;
        if (layerLoopLengthSeconds <= 0.0001f)
            return;
        MaintainLayerStemLockstep();
    }

    private void CacheLayerLoopSamples()
    {
        _loopSamplesP1 = ComputeLoopSampleWindow(bossP1, layerLoopLengthSeconds);
        _loopSamplesP2 = ComputeLoopSampleWindow(bossP2, layerLoopLengthSeconds);
        _loopSamplesP3 = ComputeLoopSampleWindow(bossP3, layerLoopLengthSeconds);
        _layerLoopCacheReady = _loopSamplesP1 > 0 && _loopSamplesP2 > 0 && _loopSamplesP3 > 0;
    }

    private static int ComputeLoopSampleWindow(AudioClip clip, float loopLenSec)
    {
        if (!clip || loopLenSec <= 0.0001f)
            return 0;
        int n = Mathf.RoundToInt(loopLenSec * clip.frequency);
        if (n <= 0)
            return 0;
        return Mathf.Min(n, clip.samples);
    }

    private void MaintainLayerStemLockstep()
    {
        if (!_p1Source || !_p1Source.isPlaying || !_p1Source.clip)
            return;

        int masterTs = _p1Source.timeSamples;
        int loop1 = _loopSamplesP1;
        while (masterTs >= loop1)
            masterTs -= loop1;
        _p1Source.timeSamples = masterTs;

        double phaseSec = masterTs / (double)_p1Source.clip.frequency;
        SnapFollowerStemToPhase(_p2Source, _loopSamplesP2, phaseSec);
        SnapFollowerStemToPhase(_p3Source, _loopSamplesP3, phaseSec);
    }

    private static void SnapFollowerStemToPhase(AudioSource src, int loopSamples, double masterPhaseSec)
    {
        if (!src || !src.isPlaying || !src.clip || loopSamples <= 0)
            return;

        AudioClip c = src.clip;
        int ts = (int)Math.Round(masterPhaseSec * c.frequency);
        while (ts >= loopSamples)
            ts -= loopSamples;
        ts = Mathf.Clamp(ts, 0, Mathf.Max(0, loopSamples - 1));
        src.timeSamples = ts;
    }

    private IEnumerator CoFadePhaseStems(int phase)
    {
        float v1 = phase >= 1 ? maxStemVolume : 0f;
        float v2 = phase >= 2 ? maxStemVolume : 0f;
        float v3 = phase >= 3 ? maxStemVolume : 0f;

        float t = 0f;
        float a1 = _p1Source.volume;
        float a2 = _p2Source.volume;
        float a3 = _p3Source.volume;
        float dur = Mathf.Max(0.05f, phaseFadeSeconds);

        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            _p1Source.volume = Mathf.Lerp(a1, v1, u);
            _p2Source.volume = Mathf.Lerp(a2, v2, u);
            _p3Source.volume = Mathf.Lerp(a3, v3, u);
            yield return null;
        }

        _p1Source.volume = v1;
        _p2Source.volume = v2;
        _p3Source.volume = v3;
        _phaseFadeRoutine = null;
    }

    private IEnumerator CoEscapePlayback()
    {
        if (bossEscape)
        {
            _escapeSource.clip = bossEscape;
            _escapeSource.loop = false;
            _escapeSource.volume = 1f;
            _escapeSource.Play();
        }

        float t = 0f;
        float a1 = _p1Source.volume;
        float a2 = _p2Source.volume;
        float a3 = _p3Source.volume;
        float dur = Mathf.Max(0.05f, escapeFadeOutSeconds);

        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            _p1Source.volume = Mathf.Lerp(a1, 0f, u);
            _p2Source.volume = Mathf.Lerp(a2, 0f, u);
            _p3Source.volume = Mathf.Lerp(a3, 0f, u);
            yield return null;
        }

        _p1Source.volume = 0f;
        _p2Source.volume = 0f;
        _p3Source.volume = 0f;
        _p1Source.Stop();
        _p2Source.Stop();
        _p3Source.Stop();
        _layersRunning = false;

        if (_startSource.isPlaying)
            _startSource.Stop();
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}
