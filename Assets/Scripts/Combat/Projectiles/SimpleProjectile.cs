using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SimpleProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 8f;
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] flightAnimationSprites;
    [SerializeField] private float animationFps = 16f;
    [SerializeField] private bool rotateToDirection = true;

    [Header("Hit")]
    [SerializeField] private float damage = 2f;
    [SerializeField] private float knockbackForce = 4f;
    [SerializeField] private LayerMask hitMask;

    public float BaseDamage => damage;

    private Rigidbody2D _rb;
    private Animator _animator;
    private Vector2 _dir;
    private float _animTimer;
    private int _animFrame;
    private float _runtimeDamage;
    private float _runtimeKnockback;
    private DamageContext _damageContext;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (!spriteRenderer)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _animator = GetComponent<Animator>();
    }

    public void Fire(Vector2 direction, float damageOverride, float knockbackOverride, DamageContext context)
    {
        _dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        _runtimeDamage = damageOverride > 0f ? damageOverride : damage;
        _runtimeKnockback = knockbackOverride > 0f ? knockbackOverride : knockbackForce;
        _damageContext = context;
        _animTimer = 0f;
        _animFrame = 0;
        ApplyAnimationFrame(_animFrame);
        TintTrailForSource(context);

        Destroy(gameObject, lifetime);
    }

    private void TintTrailForSource(DamageContext context)
    {
        var trail = GetComponent<TrailRenderer>();
        if (trail == null)
            return;

        Color color = context.WasCausedByPlayer ? GameColors.SafeDash : new Color(1f, 0.38f, 0.95f, 1f);
        trail.startColor = color;
        Color end = color;
        end.a = 0f;
        trail.endColor = end;
    }

    private void FixedUpdate()
    {
        _rb.linearVelocity = _dir * speed;

        if (rotateToDirection && _dir.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
            _rb.MoveRotation(angle);
        }

        AnimateFlight();
    }

    private void AnimateFlight()
    {
        if (_animator && _animator.runtimeAnimatorController != null)
            return;
        if (spriteRenderer == null || flightAnimationSprites == null || flightAnimationSprites.Length == 0)
            return;

        float fps = Mathf.Max(0.1f, animationFps);
        _animTimer += Time.fixedDeltaTime;
        float frameDuration = 1f / fps;
        while (_animTimer >= frameDuration)
        {
            _animTimer -= frameDuration;
            _animFrame = (_animFrame + 1) % flightAnimationSprites.Length;
            ApplyAnimationFrame(_animFrame);
        }
    }

    private void ApplyAnimationFrame(int frame)
    {
        if (spriteRenderer == null || flightAnimationSprites == null || flightAnimationSprites.Length == 0)
            return;
        spriteRenderer.sprite = flightAnimationSprites[Mathf.Clamp(frame, 0, flightAnimationSprites.Length - 1)];
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & hitMask.value) == 0)
            return;

        var dmg = other.GetComponentInParent<IDamageable>();
        if (dmg != null)
            dmg.TakeHit(_runtimeDamage > 0f ? _runtimeDamage : damage, _dir, _runtimeKnockback > 0f ? _runtimeKnockback : knockbackForce, _damageContext);
        
        Destroy(gameObject);
    }
}
