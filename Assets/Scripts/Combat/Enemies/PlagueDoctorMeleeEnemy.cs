using System.Collections;
using UnityEngine;

public class PlagueDoctorMeleeEnemy : EnemyBase
{
    [Header("Melee Attack")]
    [SerializeField] private float attackRange = 0.9f;
    [SerializeField] private float attackCooldown = 0.7f;
    [SerializeField] private float contactDamage = 2f;
    [SerializeField] private float chaseForceMultiplier = 1f;
    [SerializeField] private float attackAnimationDuration = 1.05f;
    [SerializeField] private float vulnerableDuration = 1.5f;
    [Tooltip("Extra damage taken from the player while in the vulnerable recovery anim (1 = no bonus).")]
    [SerializeField] private float vulnerableIncomingDamageMultiplier = 1.25f;
    [SerializeField] private float damageMomentSeconds = 0.9f;
    [SerializeField] private float swingHalfAngleDegrees = 55f;
    [SerializeField] private float swingReach = 1f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private string idleStateName = "PlagueDoctor_Idle";
    [SerializeField] private string attackStateName = "PlagueDoctor_Attack";
    [SerializeField] private string vulnerableStateName = "PlagueDoctor_Vulnerable";

    [Header("Telegraphing")]
    [SerializeField] private Color windupTint = new Color(1f, 0.5f, 0.5f, 1f);
    [SerializeField] private float windupPulseSpeed = 8f;
    [SerializeField] private float windupPulseScale = 0.12f;
    [SerializeField] private float moveFacingThreshold = 0.08f;
    [SerializeField] private float targetFacingThreshold = 0.2f;
    [SerializeField] private float minFlipInterval = 0.12f;
    [SerializeField] private bool spriteFacesRightByDefault = false;

    [Header("Lunge")]
    [SerializeField] private float lungeDistance = 0.9f;
    [SerializeField] private float lungeDuration = 0.22f;

    [Header("Swing Slash VFX")]
    [SerializeField] private bool enableSwingSlashVfx = true;
    [SerializeField] private Transform slashVfxTransform;
    [SerializeField] private Animator slashVfxAnimator;
    [SerializeField] private SpriteRenderer slashVfxRenderer;
    [SerializeField] private string slashStatePrefix = "SwingVFX";
    [SerializeField] private float slashDistance = 0.95f;
    [SerializeField] private float slashLifetime = 0.18f;
    [SerializeField] private float slashScale = 1.35f;
    [SerializeField] private Color slashColor = new Color(1f, 0.82f, 0.82f, 1f);

    private float _nextAttackTime;
    private bool _isAttackLocked;
    private bool _isVulnerable;
    private Coroutine _attackRoutine;
    private string _currentStateName;
    private Vector2 _lastFacingDirection = Vector2.right;
    private Color _baseColor = Color.white;
    private Vector3 _baseScale = Vector3.one;
    private float _lastFlipTime = -999f;
    private int _facingSign = 1;
    private int _lockedFacingSign = 1;
    private Coroutine _slashVfxCo;
    private float _nextAmbientTime;

    protected override void Awake()
    {
        base.Awake();
        if (!animator) animator = GetComponent<Animator>();
        if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (!slashVfxTransform)
        {
            Transform child = transform.Find("PlagueDoctorSlashVFX");
            if (child) slashVfxTransform = child;
        }
        if (!slashVfxAnimator && slashVfxTransform)
            slashVfxAnimator = slashVfxTransform.GetComponent<Animator>();
        if (!slashVfxRenderer && slashVfxTransform)
            slashVfxRenderer = slashVfxTransform.GetComponent<SpriteRenderer>();
        if (slashVfxRenderer)
            slashVfxRenderer.color = slashColor;
        if (slashVfxTransform)
            slashVfxTransform.gameObject.SetActive(false);
        if (spriteRenderer) _baseColor = spriteRenderer.color;
        _baseScale = transform.localScale;
    }

    protected override void OnRuntimeScalingApplied()
    {
        _baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        ResetVisuals();
        _facingSign = 1;
        _lockedFacingSign = 1;
        _lastFlipTime = -999f;
        _nextAmbientTime = Time.time + Random.Range(2.5f, 5.5f);
        PlayState(idleStateName, forceRestart: true);
    }

    private void OnDisable()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }

        _isAttackLocked = false;
        _isVulnerable = false;
        if (_slashVfxCo != null)
        {
            StopCoroutine(_slashVfxCo);
            _slashVfxCo = null;
        }
        if (slashVfxTransform) slashVfxTransform.gameObject.SetActive(false);
        ResetVisuals();
    }

    private void FixedUpdate()
    {
        if (!Player || IsDead) return;

        if (!IsPlayerOnSameChunk())
        {
            Rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 toPlayer = (Vector2)(Player.position - transform.position);
        float distanceToPlayer = toPlayer.magnitude;

        UpdateFacing(toPlayer);
        TryPlayAmbient(distanceToPlayer);

        if (_isAttackLocked)
        {
            Rb.linearVelocity = Vector2.zero;
            return;
        }

        if (distanceToPlayer > attackRange)
        {
            Vector2 direction = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.zero;
            Rb.AddForce(direction * EffectiveMoveSpeed * chaseForceMultiplier, ForceMode2D.Force);
            PlayState(idleStateName, forceRestart: false);
            return;
        }

        Rb.linearVelocity = Vector2.zero;
        if (Time.time >= _nextAttackTime)
            BeginAttack(toPlayer);
    }

    private void BeginAttack(Vector2 toPlayer)
    {
        _lockedFacingSign = ResolveFacingSign(toPlayer, Rb.linearVelocity, lockDuringAttack: false);
        AudioManager.Instance?.PlayPlagueLaugh();

        if (_attackRoutine != null)
            StopCoroutine(_attackRoutine);

        _attackRoutine = StartCoroutine(AttackRoutine(toPlayer));
    }

    private IEnumerator AttackRoutine(Vector2 attackDirection)
    {
        _isAttackLocked = true;
        _isVulnerable = false;
        Rb.linearVelocity = Vector2.zero;
        PlayState(attackStateName, forceRestart: true);

        float damageDelay = Mathf.Clamp(damageMomentSeconds, 0f, attackAnimationDuration);
        float lungeStartAt = Mathf.Max(0f, damageDelay - lungeDuration * 0.75f);

        if (lungeStartAt > 0f)
            yield return WindupRoutine(lungeStartAt);

        Vector2 lungeDir = attackDirection.sqrMagnitude > 0.0001f ? attackDirection.normalized : _lastFacingDirection;
        Vector2 lungeStart = Rb.position;
        Vector2 lungeEnd = lungeStart + lungeDir * Mathf.Max(0f, lungeDistance);
        float strikeMoment = Mathf.Clamp01((damageDelay - lungeStartAt) / Mathf.Max(0.0001f, lungeDuration));
        bool struck = false;

        float lt = 0f;
        while (lt < lungeDuration)
        {
            lt += Time.deltaTime;
            float u = Mathf.Clamp01(lt / lungeDuration);
            float eased = 1f - Mathf.Pow(1f - u, 3f);
            Rb.MovePosition(Vector2.Lerp(lungeStart, lungeEnd, eased));

            if (!struck && u >= strikeMoment)
            {
                struck = true;
                AudioManager.Instance?.PlayPlagueAttackAt(transform.position);
                TryApplyDirectionalSwingDamage(attackDirection);
                ApplySwingImpactVisual();
                PlaySwingSlashVfx(attackDirection);
            }

            yield return null;
        }

        if (!struck)
        {
            AudioManager.Instance?.PlayPlagueAttackAt(transform.position);
            TryApplyDirectionalSwingDamage(attackDirection);
            ApplySwingImpactVisual();
            PlaySwingSlashVfx(attackDirection);
        }

        float remaining = Mathf.Max(0.01f, attackAnimationDuration - (lungeStartAt + lungeDuration));
        yield return new WaitForSeconds(remaining);

        _isVulnerable = true;
        PlayState(vulnerableStateName, forceRestart: true);
        yield return new WaitForSeconds(Mathf.Max(0.05f, vulnerableDuration));

        _isVulnerable = false;
        _isAttackLocked = false;
        float cooldown = attackCooldown / Mathf.Max(0.1f, EffectiveAttackSpeedMultiplier);
        _nextAttackTime = Time.time + Mathf.Max(0.05f, cooldown);
        _attackRoutine = null;
        PlayState(idleStateName, forceRestart: true);
        ResetVisuals();
    }

    public override void TakeHit(float damage, Vector2 knockbackDirection, float knockbackForce, DamageContext context = default)
    {
        if (_isVulnerable && context.WasCausedByPlayer && vulnerableIncomingDamageMultiplier > 1.0001f)
            damage *= vulnerableIncomingDamageMultiplier;
        base.TakeHit(damage, knockbackDirection, knockbackForce, context);
    }

    private void TryApplyDirectionalSwingDamage(Vector2 attackDirection)
    {
        if (!Player) return;
        if (contactDamage <= 0f) return;

        Vector2 forward = attackDirection.sqrMagnitude > 0.0001f ? attackDirection.normalized : _lastFacingDirection;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector2.right;

        Vector2 toPlayer = (Vector2)(Player.position - transform.position);
        float distance = toPlayer.magnitude;
        if (distance > Mathf.Max(0.1f, swingReach)) return;

        Vector2 toPlayerDir = distance > 0.0001f ? toPlayer / distance : forward;
        float dot = Vector2.Dot(forward, toPlayerDir);
        float minDot = Mathf.Cos(Mathf.Clamp(swingHalfAngleDegrees, 1f, 89f) * Mathf.Deg2Rad);
        if (dot < minDot) return;

        float cooldown = (attackCooldown / Mathf.Max(0.1f, EffectiveAttackSpeedMultiplier)) + vulnerableDuration;
        TryDealContactDamage(Player, contactDamage, cooldown, 0f, true, forward);
    }

    private void UpdateFacing(Vector2 toPlayer)
    {
        if (!spriteRenderer) return;

        int desiredSign = ResolveFacingSign(toPlayer, Rb ? Rb.linearVelocity : Vector2.zero, _isAttackLocked);
        if (desiredSign == 0) return;

        bool signChanged = desiredSign != _facingSign;
        bool canFlip = Time.time >= (_lastFlipTime + Mathf.Max(0f, minFlipInterval));
        if (signChanged && !canFlip) return;

        if (signChanged)
            _lastFlipTime = Time.time;

        _facingSign = desiredSign;
        _lastFacingDirection = new Vector2(_facingSign, 0f);
        // If art faces left by default, flip when moving right.
        spriteRenderer.flipX = spriteFacesRightByDefault ? (_facingSign < 0) : (_facingSign > 0);
    }

    private int ResolveFacingSign(Vector2 toPlayer, Vector2 velocity, bool lockDuringAttack)
    {
        if (lockDuringAttack)
            return _lockedFacingSign;

        // Primary source: actual movement direction.
        if (Mathf.Abs(velocity.x) >= Mathf.Max(0.001f, moveFacingThreshold))
            return velocity.x < 0f ? -1 : 1;

        // Fallback source: player position, but with a larger dead zone near center.
        if (Mathf.Abs(toPlayer.x) >= Mathf.Max(0.001f, targetFacingThreshold))
            return toPlayer.x < 0f ? -1 : 1;

        // Keep last stable facing when inputs are ambiguous.
        return _facingSign;
    }

    private void PlayState(string stateName, bool forceRestart)
    {
        if (!animator) return;
        if (!forceRestart && _currentStateName == stateName) return;

        _currentStateName = stateName;
        animator.Play(stateName, 0, 0f);
    }

    private IEnumerator WindupRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * Mathf.Max(0.1f, windupPulseSpeed) * Mathf.PI * 2f);

            float intensity = Mathf.Lerp(0.25f, 1f, t) * pulse;
            if (spriteRenderer)
                spriteRenderer.color = Color.Lerp(_baseColor, windupTint, intensity);

            float scalePulse = 1f + (windupPulseScale * pulse);
            transform.localScale = _baseScale * scalePulse;
            yield return null;
        }
    }

    private void ApplySwingImpactVisual()
    {
        if (spriteRenderer)
            spriteRenderer.color = Color.white;
        transform.localScale = _baseScale * (1f + windupPulseScale * 1.4f);
    }

    private void PlaySwingSlashVfx(Vector2 attackDirection)
    {
        if (!enableSwingSlashVfx) return;
        if (!slashVfxTransform || !slashVfxAnimator || !slashVfxRenderer) return;

        Vector2 dir = attackDirection.sqrMagnitude > 0.0001f ? attackDirection.normalized : _lastFacingDirection;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.right;
        Vector2 snappedDir = SnapTo8Directions(dir);
        if (_slashVfxCo != null)
            StopCoroutine(_slashVfxCo);
        _slashVfxCo = StartCoroutine(PlaySwingSlashVfxRoutine(snappedDir));
    }

    private IEnumerator PlaySwingSlashVfxRoutine(Vector2 direction)
    {
        float distance = Mathf.Max(0.05f, slashDistance);
        slashVfxTransform.localPosition = new Vector3(direction.x * distance, direction.y * distance, slashVfxTransform.localPosition.z);
        slashVfxTransform.localScale = Vector3.one * Mathf.Max(0.01f, slashScale);
        slashVfxRenderer.color = slashColor;
        slashVfxTransform.gameObject.SetActive(true);

        string stateName = $"{slashStatePrefix}_{BuildDirectionSuffix(direction)}";
        if (slashVfxAnimator.HasState(0, Animator.StringToHash(stateName)))
            slashVfxAnimator.Play(stateName, 0, 0f);

        yield return new WaitForSeconds(Mathf.Max(0.05f, slashLifetime));
        if (slashVfxTransform)
            slashVfxTransform.gameObject.SetActive(false);
        _slashVfxCo = null;
    }

    private static Vector2 SnapTo8Directions(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return Vector2.right;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        angle = Mathf.Round(angle / 45f) * 45f;
        float rad = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
    }

    private static string BuildDirectionSuffix(Vector2 dir)
    {
        if (dir.y >= 0.6f && dir.x >= 0.3f) return "UR";
        if (dir.y <= -0.6f && dir.x >= 0.3f) return "DR";
        if (dir.y >= 0.6f && dir.x <= -0.3f) return "UL";
        if (dir.y <= -0.6f && dir.x <= -0.3f) return "DL";
        if (dir.x >= 0.6f) return "R";
        if (dir.x <= -0.6f) return "L";
        if (dir.y >= 0f) return "U";
        return "D";
    }

    private void ResetVisuals()
    {
        transform.localScale = _baseScale;
        if (spriteRenderer)
            spriteRenderer.color = _baseColor;
    }

    private void TryPlayAmbient(float distanceToPlayer)
    {
        if (Time.time < _nextAmbientTime)
            return;

        if (distanceToPlayer > 6.5f)
            return;

        _nextAmbientTime = Time.time + Random.Range(6f, 11f);
        AudioManager.Instance?.PlayPlagueAmbient(Random.Range(0.35f, 0.6f));
    }
}
