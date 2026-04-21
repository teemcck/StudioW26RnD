using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Rigidbody2D))]
public class SimpleProjectile : MonoBehaviour
{
    [Header("Visual (optional)")]
    [SerializeField] private bool enableFlightPointLight = true;
    [SerializeField] [Range(0.05f, 0.65f)] private float flightLightIntensityPlayer = 0.34f;
    [SerializeField] [Range(0.05f, 0.65f)] private float flightLightIntensityEnemy = 0.24f;
    [SerializeField] private float flightLightOuterRadius = 0.62f;
    [SerializeField] private float flightLightInnerRadius = 0.06f;

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
    private float _runtimeSpeed = -1f;
    private DamageContext _damageContext;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (!spriteRenderer)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _animator = GetComponent<Animator>();
    }

    public void Fire(Vector2 direction, float damageOverride, float knockbackOverride, DamageContext context, float speedOverride = -1f)
    {
        _dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        _runtimeDamage = damageOverride > 0f ? damageOverride : damage;
        _runtimeKnockback = knockbackOverride > 0f ? knockbackOverride : knockbackForce;
        _runtimeSpeed = speedOverride;
        _damageContext = context;
        _animTimer = 0f;
        _animFrame = 0;
        ApplyAnimationFrame(_animFrame);
        TintTrailForSource(context);
        SetupFlightLight(context);
        Destroy(gameObject, lifetime);
    }

    void SetupFlightLight(DamageContext context)
    {
        if (!enableFlightPointLight)
            return;

        var go = new GameObject("ProjectileFlightLight2D");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        var light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.blendStyleIndex = TransientPointLight2D.AdditiveBlendStyleIndex;
        light.overlapOperation = Light2D.OverlapOperation.Additive;
        light.falloffIntensity = 0.58f;
        light.pointLightInnerRadius = flightLightInnerRadius;
        light.pointLightOuterRadius = flightLightOuterRadius;

        bool player = context.WasCausedByPlayer;
        light.intensity = player ? flightLightIntensityPlayer : flightLightIntensityEnemy;
        light.color = player
            ? Color.Lerp(GameColors.SafeDash, Color.white, 0.25f)
            : new Color(0.98f, 0.55f, 1f, 1f);
        Light2DGameplayTargets.ApplyLocalAccentWithoutMapLayer(light);
        Light2DGameplayTargets.EnableAccentLightShadows(light, shadowIntensity: 0.45f, shadowSoftness: 0.25f);
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
        float moveSpeed = _runtimeSpeed > 0f ? _runtimeSpeed : speed;
        _rb.linearVelocity = _dir * moveSpeed;

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
        if (!HasMultipleFlightAnimationFrames())
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
        if (!HasFlightAnimationFrames())
            return;
        spriteRenderer.sprite = flightAnimationSprites[Mathf.Clamp(frame, 0, flightAnimationSprites.Length - 1)];
    }

    private bool HasFlightAnimationFrames()
    {
        return spriteRenderer != null &&
               flightAnimationSprites != null &&
               flightAnimationSprites.Length > 0 &&
               flightAnimationSprites[0] != null;
    }

    private bool HasMultipleFlightAnimationFrames()
    {
        return HasFlightAnimationFrames() && flightAnimationSprites.Length > 1;
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
