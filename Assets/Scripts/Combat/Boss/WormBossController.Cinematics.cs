using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The public room-intro bridge and boss-death outro for WormBossController.
/// The boss remains alive as a Unity object until the exit reveal and escape handoff are complete.
/// </summary>
public sealed partial class WormBossController
{
    private bool _introActive;

    private float _deathHealthBarDrainFromNormalized = 1f;

    /// <summary>
    /// Whether enabling the boss waits for the room intro to start combat explicitly.
    /// </summary>
    public bool WaitForExternalCutscene => waitForExternalCutscene;

    /// <summary>
    /// Controls intro damage immunity. The room handler clears it before starting combat.
    /// </summary>
    public void SetIntroCutsceneActive(bool active)
    {
        _introActive = active;
    }

    /// <summary>
    /// Starts combat once; the caller separately releases intro immunity with SetIntroCutsceneActive.
    /// </summary>
    public void StartCombatAfterCutscene()
    {
        if (_behaviorRoutine != null)
            return;
        _state = InternalState.Idle;
        PlayAnimState(StateIdle);
        _behaviorRoutine = StartCoroutine(BehaviorLoop());
    }

    /// <summary>
    /// Lets the room intro populate the boss bar before revealing the HUD.
    /// </summary>
    public void BindHealthBarForFightStart() => BindHealthBar();

    /// <summary>
    /// Faces the player before hiding the boss, its collision, and its shadows for the intro.
    /// </summary>
    public void Cutscene_PrepareBuriedFacingPlayer()
    {
        FaceDirection(AimDirectionToPlayer());
        SetUndergroundVisuals(true);
    }

    /// <summary>
    /// Exposes buried visibility and damage immunity to the external intro sequence.
    /// </summary>
    public void Cutscene_SetUnderground(bool underground) => SetUndergroundVisuals(underground);

    /// <summary>
    /// Exposes named animation playback to the external intro without exposing the Animator.
    /// </summary>
    public void Cutscene_PlayAnimatorState(string stateName) => PlayAnimState(stateName);

    /// <summary>
    /// Returns authored clip timing, or the supplied seconds when the clip cannot be resolved.
    /// </summary>
    public float Cutscene_GetAnimatorClipLength(string stateName, float fallback) => GetAnimClipLength(stateName, fallback);

    /// <summary>
    /// Claims the dead state immediately and stops combat, deferring EnemyBase death completion until the outro finishes.
    /// </summary>
    protected override void Die(DamageContext context = default)
    {
        if (_state == InternalState.Dead) return;
        _state = InternalState.Dead;
        _immuneDuringDigMove = false;
        _introActive = false;
        if (_behaviorRoutine != null) StopCoroutine(_behaviorRoutine);
        if (_attackRoutine != null) StopCoroutine(_attackRoutine);
        if (_damageRoutine != null) StopCoroutine(_damageRoutine);
        if (_repositionRoutine != null) StopCoroutine(_repositionRoutine);
        _repositionRoutine = null;
        if (_phaseTransitionRoutine != null) StopCoroutine(_phaseTransitionRoutine);
        _phaseTransitionActive = false;
        _phaseChangePending = false;
        ReleaseWormLaserFireHoldPose();
        ReleasePhaseChangeHoldPose();
        if (_shieldPingRoutine != null) StopCoroutine(_shieldPingRoutine);
        if (_shieldBreakFeedbackRoutine != null) StopCoroutine(_shieldBreakFeedbackRoutine);
        _shieldBreakFeedbackRoutine = null;

        ResetSpriteVisualLocalIfChild();
        DestroyActiveIndicator();
        StartCoroutine(DeathRoutine(context));
    }

    /// <summary>
    /// Plays the death cinematic, reveals the exit, starts escape systems, then emits the ordinary enemy death event.
    /// </summary>
    private IEnumerator DeathRoutine(DamageContext context)
    {
        SetUndergroundVisuals(false);
        if (_rb) _rb.linearVelocity = Vector2.zero;

        if (healthBarUI)
            yield return healthBarUI.AnimateHealthFillTo(0f, deathHealthDrainSeconds, _deathHealthBarDrainFromNormalized);

        CameraController cam = phaseTransitionCamera ? phaseTransitionCamera : FindFirstObjectByType<CameraController>();
        if (cam)
            yield return cam.PlayDeathKillImpactRoutine();
        if (!escapeSequenceManager)
            escapeSequenceManager = FindFirstObjectByType<BossEscapeSequenceManager>();
        GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
        PlayerController playerController = playerGo ? playerGo.GetComponent<PlayerController>() : null;
        Transform playerTf = playerGo ? playerGo.transform : null;

        if (playerController)
            playerController.LockControlsForSeconds(600f);

        Transform bossTf = transform;
        Transform panProxy = null;

        PlayAnimState(StateIdle);

        if (playerTf && cam)
        {
            panProxy = new GameObject("DeathCutscenePanProxy").transform;
            Vector3 fromPos = playerTf.position;
            fromPos.z = bossTf.position.z;
            Vector3 toPos = bossTf.position;
            panProxy.position = fromPos;
            cam.LockToTransform(panProxy);
            yield return BossTeleporterReveal.PanProxyLerp(panProxy, fromPos, toPos, deathPanPlayerToBossSeconds);
            cam.LockToTransform(bossTf);
        }
        else if (cam)
            cam.LockToTransform(bossTf);

        float dyingLen = GetAnimClipLength(StateDying, 0.96f);
        PlayAnimState(StateDying);
        ResolveBossAudio()?.PlayDeathSfxForAnimationDuration(dyingLen);

        yield return new WaitForSeconds(dyingLen);

        HideBossAfterDeathVisuals();

        if (healthBarUI)
            yield return healthBarUI.FadeOutForDeath(deathHealthBarFadeSeconds);

        bool teleporterPanFinished = false;
        if (postBossTeleporter && cam)
        {
            float zRef = postBossTeleporter.transform.position.z;
            Vector3 bossPos = bossTf.position;
            bossPos.z = zRef;
            Vector3 telePos = GetTeleporterPanWorldPosition(zRef);

            postBossTeleporter.SetActive(true);
            if (!panProxy)
            {
                panProxy = new GameObject("DeathCutscenePanProxy").transform;
                panProxy.position = bossPos;
            }
            else
                panProxy.position = bossPos;

            cam.LockToTransform(panProxy);
            yield return BossTeleporterReveal.PanAndFadeTeleporterReveal(panProxy, bossPos, telePos, postBossTeleporter, deathPanBossToTeleporterSeconds, deathTeleporterFadeSeconds);

            if (escapeSequenceManager)
                yield return escapeSequenceManager.RunTeleporterRevealRumble(cam);

            if (playerTf)
            {
                Vector3 fromT = telePos;
                fromT.z = playerTf.position.z;
                Vector3 toP = playerTf.position;
                panProxy.position = fromT;
                cam.LockToTransform(panProxy);
                yield return BossTeleporterReveal.PanProxyLerp(panProxy, fromT, toP, deathPanTeleporterToPlayerSeconds);
            }

            cam.LockToPlayer();
            teleporterPanFinished = true;
        }
        else if (postBossTeleporter)
        {
            postBossTeleporter.SetActive(true);
            yield return BossTeleporterReveal.FadeTeleporterInPlace(postBossTeleporter, deathTeleporterFadeSeconds);
            if (escapeSequenceManager)
                yield return escapeSequenceManager.RunTeleporterRevealRumble(cam);
        }

        if (cam && !teleporterPanFinished)
            cam.LockToPlayer();

        if (panProxy)
        {
            Destroy(panProxy.gameObject);
            panProxy = null;
        }

        if (playerController)
            playerController.UnlockControlsImmediate();

        BossAudioManager bossAudio = ResolveBossAudio();
        if (postBossTeleporter && bossAudio)
            bossAudio.BeginEscapeMusic();

        if (postBossTeleporter && escapeSequenceManager)
            escapeSequenceManager.BeginEscapePhase();

        // EnemyBase raises the kill event and destroys this object; defer that until the outro hands off.
        base.Die(context);
    }

    /// <summary>
    /// Hides all boss sprites and collision after the death animation while the outro continues on this object.
    /// </summary>
    private void HideBossAfterDeathVisuals()
    {
        if (_activeBossLaserBeam)
        {
            Destroy(_activeBossLaserBeam);
            _activeBossLaserBeam = null;
        }

        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
            sr.enabled = false;
        if (animator)
            animator.enabled = false;
        if (_rb)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false;
        }

        foreach (Collider2D c in GetComponentsInChildren<Collider2D>(true))
            c.enabled = false;
    }

    /// <summary>
    /// Uses the optional camera marker at the requested depth; an absent marker retains the origin fallback.
    /// </summary>
    private Vector3 GetTeleporterPanWorldPosition(float zWorld)
    {
        if (!postBossTeleporterPan)
            return new Vector3(0f, 0f, zWorld);

        Vector3 p = postBossTeleporterPan.position;
        p.z = zWorld;
        return p;
    }
}
