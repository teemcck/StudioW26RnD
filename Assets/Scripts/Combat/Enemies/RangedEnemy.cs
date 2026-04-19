using System.Collections;
using UnityEngine;

public class RangedEnemy : EnemyBase
{
    [Header("Straight Shot")]
    [SerializeField] private SimpleProjectile projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float shotMomentSeconds = 0.66f;
    [SerializeField] private float attackAnimationDuration = 1.1f;
    [SerializeField] private float attackCadenceMultiplier = 1f;
    [SerializeField] private Vector2 playerAimOffset = Vector2.zero;

    [Header("Spacing")]
    [SerializeField] private float desiredDistance = 4.2f;
    [SerializeField] private float shootCooldown = 1.25f;
    [SerializeField] private float keepDistanceDeadZone = 0.3f;
    [SerializeField] private float maxShootDistance = 7.5f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private string idleStateName = "UfoStraightShooter_Idle";
    [SerializeField] private string attackStateName = "UfoStraightShooter_Attack";
    [SerializeField] private Color windupTint = new Color(1f, 0.62f, 0.45f, 1f);
    [SerializeField] private float windupPulseSpeed = 7f;
    [SerializeField] private float windupPulseScale = 0.08f;
    [SerializeField] private float moveFacingThreshold = 0.08f;
    [SerializeField] private float targetFacingThreshold = 0.2f;
    [SerializeField] private float minFlipInterval = 0.12f;
    [SerializeField] private bool spriteFacesRightByDefault = true;

    [Header("Polish FX")]
    [SerializeField] private bool showMuzzleFlash = true;
    [SerializeField] private Color muzzleFlashColor = new Color(1f, 0.86f, 0.5f, 0.92f);
    [SerializeField] private float muzzleFlashDuration = 0.08f;
    [SerializeField] private float muzzleFlashScale = 0.32f;
    [SerializeField] private int muzzleFlashSortingOrderOffset = 2;

    private float _nextShootTime;
    private bool _isAttackLocked;
    private bool _firedThisAttack;
    private Coroutine _attackRoutine;
    private string _currentStateName;
    private Vector2 _lockedShootDirection = Vector2.right;
    private int _facingSign = 1;
    private int _lockedFacingSign = 1;
    private float _lastFlipTime = -999f;
    private Color _baseColor = Color.white;
    private Vector3 _baseScale = Vector3.one;
    private static Sprite _whiteSprite;

    protected override void Awake()
    {
        base.Awake();
        if (!firePoint)
            firePoint = transform;
        if (!animator) animator = GetComponent<Animator>();
        if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer) _baseColor = spriteRenderer.color;
        _baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        _facingSign = 1;
        _lockedFacingSign = 1;
        _lastFlipTime = -999f;
        _isAttackLocked = false;
        _firedThisAttack = false;
        ResetVisuals();
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
        _firedThisAttack = false;
        ResetVisuals();
    }

    private void FixedUpdate()
    {
        if (!Player || IsDead) return;

        Vector2 toPlayer = (Vector2)(Player.position - transform.position);
        float dist = toPlayer.magnitude;
        Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.zero;

        UpdateFacing(toPlayer, Rb.linearVelocity);

        if (!IsPlayerInSameChunk())
            return;

        if (IsInHitReaction)
        {
            _isAttackLocked = false;
            _firedThisAttack = false;
            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
            }
            PlayState(idleStateName, forceRestart: true);
            return;
        }

        if (_isAttackLocked)
        {
            Rb.linearVelocity = Vector2.zero;
            return;
        }

        if (dist > desiredDistance + keepDistanceDeadZone)
            Rb.AddForce(dir * EffectiveMoveSpeed, ForceMode2D.Force);
        else if (dist < desiredDistance - keepDistanceDeadZone)
            Rb.AddForce(-dir * EffectiveMoveSpeed * 0.8f, ForceMode2D.Force);
        else
            Rb.linearVelocity = Vector2.Lerp(Rb.linearVelocity, Vector2.zero, 0.2f);

        PlayState(idleStateName, forceRestart: false);

        if (Time.time < _nextShootTime || !projectilePrefab) return;
        if (dist > maxShootDistance) return;

        BeginAttack();
    }

    private void BeginAttack()
    {
        float cadence = Mathf.Max(0.2f, attackCadenceMultiplier * EffectiveAttackSpeedMultiplier);
        _nextShootTime = Time.time + (shootCooldown / cadence);
        _firedThisAttack = false;
        _isAttackLocked = true;

        Vector2 toPlayer = (Vector2)(Player.position - transform.position);
        _lockedFacingSign = ResolveFacingSign(toPlayer, Rb.linearVelocity, lockDuringAttack: false);

        Vector2 aim = GetPlayerAimWorldPoint(Player);
        Vector2 fire = firePoint ? (Vector2)firePoint.position : (Vector2)transform.position;
        Vector2 shootDir = (aim - fire);
        _lockedShootDirection = shootDir.sqrMagnitude > 0.0001f ? shootDir.normalized : new Vector2(_lockedFacingSign, 0f);

        if (_attackRoutine != null)
            StopCoroutine(_attackRoutine);
        _attackRoutine = StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        Rb.linearVelocity = Vector2.zero;
        PlayState(attackStateName, forceRestart: true);

        float shotDelay = Mathf.Clamp(shotMomentSeconds, 0f, attackAnimationDuration);
        if (shotDelay > 0f)
            yield return WindupRoutine(shotDelay);

        TryFireLockedShot();

        float remain = Mathf.Max(0.01f, attackAnimationDuration - shotDelay);
        yield return new WaitForSeconds(remain);

        _isAttackLocked = false;
        _attackRoutine = null;
        PlayState(idleStateName, forceRestart: true);
        ResetVisuals();
    }

    private void TryFireLockedShot()
    {
        if (_firedThisAttack || !projectilePrefab) return;
        if (!IsPlayerInSameChunk()) return;

        _firedThisAttack = true;
        Vector2 fire = firePoint ? (Vector2)firePoint.position : (Vector2)transform.position;
        var proj = Instantiate(projectilePrefab, fire, Quaternion.identity);
        proj.Fire(_lockedShootDirection, 0f, 0f, new DamageContext(gameObject, gameObject, AttackKind.Ranged, "enemy_projectile"));
        SpawnMuzzleFlash(fire);
    }

    private Vector2 GetPlayerAimWorldPoint(Transform player)
    {
        if (!player) return transform.position;
        var col = player.GetComponent<Collider2D>();
        if (col != null)
            return (Vector2)col.bounds.center + playerAimOffset;
        return (Vector2)player.position + playerAimOffset;
    }

    private IEnumerator WindupRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * Mathf.Max(0.1f, windupPulseSpeed) * Mathf.PI * 2f);
            float ramp = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
            float strength = pulse * Mathf.Lerp(0.25f, 1f, ramp);

            if (spriteRenderer)
                spriteRenderer.color = Color.Lerp(_baseColor, windupTint, strength);

            transform.localScale = _baseScale * (1f + windupPulseScale * pulse);
            yield return null;
        }
    }

    private void UpdateFacing(Vector2 toPlayer, Vector2 velocity)
    {
        if (!spriteRenderer) return;

        int desiredSign = ResolveFacingSign(toPlayer, velocity, _isAttackLocked);
        if (desiredSign == 0) return;

        bool signChanged = desiredSign != _facingSign;
        bool canFlip = Time.time >= (_lastFlipTime + Mathf.Max(0f, minFlipInterval));
        if (signChanged && !canFlip) return;

        if (signChanged)
            _lastFlipTime = Time.time;

        _facingSign = desiredSign;
        spriteRenderer.flipX = spriteFacesRightByDefault ? (_facingSign < 0) : (_facingSign > 0);
    }

    private int ResolveFacingSign(Vector2 toPlayer, Vector2 velocity, bool lockDuringAttack)
    {
        if (lockDuringAttack)
            return _lockedFacingSign;

        if (Mathf.Abs(velocity.x) >= Mathf.Max(0.001f, moveFacingThreshold))
            return velocity.x < 0f ? -1 : 1;

        if (Mathf.Abs(toPlayer.x) >= Mathf.Max(0.001f, targetFacingThreshold))
            return toPlayer.x < 0f ? -1 : 1;

        return _facingSign;
    }

    private void PlayState(string stateName, bool forceRestart)
    {
        if (!animator) return;
        if (!forceRestart && _currentStateName == stateName) return;

        _currentStateName = stateName;
        animator.Play(stateName, 0, 0f);
    }

    private void ResetVisuals()
    {
        transform.localScale = _baseScale;
        if (spriteRenderer)
            spriteRenderer.color = _baseColor;
    }

    private void SpawnMuzzleFlash(Vector2 worldPos)
    {
        if (!showMuzzleFlash || muzzleFlashDuration <= 0f) return;

        var go = new GameObject("UfoStraightShooterMuzzleFlash");
        go.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSprite();
        sr.color = muzzleFlashColor;
        sr.sortingOrder = spriteRenderer ? spriteRenderer.sortingOrder + muzzleFlashSortingOrderOffset : 10;
        if (spriteRenderer) sr.sortingLayerID = spriteRenderer.sortingLayerID;
        go.transform.localScale = Vector3.one * Mathf.Max(0.05f, muzzleFlashScale);
        StartCoroutine(FadeAndDestroy(sr, Mathf.Max(0.02f, muzzleFlashDuration)));
    }

    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite) return _whiteSprite;
        Texture2D texture = Texture2D.whiteTexture;
        _whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        return _whiteSprite;
    }

    private static IEnumerator FadeAndDestroy(SpriteRenderer sr, float duration)
    {
        if (!sr) yield break;

        float elapsed = 0f;
        Color start = sr.color;
        Vector3 startScale = sr.transform.localScale;
        while (elapsed < duration && sr)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Color c = start;
            c.a = Mathf.Lerp(start.a, 0f, t);
            sr.color = c;
            sr.transform.localScale = startScale * Mathf.Lerp(1f, 1.45f, t);
            yield return null;
        }

        if (sr) Destroy(sr.gameObject);
    }

    public void SetAttackCadenceMultiplier(float multiplier)
    {
        attackCadenceMultiplier = Mathf.Max(0.2f, multiplier);
    }

    protected override void OnTookHit(float damage, Vector2 direction, float knockbackForce)
    {
        _isAttackLocked = false;
        _firedThisAttack = false;
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
        PlayState(idleStateName, forceRestart: true);
    }

    private bool IsPlayerInSameChunk()
    {
        if (!Player) return false;
        if (MapSpawner.Instance == null) return true;
        return MapSpawner.Instance.ArePositionsInSameChunk(transform.position, Player.position);
    }
}
