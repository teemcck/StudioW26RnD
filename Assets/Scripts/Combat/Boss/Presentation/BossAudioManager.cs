using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Boss music stems, intro roar, encounter sound effects, and escape music.
/// Music layers start on one DSP timestamp, then phase changes fade their levels without restarting playback.
/// </summary>
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
    [Tooltip("Random pitch ± around 1.0 for one-shots (e.g. 0.01 ≈ ±1%).")]
    [SerializeField] [Range(0f, 0.04f)] private float bossSfxPitchJitterHalfRange = 0.01f;
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

    /// <summary>
    /// Creates owned child AudioSources for stems, roar, one-shots, and the independently pitched death sound.
    /// </summary>
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

    /// <summary>
    /// Creates a nonspatial one-shot source using the boss SFX priority and optional mixer route.
    /// </summary>
    private AudioSource NewSfxSource(string goName)
    {
        GameObject go = new GameObject(goName);
        go.transform.SetParent(transform, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = false;
        s.spatialBlend = 0f;
        s.dopplerLevel = 0f;
        s.priority = BossSfxSourcePriority;
        if (sfxOutput)
            s.outputAudioMixerGroup = sfxOutput;
        return s;
    }

    /// <summary>
    /// Plays an assigned encounter one-shot using authored volume and pitch-jitter settings.
    /// </summary>
    private void PlayBossSfxClip(AudioClip clip, float volumeScale = 1f)
    {
        if (!clip || !_sfxSource) return;
        float h = bossSfxPitchJitterHalfRange;
        _sfxSource.pitch = h <= 0f ? 1f : Mathf.Clamp(1f + UnityEngine.Random.Range(-h, h), 0.5f, 2f);
        _sfxSource.PlayOneShot(clip, Mathf.Clamp01(bossSfxVolume * volumeScale));
        _sfxSource.pitch = 1f;
    }

    /// <summary>
    /// Plays the sound that introduces phase two.
    /// </summary>
    public void PlayPhaseTransitionToPhase2Sfx() => PlayBossSfxClip(sfxPhaseTransitionToPhase2);

    /// <summary>
    /// Plays the sound that introduces phase three.
    /// </summary>
    public void PlayPhaseTransitionToPhase3Sfx() => PlayBossSfxClip(sfxPhaseTransitionToPhase3);

    /// <summary>
    /// Plays the beam-firing sound when laser charge completes.
    /// </summary>
    public void PlayLaserShootSfx() => PlayBossSfxClip(sfxLaserShoot);

    /// <summary>
    /// Plays one sound for a projectile volley.
    /// </summary>
    public void PlayRangedShootSfx() => PlayBossSfxClip(sfxRangedShoot);

    /// <summary>
    /// Plays the impact cue when the telegraphed melee strike resolves.
    /// </summary>
    public void PlayMeleeSfx() => PlayBossSfxClip(sfxMelee);

    /// <summary>
    /// Plays the cue as digging or repositioning begins.
    /// </summary>
    public void PlayDigSfx() => PlayBossSfxClip(sfxDig);

    /// <summary>
    /// Plays the cue as the boss emerges from underground.
    /// </summary>
    public void PlayRiseSfx() => PlayBossSfxClip(sfxRise);

    /// <summary>
    /// Plays the ordinary death one-shot when no animation-duration matching is required.
    /// </summary>
    public void PlayDeathSfx() => PlayBossSfxClip(sfxDeath);

    /// <summary>
    /// Uses a separate source to lower death-sound pitch and stop playback at the requested animation duration.
    /// </summary>
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

    /// <summary>
    /// Stops the matched death clip after real elapsed seconds and restores the source's normal pitch.
    /// </summary>
    private IEnumerator CoStopDeathSfxAfter(float durationWorldSeconds)
    {
        yield return new WaitForSecondsRealtime(durationWorldSeconds);
        if (_pitchedSfxSource && _pitchedSfxSource.isPlaying && _pitchedSfxSource.clip == sfxDeath)
            _pitchedSfxSource.Stop();
        if (_pitchedSfxSource)
            _pitchedSfxSource.pitch = 1f;
        _deathSfxStopRoutine = null;
    }

    /// <summary>
    /// Plays the HP-damage cue after encounter shields have been exhausted.
    /// </summary>
    public void PlayBossDamageSfx() => PlayBossSfxClip(sfxBossDamage);

    /// <summary>
    /// Plays the shield-hit cue without implying that HP was damaged.
    /// </summary>
    public void PlayShieldDamageSfx() => PlayBossSfxClip(sfxShieldDamage);

    /// <summary>
    /// Plays the shield-break cue at the start of its stun feedback.
    /// </summary>
    public void PlayShieldBreakSfx() => PlayBossSfxClip(sfxShieldBreak);

    /// <summary>
    /// Listens for audio-device changes so playing stems can be realigned.
    /// </summary>
    private void OnEnable()
    {
        AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
    }

    /// <summary>
    /// Removes the audio callback and stops any independently pitched death playback.
    /// </summary>
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

    /// <summary>
    /// Realigns active combat stems after an audio configuration change.
    /// </summary>
    private void OnAudioConfigurationChanged(bool deviceWasChanged)
    {
        if (_layersRunning && !_escapeActive)
            MaintainLayerStemLockstep();
    }

    /// <summary>
    /// Realigns combat stems when the application resumes from a pause.
    /// </summary>
    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus && _layersRunning && !_escapeActive)
            MaintainLayerStemLockstep();
    }

    /// <summary>
    /// Applies the common music priority and optional mixer output to a stem.
    /// </summary>
    private void ApplySharedMusicSourceSettings(AudioSource s)
    {
        s.priority = BossMusicSourcePriority;
        if (musicOutput)
            s.outputAudioMixerGroup = musicOutput;
    }

    /// <summary>
    /// Starts boss music after owned AudioSources have been initialized.
    /// </summary>
    private void Start()
    {
        BeginBossPlayback();
    }

    /// <summary>
    /// Creates a nonspatial, explicitly started stem; custom sample-window code owns looping.
    /// </summary>
    private AudioSource NewStem(string goName)
    {
        GameObject go = new GameObject(goName);
        go.transform.SetParent(transform, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = false;
        s.spatialBlend = 0f;
        s.dopplerLevel = 0f;
        return s;
    }

    /// <summary>
    /// Starts the intro music sequence once and resets phase bookkeeping for that initial playback.
    /// </summary>
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

    /// <summary>
    /// Queues phase changes before stems start, or fades active stems to the requested phase mix.
    /// </summary>
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

    /// <summary>
    /// Cancels intro/phase music work once, stops the roar, and begins the escape handoff.
    /// </summary>
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

    /// <summary>
    /// Schedules roar audio against the animation's scaled-time start and duration.
    /// </summary>
    public void NotifyIntroGrowlStarted(float roarAnimLengthSeconds)
    {
        if (!bossGrowl || roarAnimLengthSeconds <= 0.0001f)
            return;
        float roarStartTime = Time.time;
        if (_growlRoutine != null)
            StopCoroutine(_growlRoutine);
        _growlRoutine = StartCoroutine(CoIntroGrowl(roarStartTime, roarAnimLengthSeconds));
    }

    /// <summary>
    /// Applies the configured audio lead and optionally cuts the roar when its animation ends.
    /// </summary>
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

    /// <summary>
    /// Plays the opening clip, schedules all phase stems together on the DSP clock, then applies any queued phase.
    /// </summary>
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

    /// <summary>
    /// Maintains the authored sample-loop window and shared phase for active combat stems.
    /// </summary>
    private void LateUpdate()
    {
        if (!_layersRunning || _escapeActive || !_layerLoopCacheReady)
            return;
        if (layerLoopLengthSeconds <= 0.0001f)
            return;
        MaintainLayerStemLockstep();
    }

    /// <summary>
    /// Converts the configured loop duration into each clip's own sample-rate window.
    /// </summary>
    private void CacheLayerLoopSamples()
    {
        _loopSamplesP1 = ComputeLoopSampleWindow(bossP1, layerLoopLengthSeconds);
        _loopSamplesP2 = ComputeLoopSampleWindow(bossP2, layerLoopLengthSeconds);
        _loopSamplesP3 = ComputeLoopSampleWindow(bossP3, layerLoopLengthSeconds);
        _layerLoopCacheReady = _loopSamplesP1 > 0 && _loopSamplesP2 > 0 && _loopSamplesP3 > 0;
    }

    /// <summary>
    /// Returns a positive sample window bounded by the actual clip length, or zero for invalid input.
    /// </summary>
    private static int ComputeLoopSampleWindow(AudioClip clip, float loopLenSec)
    {
        if (!clip || loopLenSec <= 0.0001f)
            return 0;
        int n = Mathf.RoundToInt(loopLenSec * clip.frequency);
        if (n <= 0)
            return 0;
        return Mathf.Min(n, clip.samples);
    }

    /// <summary>
    /// Wraps phase-one samples within the authored loop and uses that time as the other stems' master phase.
    /// </summary>
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

    /// <summary>
    /// Maps master elapsed seconds into a follower clip's sample rate and bounded loop window.
    /// </summary>
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

    /// <summary>
    /// Adds phase stems cumulatively, fading from current levels so interrupted transitions remain continuous.
    /// </summary>
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

    /// <summary>
    /// Starts the escape clip and fades combat stems to silence before stopping them and the opening clip.
    /// </summary>
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

    /// <summary>
    /// Cancels remaining music coroutines while Unity destroys their owned child sources.
    /// </summary>
    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}
