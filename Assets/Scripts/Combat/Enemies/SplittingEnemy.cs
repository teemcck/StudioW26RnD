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

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string idleStateName = "SlimeSplitter_Idle";
    [SerializeField] private string attackStateName = "SlimeSplitter_Attack";
    [SerializeField] private string splitStateName = "SlimeSplitter_Split";

    [Tooltip("Length of SlimeSplitter_Attack clip (seconds).")]
    [SerializeField] private float attackAnimationDuration = 0.9f;

    [Tooltip("Time within the attack clip when the Attack frame deals contact damage.")]
    [SerializeField] private float attackDamageMomentSeconds = 0.45f;

    [Tooltip("Time from start of split clip when mini-slimes spawn (Split 3 frame).")]
    [SerializeField] private float splitSpawnAtSeconds = 0.24f;

    [Tooltip("Total length of SlimeSplitter_Split clip (seconds).")]
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

    public void SetSplitDepth(int depth) => _splitDepth = depth;

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

    protected override void FixedUpdate()
    {
        if (_splitDeathRunning || IsDead || !Player)
            return;

        Vector2 toPlayer = (Vector2)(Player.position - transform.position);
        float dist = toPlayer.magnitude;

        if (_isAttackLocked)
        {
            Rb.linearVelocity = Vector2.zero;
            return;
        }

        if (dist > MeleeAttackRange)
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

    private IEnumerator AttackRoutine(Vector2 toPlayer)
    {
        _isAttackLocked = true;
        Rb.linearVelocity = Vector2.zero;

        PlayAnimatorState(attackStateName, forceRestart: true);

        float clipLen = Mathf.Max(0.05f, attackAnimationDuration);
        float strikeAt = Mathf.Clamp(attackDamageMomentSeconds, 0f, clipLen - 0.01f);
        if (strikeAt > 0f)
            yield return new WaitForSeconds(strikeAt);

        float cooldown = MeleeAttackCooldown / Mathf.Max(0.1f, EffectiveAttackSpeedMultiplier);
        TryDealContactDamage(Player, MeleeContactDamage, cooldown, 0f, true, toPlayer);

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

        if (_deathWillSpawnChildren)
            SpawnSplitChildren();

        float remainder = Mathf.Max(0f, splitAnimationDuration - spawnAt);
        if (remainder > 0f)
            yield return new WaitForSeconds(remainder);

        _splitDeathRoutine = null;
        var ctx = _pendingDeathContext;
        _splitDeathRunning = false;
        base.Die(ctx);
    }

    private void SpawnSplitChildren()
    {
        if (!splitEnemyPrefab)
            return;

        float step = 360f / Mathf.Max(1, splitCount);
        for (int i = 0; i < splitCount; i++)
        {
            float rad = step * i * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * splitSpawnRadius;
            var go = Instantiate(splitEnemyPrefab, (Vector2)transform.position + offset, Quaternion.identity);
            if (go.TryGetComponent<EnemyBase>(out var eb))
                eb.ApplyRuntimeScaling(splitHealthMultiplier, splitSizeMultiplier);
            if (go.TryGetComponent<SplittingEnemy>(out var childSplit))
                childSplit.SetSplitDepth(_splitDepth + 1);
            EnsureSplitSpawnPhysics(go);
        }
    }

    /// <summary>
    /// Split clones are scaled at runtime; ensure RB/colliders are simulated and physics state is current.
    /// </summary>
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
}
