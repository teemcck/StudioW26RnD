using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class AOEProjectile : MonoBehaviour
{
    [SerializeField] private float travelSpeed = 5f;
    [SerializeField] private float lifetime = 4f;

    [Header("Direct hit (optional)")]
    [SerializeField] private float directDamage = 1f;
    [SerializeField] private bool dealDirectDamage;

    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 1.35f;
    [SerializeField] private float explosionDamage = 3f;
    [SerializeField] private float explosionKnockback = 5f;
    [SerializeField] private LayerMask hitMask;
    [SerializeField] private LayerMask explosionDamageMask;
    [SerializeField] private bool explodeOnExpire = true;

    [Header("Explosion visual (ground)")]
    [SerializeField] private bool showGroundExplosionVisual = true;
    [SerializeField] private float groundExplosionVisualDuration = 0.3f;
    [SerializeField] private Color groundExplosionVisualColor = new Color(1f, 0.55f, 0.15f, 0.55f);
    [SerializeField] private Sprite groundExplosionSprite;
    [SerializeField] private float groundExplosionRingScale = 1f;
    [SerializeField] private float explosionVisualDiameterMultiplier = 2.38f;
    [SerializeField] private int groundExplosionSortingOrder = 45;
    [SerializeField] private int groundExplosionSortingLayerId;

    protected Rigidbody2D RbProjectile { get; private set; }
    protected Vector2 FlightDirection { get; private set; } = Vector2.right;

    protected float ExplosionGroundRingWorldScale =>
        Mathf.Max(0.05f, groundExplosionRingScale * explosionRadius * explosionVisualDiameterMultiplier);

    protected int ExplosionGroundSortingLayerId => groundExplosionSortingLayerId;

    /// <summary>Multiply both direct and explosion damage by <paramref name="multiplier"/>.</summary>
    public void ScaleDamage(float multiplier)
    {
        if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f))
            return;

        directDamage *= multiplier;
        explosionDamage *= multiplier;
    }

    private float GetExplosionDamageRadius(float ringWorldScale)
    {
        if (groundExplosionSprite != null)
        {
            float spriteWorldWidth = groundExplosionSprite.bounds.size.x;
            return Mathf.Max(0.01f, 0.5f * ringWorldScale * spriteWorldWidth);
        }

        return Mathf.Max(0.01f, explosionRadius);
    }

    private bool _exploded;

    protected bool HasExploded => _exploded;

    protected void SetFlightDirection(Vector2 direction)
    {
        FlightDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
    }

    protected virtual void Awake()
    {
        RbProjectile = GetComponent<Rigidbody2D>();
        RbProjectile.gravityScale = 0f;
    }

    public virtual void Fire(Vector2 direction)
    {
        SetFlightDirection(direction);
        ScheduleLifetime();
    }

    protected void ScheduleLifetime()
    {
        CancelInvoke(nameof(DelayedExpire));
        Invoke(nameof(DelayedExpire), lifetime);
    }

    private void DelayedExpire()
    {
        if (_exploded) return;
        if (explodeOnExpire)
            Explode(GetExpireExplosionPosition());
        else
            Destroy(gameObject);
    }

    protected virtual Vector2 GetExpireExplosionPosition() => transform.position;

    protected virtual void FixedUpdate()
    {
        RbProjectile.linearVelocity = FlightDirection * travelSpeed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_exploded) return;
        if (((1 << other.gameObject.layer) & hitMask.value) == 0) return;

        if (dealDirectDamage)
        {
            var dmg = other.GetComponentInParent<IDamageable>();
            if (dmg != null)
                dmg.TakeHit(directDamage, FlightDirection, explosionKnockback * 0.35f);
        }

        Explode(ResolveExplosionPosition(other));
    }

    protected virtual Vector2 ResolveExplosionPosition(Collider2D other)
    {
        return transform.position;
    }

    protected virtual void OnAboutToExplode(Vector2 center) { }

    protected void Explode(Vector2 center)
    {
        if (_exploded) return;
        OnAboutToExplode(center);
        _exploded = true;
        CancelInvoke(nameof(DelayedExpire));

        float ringScale = ExplosionGroundRingWorldScale;

        if (showGroundExplosionVisual && groundExplosionSprite)
        {
            ExplosionGroundFx.Spawn(
                center,
                ringScale,
                groundExplosionVisualDuration,
                groundExplosionVisualColor,
                groundExplosionSprite,
                groundExplosionSortingOrder,
                groundExplosionSortingLayerId);
        }

        float damageRadius = GetExplosionDamageRadius(ringScale);
        var hits = Physics2D.OverlapCircleAll(center, damageRadius, explosionDamageMask);
        var damagedRoots = new HashSet<GameObject>();
        foreach (var h in hits)
        {
            if (!h) continue;
            var dmg = h.GetComponentInParent<IDamageable>();
            if (dmg is not MonoBehaviour mb) continue;
            if (!damagedRoots.Add(mb.gameObject)) continue;

            Vector2 away = (Vector2)h.transform.position - center;
            Vector2 kbDir = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up;
            dmg.TakeHit(explosionDamage, kbDir, explosionKnockback);
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
        float r = GetExplosionDamageRadius(ExplosionGroundRingWorldScale);
        Gizmos.DrawWireSphere(transform.position, r);
    }
}

public class ExplosionGroundFx : MonoBehaviour
{
    public static void Spawn(
        Vector2 worldCenter,
        float ringWorldScale,
        float duration,
        Color color,
        Sprite ringSprite,
        int sortingOrder,
        int sortingLayerId = 0)
    {
        if (!ringSprite || duration <= 0f) return;

        var go = new GameObject("GroundExplosionFx");
        go.transform.position = new Vector3(worldCenter.x, worldCenter.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ringSprite;
        sr.sortingOrder = sortingOrder;
        if (sortingLayerId != 0)
            sr.sortingLayerID = sortingLayerId;
        sr.color = color;
        go.transform.localScale = Vector3.one * ringWorldScale;

        var fx = go.AddComponent<ExplosionGroundFx>();
        fx._spriteRenderer = sr;
        fx._duration = duration;
        fx._startColor = color;
        fx._startScale = ringWorldScale;
        fx.StartCoroutine(fx.Run());
    }

    private SpriteRenderer _spriteRenderer;
    private float _duration;
    private Color _startColor;
    private float _startScale;

    private IEnumerator Run()
    {
        float t = 0f;
        while (t < _duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / _duration);
            if (!_spriteRenderer) yield break;

            var c = _startColor;
            c.a = _startColor.a * (1f - u);
            _spriteRenderer.color = c;
            transform.localScale = Vector3.one * Mathf.Lerp(_startScale, _startScale * 1.2f, u);
            yield return null;
        }

        Destroy(gameObject);
    }
}
