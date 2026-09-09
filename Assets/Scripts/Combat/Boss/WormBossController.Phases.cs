using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Queued phase changes and their shield, camera, animation, and music sequence for WormBossController.
/// </summary>
public sealed partial class WormBossController
{
    // Separate the HP-derived phase from the last cinematic started, including hits that skip a phase.
    private int _lastObservedPhase = 1;

    private bool _wormAnimatorHeldOnPhaseChangeFrame;

    private bool _phaseTransitionActive;

    private Coroutine _phaseTransitionRoutine;

    private bool _phaseChangePending;

    private int _queuedNewPhase = 1;

    /// <summary>
    /// Defers phase transitions during the intro or another transition; active moves may be interrupted.
    /// </summary>
    private bool CanStartPhaseTransitionNow()
    {
        if (_introActive) return false;
        if (_phaseTransitionActive) return false;
        return true;
    }

    /// <summary>
    /// Interrupts combat, refills the new shield under damage immunity, then restores camera, animation, and music.
    /// </summary>
    private IEnumerator PhaseTransitionSequence(int newPhase)
    {
        // Claim immunity before cancellation exposes a previously underground boss.
        _phaseTransitionActive = true;
        _phaseChangePending = false;
        if (_shieldBreakFeedbackRoutine != null)
        {
            StopCoroutine(_shieldBreakFeedbackRoutine);
            _shieldBreakFeedbackRoutine = null;
        }
        ResetSpriteVisualLocalIfChild();

        PriorityCancelOngoingMoves();
        _isStunned = false;
        _state = InternalState.Idle;

        _shieldBroken = false;
        _currentShield = 0f;
        _lastShieldDamageTime = Time.time;
        UpdateShieldUI();

        Vector2 aim = AimDirectionToPlayer();
        FaceDirection(aim);

        CameraController cam = phaseTransitionCamera ? phaseTransitionCamera : FindFirstObjectByType<CameraController>();
        if (AudioManager.Instance != null)
            AudioManager.Instance.DuckMusic(0.08f, 0f);
        if (cam)
        {
            float enterShake = newPhase >= 3 ? phaseTransitionShakeEnterPhase3 : phaseTransitionShakeEnterPhase2;
            cam.Shake(Mathf.Max(0.05f, enterShake));
        }
        if (cam) cam.PhaseTransitionZoomIn(transform);

        if (newPhase == 2) ResolveBossAudio()?.PlayPhaseTransitionToPhase2Sfx();
        else if (newPhase == 3) ResolveBossAudio()?.PlayPhaseTransitionToPhase3Sfx();

        if (healthBarUI)
            healthBarUI.NotifyPhaseChange(newPhase);

        float phaseClipLen = GetAnimClipLength(StatePhaseChange, 0.88f);
        CrossFadeAnimState(StatePhaseChange, 0.1f);
        float introT = 0f;
        while (introT < phaseClipLen)
        {
            introT += Time.deltaTime;
            if (_rb) _rb.linearVelocity = Vector2.zero;
            yield return null;
        }

        FreezePhaseChangeHoldPose();

        float maxShield = GetMaxShieldForCurrentPhase();
        float fillDur = newPhase >= 3 ? phaseTransitionShieldFillPhase3 : phaseTransitionShieldFillPhase2;
        fillDur = Mathf.Max(0.05f, fillDur);
        float fillT = 0f;
        while (fillT < fillDur)
        {
            fillT += Time.deltaTime;
            float u = Mathf.Clamp01(fillT / fillDur);
            _currentShield = maxShield * u;
            UpdateShieldUI();
            if (_rb) _rb.linearVelocity = Vector2.zero;
            yield return null;
        }

        _currentShield = maxShield;
        _lastShieldDamageTime = Time.time;
        UpdateShieldUI();
        if (healthBarUI)
            healthBarUI.NotifyShieldRestored(newPhase);

        float holdAfter = newPhase >= 3 ? phaseTransitionLaserHoldAfterShieldPhase3 : phaseTransitionLaserHoldAfterShieldPhase2;
        holdAfter = Mathf.Max(0f, holdAfter);
        float holdT = 0f;
        while (holdT < holdAfter)
        {
            holdT += Time.deltaTime;
            if (_rb) _rb.linearVelocity = Vector2.zero;
            yield return null;
        }

        ReleasePhaseChangeHoldPose();
        PlayAnimState(StateIdle);

        if (cam) cam.PhaseTransitionZoomRestore();
        if (cam && phaseTransitionShakeResume > 0.0001f)
            cam.ShakeMedium();

        BossAudioManager bossAudio = ResolveBossAudio();
        if (bossAudio)
            bossAudio.NotifyBossPhase(newPhase);

        _phaseTransitionActive = false;
        _phaseTransitionRoutine = null;
    }

    /// <summary>
    /// Holds the last transition frame while the new shield fills and the cinematic pause completes.
    /// </summary>
    private void FreezePhaseChangeHoldPose()
    {
        if (!animator) return;
        animator.Play(StatePhaseChange, 0, 1f);
        animator.Update(0f);
        animator.speed = 0f;
        _wormAnimatorHeldOnPhaseChangeFrame = true;
    }

    /// <summary>
    /// Releases the transition's Animator hold on completion or interruption.
    /// </summary>
    private void ReleasePhaseChangeHoldPose()
    {
        if (!_wormAnimatorHeldOnPhaseChangeFrame) return;
        _wormAnimatorHeldOnPhaseChangeFrame = false;
        if (animator) animator.speed = 1f;
    }

    /// <summary>
    /// Queues the latest HP-derived phase and gives its transition priority over ordinary combat feedback.
    /// </summary>
    private void TickPhaseTransitions()
    {
        if (CurrentPhase != _lastObservedPhase)
        {
            if (!_phaseChangePending)
                _phaseChangePending = true;
            _queuedNewPhase = CurrentPhase;
        }

        if (_phaseChangePending && _phaseTransitionRoutine == null && CanStartPhaseTransitionNow())
        {
            if (_shieldBreakFeedbackRoutine != null)
            {
                StopCoroutine(_shieldBreakFeedbackRoutine);
                _shieldBreakFeedbackRoutine = null;
            }
            ResetSpriteVisualLocalIfChild();

            _lastObservedPhase = _queuedNewPhase;
            _phaseTransitionRoutine = StartCoroutine(PhaseTransitionSequence(_queuedNewPhase));
        }
    }
}
