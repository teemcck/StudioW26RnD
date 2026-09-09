using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

/// <summary>
/// Unity entry point and combat scheduler for the worm boss.
/// Feature partials share this component's lifecycle and coroutine ownership; Settings preserves serialized prefab fields.
/// BossArenaNavigation owns arena geometry, and BossTeleporterReveal owns the exit reveal operations.
/// </summary>
[RequireComponent(typeof(WormBossRoomIntro))] // BossRoomCutsceneHandler pulls the intro sequence straight from the boss.
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public sealed partial class WormBossController : EnemyBase
{
    // Constructed in Awake after resolving tilemaps; owns all arena geometry caches.
    private BossArenaNavigation _arena;

    /// <summary>
    /// Attack families available to the encounter; laser attacks join the pool in phase two.
    /// </summary>
    public enum BossAttackType
    {
        Melee,
        Shoot,
        Dig,
        Laser
    }

    /// <summary>
    /// Coarse encounter state. Coroutine and immunity flags describe temporary work within each state.
    /// </summary>
    private enum InternalState
    {
        Initializing,
        Idle,
        Attacking,
        Stunned,
        Repositioning,
        Dead
    }

    // Physics references are shared by attacks, underground movement, and feedback.
    private Rigidbody2D _rb;

    private Collider2D _mainCollider;

    // The scheduler owns attack start/finish. Priority cancellation can end child work early.
    private Coroutine _behaviorRoutine;

    private Coroutine _attackRoutine;

    // State describes the encounter; _attackActive lets the scheduler wait across coroutine boundaries.
    private InternalState _state = InternalState.Initializing;

    private BossAttackType _currentAttack;

    private bool _attackActive;

    private BossAttackType _lastAttack = BossAttackType.Melee;

    /// <summary>
    /// The encounter uses BossHealthBarUI instead of the ordinary enemy health bar.
    /// </summary>
    public override bool UsesWorldFloatingHealthBar => false;

    /// <summary>
    /// Initializes EnemyBase first, then caches scene dependencies and constructs the arena navigation module.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        _rb = GetComponent<Rigidbody2D>();
        _mainCollider = GetComponent<Collider2D>();
        _shadowCasters = GetComponentsInChildren<ShadowCaster2D>(true);
        if (!animator) animator = GetComponent<Animator>();
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer)
        {
            _baseAlpha = spriteRenderer.color.a;
            _shieldPingBaseColor = spriteRenderer.color;
            _spriteVisualBaseLocal = spriteRenderer.transform.localPosition;
            _spriteVisualBaseLocalRot = spriteRenderer.transform.localRotation;
            _spriteVisualIsChild = spriteRenderer.transform != transform;
        }
        if (!string.IsNullOrEmpty(indicatorSortingLayerName))
            _sortingLayerId = SortingLayer.NameToID(indicatorSortingLayerName);
        else if (spriteRenderer)
            _sortingLayerId = spriteRenderer.sortingLayerID;

        ResolveSceneReferences();
        _arena = new BossArenaNavigation(transform, baseTilemap, decorationTilemap,
            _mainCollider, spriteRenderer, baseArenaEdgePadding);
        _currentShield = GetMaxShieldForCurrentPhase();
    }

    /// <summary>
    /// Either reserves the encounter for its intro or starts the single combat scheduling coroutine.
    /// </summary>
    private void OnEnable()
    {
        if (waitForExternalCutscene)
        {
            _state = InternalState.Initializing;
            _introActive = true;
            return;
        }

        _state = InternalState.Idle;
        PlayAnimState(StateIdle);
        _behaviorRoutine = StartCoroutine(BehaviorLoop());
    }

    /// <summary>
    /// Stops owned combat work and removes temporary visuals so they cannot outlive the boss component.
    /// </summary>
    private void OnDisable()
    {
        if (_introActive)
        {
            _introActive = false;
            CameraController cam = phaseTransitionCamera ? phaseTransitionCamera : FindFirstObjectByType<CameraController>();
            if (cam)
                cam.LockToPlayer();
        }
        if (_behaviorRoutine != null) StopCoroutine(_behaviorRoutine);
        if (_attackRoutine != null) StopCoroutine(_attackRoutine);
        if (_damageRoutine != null) StopCoroutine(_damageRoutine);
        if (_shieldPingRoutine != null) StopCoroutine(_shieldPingRoutine);
        if (_repositionRoutine != null) StopCoroutine(_repositionRoutine);
        _repositionRoutine = null;
        if (_shieldBreakFeedbackRoutine != null) StopCoroutine(_shieldBreakFeedbackRoutine);
        _shieldBreakFeedbackRoutine = null;

        ResetSpriteVisualLocalIfChild();
        ReleaseWormLaserFireHoldPose();
        ReleasePhaseChangeHoldPose();
        if (_activeBossLaserBeam)
        {
            Destroy(_activeBossLaserBeam);
            _activeBossLaserBeam = null;
        }
        DestroyActiveIndicator();
        _attackActive = false;
        _digInvulnerable = false;
        _immuneDuringDigMove = false;
        if (_phaseTransitionRoutine != null)
        {
            StopCoroutine(_phaseTransitionRoutine);
            _phaseTransitionRoutine = null;
        }
        _phaseTransitionActive = false;
        _phaseChangePending = false;
    }

    /// <summary>
    /// Advances shield recovery and pending phase transitions only while the encounter is live.
    /// </summary>
    private void Update()
    {
        if (IsDead || _state == InternalState.Dead) return;
        if (_introActive) return;

        TickShieldRegeneration();
        if (healthBarUI)
            healthBarUI.SetHealth(HealthNormalized);
        TickPhaseTransitions();
    }

    /// <summary>
    /// Uses the scene's Base and Decoration tilemaps and inactive boss HUD when references are unassigned.
    /// </summary>
    private void ResolveSceneReferences()
    {
        if (!baseTilemap)
        {
            GameObject go = GameObject.Find("Base");
            if (go) baseTilemap = go.GetComponent<Tilemap>();
        }
        if (!decorationTilemap)
        {
            GameObject go = GameObject.Find("Decoration");
            if (go) decorationTilemap = go.GetComponent<Tilemap>();
        }
        if (!healthBarUI)
            healthBarUI = FindFirstObjectByType<BossHealthBarUI>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// Schedules repositioning, attacks, optional phase-three chains, and recovery while respecting encounter interruptions.
    /// </summary>
    private IEnumerator BehaviorLoop()
    {
        // Give other scene components a startup frame before resolving and showing the encounter HUD.
        yield return null;
        BindHealthBar();

        while (!IsDead)
        {
            if (_phaseTransitionActive)
            {
                yield return null;
                continue;
            }

            if (!Player || _state == InternalState.Dead)
            {
                yield return null;
                continue;
            }

            if (_isStunned || _attackActive)
            {
                yield return null;
                continue;
            }

            if (ShouldReposition())
            {
                _repositionRoutine = StartCoroutine(RepositionRoutine());
                yield return _repositionRoutine;
                _repositionRoutine = null;
                if (_isStunned || IsDead)
                    continue;
            }

            BossAttackType next = PickNextAttack();
            StartAttack(next);

            while (_attackActive && !_isStunned && !IsDead)
                yield return null;

            bool chained = false;
            if (CurrentPhase >= 3 && !_isStunned && !IsDead
                && Random.value < phaseThreeChainChance)
            {
                float chainBreather = Mathf.Max(0.2f, GetIdleTimeForCurrentPhase() * 0.25f);
                float gapUntil = Time.time + chainBreather;
                while (Time.time < gapUntil && !_isStunned && !IsDead)
                    yield return null;

                BossAttackType chain = PickChainAttack(next);
                StartAttack(chain);
                while (_attackActive && !_isStunned && !IsDead)
                    yield return null;
                chained = true;
            }

            float idleTime = GetIdleTimeForCurrentPhase();
            if (chained) idleTime *= phaseThreeChainCooldownMultiplier;
            float waitUntil = Time.time + idleTime;
            PlayAnimState(StateIdle);
            while (Time.time < waitUntil && !_isStunned && !IsDead)
                yield return null;
        }
    }

    /// <summary>
    /// Rolls the phase-specific reposition chance only when a player can be targeted.
    /// </summary>
    private bool ShouldReposition()
    {
        if (!Player) return false;
        float chance = GetArrayValue(repositionChanceByPhase, CurrentPhase - 1, 0.5f);
        return Random.value < chance;
    }

    /// <summary>
    /// Reads the recovery delay for the current phase, reusing the last configured entry if needed.
    /// </summary>
    private float GetIdleTimeForCurrentPhase()
    {
        return GetArrayValue(idleTimeAfterAttackByPhase, CurrentPhase - 1, 0.9f);
    }

    /// <summary>
    /// Reads phase-indexed tuning safely; empty arrays use the supplied fallback and short arrays repeat their last entry.
    /// </summary>
    private static float GetArrayValue(float[] arr, int index, float fallback)
    {
        if (arr == null || arr.Length == 0) return fallback;
        return arr[Mathf.Clamp(index, 0, arr.Length - 1)];
    }

    /// <summary>
    /// Avoids repeating the last attack with bounded rerolls, preserving the encounter's random selection order.
    /// </summary>
    private BossAttackType PickNextAttack()
    {
        List<BossAttackType> pool = BuildPhaseAttackPool(CurrentPhase);
        BossAttackType pick = pool[Random.Range(0, pool.Count)];
        int safety = 0;
        while (pick == _lastAttack && safety < 6 && pool.Count > 1)
        {
            pick = pool[Random.Range(0, pool.Count)];
            safety++;
        }
        _lastAttack = pick;
        return pick;
    }

    /// <summary>
    /// Chooses a different follow-up and prevents a laser from following a laser or dig in a chain.
    /// </summary>
    private BossAttackType PickChainAttack(BossAttackType previous)
    {
        List<BossAttackType> pool = BuildPhaseAttackPool(CurrentPhase);
        pool.RemoveAll(a => a == previous);
        if (previous == BossAttackType.Laser || previous == BossAttackType.Dig)
            pool.RemoveAll(a => a == BossAttackType.Laser);
        if (pool.Count == 0) return BossAttackType.Melee;
        return pool[Random.Range(0, pool.Count)];
    }

    /// <summary>
    /// Builds the phase's allowed attacks; melee, projectiles, and digging are always available.
    /// </summary>
    private List<BossAttackType> BuildPhaseAttackPool(int phase)
    {
        List<BossAttackType> pool = new List<BossAttackType>();
        pool.Add(BossAttackType.Melee);
        pool.Add(BossAttackType.Shoot);
        pool.Add(BossAttackType.Dig);
        if (phase >= 2) pool.Add(BossAttackType.Laser);
        return pool;
    }

    /// <summary>
    /// Marks attack ownership before starting its coroutine, so the scheduler waits for completion or cancellation.
    /// </summary>
    private void StartAttack(BossAttackType type)
    {
        _currentAttack = type;
        _attackActive = true;
        _state = InternalState.Attacking;

        IEnumerator routine = type switch
        {
            BossAttackType.Melee => MeleeRoutine(),
            BossAttackType.Shoot => ShootRoutine(),
            BossAttackType.Dig => DigRoutine(),
            BossAttackType.Laser => LaserRoutine(),
            _ => null
        };
        if (routine != null)
            _attackRoutine = StartCoroutine(routine);
    }

    /// <summary>
    /// Releases attack immunity and held animation, then returns the encounter to idle.
    /// </summary>
    private void EndAttack()
    {
        _immuneDuringDigMove = false;
        ReleaseWormLaserFireHoldPose();
        _attackActive = false;
        _attackRoutine = null;
        if (_state == InternalState.Attacking)
            _state = InternalState.Idle;
        PlayAnimState(StateIdle);
    }

    /// <summary>
    /// Cancels attacks, repositioning, and hit reactions before a shield break or phase change takes control.
    /// </summary>
    private void PriorityCancelOngoingMoves()
    {
        // Stopping a coroutine does not run its normal tail, so release poses and visuals explicitly.
        ReleaseWormLaserFireHoldPose();
        ReleasePhaseChangeHoldPose();
        DestroyLaserStretchSegments();
        if (_activeBossLaserBeam)
        {
            Destroy(_activeBossLaserBeam);
            _activeBossLaserBeam = null;
        }
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
        if (_repositionRoutine != null)
        {
            StopCoroutine(_repositionRoutine);
            _repositionRoutine = null;
        }
        if (_damageRoutine != null)
        {
            StopCoroutine(_damageRoutine);
            _damageRoutine = null;
        }
        DestroyActiveIndicator();
        _attackActive = false;
        _immuneDuringDigMove = false;
        if (_digInvulnerable)
            SetUndergroundVisuals(false);
        if (_rb)
            _rb.linearVelocity = Vector2.zero;
        if (_state == InternalState.Repositioning || _state == InternalState.Attacking)
            _state = InternalState.Idle;
    }
}
