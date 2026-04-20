using System.Collections;
using UnityEngine;

public class SplittingEnemy : MeleeEnemy
{
    [Header("Split on death")]
    [SerializeField] private GameObject splitEnemyPrefab;
    [SerializeField] private int splitCount = 2;
    [SerializeField] private float splitHealthMultiplier = 0.45f;
    [SerializeField] private float splitSizeMultiplier = 0.7f;
    [SerializeField] private float splitSpawnRadius = 0.35f;
    [SerializeField] private int maxSplitDepth = 2;
    [SerializeField] private float splitMoveSpeedMultiplierPerDepth = 1.12f;
    [SerializeField] private float smallestSlimeContactDamageMultiplier = 0.5f;

    [Header("Slime attack")]
    [SerializeField] [Range(0.25f, 1f)] private float strikeRangeFraction = 0.58f;
    [SerializeField] [Range(0.5f, 1f)] private float minStrikeReachVsMeleeRange = 0.9f;
    [SerializeField] private float pointBlankConeBypassDistance = 0.3f;
    [SerializeField] [Range(40f, 200f)] private float strikeArcDegrees = 100f;
    [SerializeField] private float attackStartBuffer = 0.04f;

    [Header("Slime animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string idleStateName = "SlimeSplitter_Idle";
    [SerializeField] private string attackStateName = "SlimeSplitter_Attack";
    [SerializeField] private string splitStateName = "SlimeSplitter_Split";
    [SerializeField] private float attackAnimationDuration = 0.9f;
    [SerializeField] private float attackDamageMomentSeconds = 0.45f;
    [SerializeField] private float splitSpawnAtSeconds = 0.24f;
    [SerializeField] private float splitAnimationDuration = 0.5f;

    protected override bool UseContinuousContactDamageWhileInRange => false;

    private int _splitDepth;
    private bool _splitDeathRunning;
    private bool _isAttackLocked;
    private float _nextAttackTime = -999f;
    private Coroutine _attackRoutine;
    private Coroutine _splitDeathRoutine;
    private DamageContext _pendingDeathContext;
    private bool _deathWillSpawnChildren;

    private SlimeKillGroup _killGroup;

    public void AssignSlimeGroup(SlimeKillGroup group, int depth)
    {
        _killGroup = group;
        _splitDepth = depth;
    }

    protected override void Awake()
    {
        base.Awake();
        if (!animator)
            animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        _nextAttackTime = Time.time;
        if (animator && !string.IsNullOrEmpty(idleStateName))
            PlayAnimatorState(idleStateName, forceRestart: true);
    }

    private void OnDisable()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
        if (_splitDeathRoutine != null)
        {
            StopCoroutine(_splitDeathRoutine);
            _splitDeathRoutine = null;
        }
        _isAttackLocked = false;
        _splitDeathRunning = false;
    }

    public override void TakeHit(float damage, Vector2 knockbackDirection, float knockbackForce, DamageContext context = default)
    {
        if (_splitDeathRunning)
            return;
        base.TakeHit(damage, knockbackDirection, knockbackForce, context);
    }

    public override void ApplyStatusDamage(float damage, DamageContext context)
    {
        if (_splitDeathRunning)
            return;
        base.ApplyStatusDamage(damage, context);
    }

    public override void ExecuteFrailty(DamageContext context = default)
    {
        if (_splitDeathRunning)
            return;
        base.ExecuteFrailty(context);
    }

    public override void ApplyRuntimeScaling(float healthMultiplier, float sizeMultiplier = 1f)
    {
        base.ApplyRuntimeScaling(healthMultiplier, sizeMultiplier);
        if (_splitDepth <= 0)
            return;
        float rangeFactor = Mathf.Pow(splitSizeMultiplier, _splitDepth);
        float speedFactor = Mathf.Pow(splitMoveSpeedMultiplierPerDepth, _splitDepth);
        ApplySplitMeleeTuning(rangeFactor, speedFactor);
        if (_splitDepth >= maxSplitDepth)
            MultiplyMeleeContactDamage(smallestSlimeContactDamageMultiplier);
    }

    protected override void FixedUpdate()
    {
        if (_splitDeathRunning || IsDead || !Player)
            return;

        Vector2 playerContactPoint = GetPlayerClosestCombatPoint((Vector2)transform.position);
        Vector2 toPlayer = playerContactPoint - (Vector2)transform.position;
        float dist = toPlayer.magnitude;

        if (_isAttackLocked)
        {
            Rb.linearVelocity = Vector2.zero;
            return;
        }

        float biteRadius = GetBiteRadius();
        float engageDistance = biteRadius + attackStartBuffer;
        if (dist > engageDistance)
        {
            Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.zero;
            Rb.AddForce(dir * EffectiveMoveSpeed, ForceMode2D.Force);
            PlayAnimatorState(idleStateName, forceRestart: false);
            return;
        }

        Rb.linearVelocity = Vector2.zero;
        PlayAnimatorState(idleStateName, forceRestart: false);

        if (Time.time >= _nextAttackTime)
            BeginAttack(toPlayer);
    }

    private void BeginAttack(Vector2 toPlayer)
    {
        if (_attackRoutine != null)
            StopCoroutine(_attackRoutine);
        _attackRoutine = StartCoroutine(AttackRoutine(toPlayer));
    }

    private IEnumerator AttackRoutine(Vector2 toPlayerOffset)
    {
        _isAttackLocked = true;
        Rb.linearVelocity = Vector2.zero;

        PlayAnimatorState(attackStateName, forceRestart: true);

        float clipLen = Mathf.Max(0.05f, attackAnimationDuration);
        float strikeAt = Mathf.Clamp(attackDamageMomentSeconds, 0f, clipLen - 0.01f);
        float biteRadius = GetBiteRadius();
        Vector2 forward = toPlayerOffset.sqrMagnitude > 0.0001f ? toPlayerOffset.normalized : Vector2.right;
        ClearExistingStrikeArc();
        SpriteRenderer bodySr = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
        int sortLayer = bodySr != null ? bodySr.sortingLayerID : 0;
        int sortOrder = bodySr != null ? bodySr.sortingOrder + 2 : 10;
        SlimeStrikeArcVisual.Spawn(transform, forward, biteRadius, strikeArcDegrees, strikeAt, sortLayer, sortOrder);

        if (strikeAt > 0f)
            yield return new WaitForSeconds(strikeAt);

        float cooldown = MeleeAttackCooldown / Mathf.Max(0.1f, EffectiveAttackSpeedMultiplier);
        if (Player != null)
        {
            Vector2 playerContactPoint = GetPlayerClosestCombatPoint((Vector2)transform.position);
            Vector2 toNow = playerContactPoint - (Vector2)transform.position;
            float dist = toNow.magnitude;
            if (dist <= biteRadius + 0.03f)
            {
                float halfArc = strikeArcDegrees * 0.5f;
                Vector2 strikeFacing = dist > 0.0001f ? toNow / dist : forward;
                bool inCone;
                if (dist <= pointBlankConeBypassDistance)
                    inCone = true;
                else
                {
                    inCone = Vector2.Angle(forward, strikeFacing) <= halfArc + 10f;
                }

                if (inCone)
                {
                    Vector2 kb = strikeFacing;
                    TryDealContactDamage(Player, MeleeContactDamage, cooldown, 0f, true, kb);
                }
            }
        }

        float remaining = Mathf.Max(0.01f, clipLen - strikeAt);
        yield return new WaitForSeconds(remaining);

        _isAttackLocked = false;
        _attackRoutine = null;
        _nextAttackTime = Time.time + Mathf.Max(0.05f, cooldown);
        PlayAnimatorState(idleStateName, forceRestart: true);
    }

    protected override void Die(DamageContext context = default)
    {
        if (_splitDeathRunning)
            return;

        if (_killGroup == null)
            _killGroup = new SlimeKillGroup();

        _deathWillSpawnChildren = _splitDepth < maxSplitDepth && splitEnemyPrefab;
        _splitDeathRunning = true;
        _pendingDeathContext = context;

        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
        _isAttackLocked = false;

        if (Rb)
        {
            Rb.linearVelocity = Vector2.zero;
            Rb.simulated = false;
        }

        foreach (var col in GetComponents<Collider2D>())
            col.enabled = false;

        PlayAnimatorState(splitStateName, forceRestart: true);
        _splitDeathRoutine = StartCoroutine(SplitDeathRoutine());
    }

    private IEnumerator SplitDeathRoutine()
    {
        float spawnAt = Mathf.Clamp(splitSpawnAtSeconds, 0f, splitAnimationDuration);
        if (spawnAt > 0f)
            yield return new WaitForSeconds(spawnAt);

        int spawned = 0;
        if (_deathWillSpawnChildren)
            spawned = SpawnSplitChildren();

        float remainder = Mathf.Max(0f, splitAnimationDuration - spawnAt);
        if (remainder > 0f)
            yield return new WaitForSeconds(remainder);

        _splitDeathRoutine = null;
        var ctx = _pendingDeathContext;
        _splitDeathRunning = false;

        if (_killGroup == null)
            _killGroup = new SlimeKillGroup();

        if (_deathWillSpawnChildren && spawned > 0)
            _killGroup.NotifyReplacedBySplits(spawned);
        else
            _killGroup.NotifyLeafDied(this, ctx);

        DestroyWithoutKillEvent(ctx);
    }

    private int SpawnSplitChildren()
    {
        if (!splitEnemyPrefab)
            return 0;

        if (_killGroup == null)
            _killGroup = new SlimeKillGroup();

        int spawned = 0;
        float step = 360f / Mathf.Max(1, splitCount);
        for (int i = 0; i < splitCount; i++)
        {
            float rad = step * i * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * splitSpawnRadius;
            var go = Instantiate(splitEnemyPrefab, (Vector2)transform.position + offset, Quaternion.identity);
            if (go.TryGetComponent<SplittingEnemy>(out var childSplit))
                childSplit.AssignSlimeGroup(_killGroup, _splitDepth + 1);
            if (go.TryGetComponent<EnemyBase>(out var eb))
                eb.ApplyRuntimeScaling(splitHealthMultiplier, splitSizeMultiplier);
            EnsureSplitSpawnPhysics(go);
            spawned++;
        }

        return spawned;
    }

    private static void EnsureSplitSpawnPhysics(GameObject go)
    {
        if (go.TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.simulated = true;
            rb.WakeUp();
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        foreach (var col in go.GetComponents<Collider2D>())
            col.enabled = true;

        Physics2D.SyncTransforms();
    }

    private void PlayAnimatorState(string stateName, bool forceRestart)
    {
        if (!animator || string.IsNullOrEmpty(stateName))
            return;

        if (!forceRestart && animator.GetCurrentAnimatorStateInfo(0).IsName(stateName))
            return;

        animator.Play(Animator.StringToHash(stateName), 0, 0f);
    }

    private float GetBiteRadius()
    {
        return Mathf.Max(
            MeleeAttackRange * strikeRangeFraction,
            MeleeAttackRange * minStrikeReachVsMeleeRange,
            0.12f);
    }

    private void ClearExistingStrikeArc()
    {
        Transform existing = transform.Find("SlimeStrikeArc");
        if (existing != null)
            Destroy(existing.gameObject);
    }
}
