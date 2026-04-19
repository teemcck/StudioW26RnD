using UnityEngine;
using System.Collections;

public class LobbingEnemy : EnemyBase
{
    [Header("Lob attack")]
    [SerializeField] private LobbingProjectile projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float horizontalShotSpeed = 2.6f;
    [SerializeField] private float projectileAimDistance = 5.5f;
    [SerializeField] private float shotMomentSeconds = 0.9f;
    [SerializeField] private float attackAnimationDuration = 1.5f;
    [SerializeField] private float attackCadenceMultiplier = 1f;

    [Header("Spacing")]
    [SerializeField] private float desiredDistance = 5.5f;
    [SerializeField] private float shootCooldown = 2f;
    [SerializeField] private float keepDistanceDeadZone = 0.35f;
    [SerializeField] private float maxShootDistance = 8.5f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private string idleStateName = "UfoLobber_Idle";
    [SerializeField] private string attackStateName = "UfoLobber_Attack";
    [SerializeField] private Color windupTint = new Color(1f, 0.6f, 0.4f, 1f);
    [SerializeField] private float windupPulseSpeed = 7f;
    [SerializeField] private float windupPulseScale = 0.08f;
    [SerializeField] private float moveFacingThreshold = 0.08f;
    [SerializeField] private float targetFacingThreshold = 0.2f;
    [SerializeField] private float minFlipInterval = 0.12f;
    [SerializeField] private bool spriteFacesRightByDefault = true;

    [Header("Polish FX")]
    [SerializeField] private bool showPreShotLandingIndicator = true;
    [SerializeField] private float preShotIndicatorPadding = 0.15f;
    [SerializeField] private bool showMuzzleFlash = true;
    [SerializeField] private Color muzzleFlashColor = new Color(1f, 0.82f, 0.4f, 0.92f);
    [SerializeField] private float muzzleFlashDuration = 0.1f;
    [SerializeField] private float muzzleFlashScale = 0.38f;
    [SerializeField] private int muzzleFlashSortingOrderOffset = 2;

    private float _nextShootTime;
    private bool _isAttackLocked;
    private bool _firedThisAttack;
    private Coroutine _attackRoutine;
    private string _currentStateName;
    private Vector2 _lockedTargetPoint;
    private int _facingSign = 1;
    private int _lockedFacingSign = 1;
    private float _lastFlipTime = -999f;
    private Color _baseColor = Color.white;
    private Vector3 _baseScale = Vector3.one;
    private GameObject _preShotIndicatorGo;
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
        DestroyPreShotIndicator();
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
            DestroyPreShotIndicator();
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

        BeginAttack(dir);
    }

    private void BeginAttack(Vector2 dirToPlayer)
    {
        float cadence = Mathf.Max(0.2f, attackCadenceMultiplier * EffectiveAttackSpeedMultiplier);
        _nextShootTime = Time.time + (shootCooldown / cadence);
        _firedThisAttack = false;
        _isAttackLocked = true;

        _lockedFacingSign = ResolveFacingSign(dirToPlayer, Rb.linearVelocity, lockDuringAttack: false);
        Vector2 lockedAimDirection = dirToPlayer.sqrMagnitude > 0.0001f ? dirToPlayer.normalized : new Vector2(_lockedFacingSign, 0f);
        _lockedTargetPoint = Player ? GetPlayerFeetWorld(Player) : (Vector2)transform.position + lockedAimDirection * Mathf.Max(1f, projectileAimDistance);
        SpawnPreShotIndicator(shotMomentSeconds + preShotIndicatorPadding);

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
        DestroyPreShotIndicator();
        PlayState(idleStateName, forceRestart: true);
        ResetVisuals();
    }

    private void TryFireLockedShot()
    {
        if (_firedThisAttack || !projectilePrefab) return;
        if (!IsPlayerInSameChunk()) return;
        _firedThisAttack = true;
        DestroyPreShotIndicator();

        Vector2 fire = firePoint ? (Vector2)firePoint.position : (Vector2)transform.position;
        var proj = Instantiate(projectilePrefab, fire, Quaternion.identity);
        proj.FireBallistic(fire, _lockedTargetPoint, horizontalShotSpeed);
        SpawnMuzzleFlash(fire);
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

    private void SpawnPreShotIndicator(float duration)
    {
        DestroyPreShotIndicator();
        if (!showPreShotLandingIndicator || !projectilePrefab) return;

        Sprite indicatorSprite = projectilePrefab.LandingTelegraphSpriteResolved;
        if (!indicatorSprite || duration <= 0.01f) return;

        var go = new GameObject("UfoLobberPreShotIndicator");
        go.transform.position = new Vector3(_lockedTargetPoint.x, _lockedTargetPoint.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = indicatorSprite;
        sr.sortingOrder = projectilePrefab.LandingTelegraphSortingOrder;
        if (projectilePrefab.LandingTelegraphSortingLayerId != 0)
            sr.sortingLayerID = projectilePrefab.LandingTelegraphSortingLayerId;

        _preShotIndicatorGo = go;
        var fx = go.AddComponent<LobLandingTelegraph>();
        fx.StartRun(sr, projectilePrefab.LandingTelegraphColor, projectilePrefab.LandingTelegraphScale, duration, projectilePrefab.LandingTelegraphPulseSpeed);
    }

    private void DestroyPreShotIndicator()
    {
        if (_preShotIndicatorGo)
        {
            Destroy(_preShotIndicatorGo);
            _preShotIndicatorGo = null;
        }
    }

    private void SpawnMuzzleFlash(Vector2 worldPos)
    {
        if (!showMuzzleFlash || muzzleFlashDuration <= 0f) return;

        var go = new GameObject("UfoLobberMuzzleFlash");
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

    // Optional hook for difficulty systems to tune cadence.
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
        DestroyPreShotIndicator();
        PlayState(idleStateName, forceRestart: true);
    }

    private bool IsPlayerInSameChunk()
    {
        if (!Player) return false;
        if (MapSpawner.Instance == null) return true;
        return MapSpawner.Instance.ArePositionsInSameChunk(transform.position, Player.position);
    }

    private static Vector2 GetPlayerFeetWorld(Transform player)
    {
        var col = player.GetComponent<Collider2D>();
        if (col == null)
            return player.position;

        Bounds b = col.bounds;
        return new Vector2(b.center.x, b.min.y);
    }

}
