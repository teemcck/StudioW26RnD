using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyBase : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    [SerializeField] private float maxHealth = 10f;

    [Tooltip("Higher weight = less knockback")]
    [SerializeField] private float weight = 1f;

    [Header("Movement")]
    [SerializeField] protected float moveSpeed = 2.5f;

    [Header("VFX")]
    [SerializeField] private DamageFlash damageFlash;

    [Header("Hit Reaction")]
    [SerializeField] private float minHitReactionDuration = 0.07f;
    [SerializeField] private float maxHitReactionDuration = 0.2f;
    [SerializeField] private bool showHitPulse = true;
    [SerializeField] private Color hitPulseColor = new Color(1f, 0.9f, 0.35f, 0.9f);
    [SerializeField] private float hitPulseDuration = 0.11f;
    [SerializeField] private float hitPulseStartScale = 0.45f;
    [SerializeField] private float hitPulseEndScale = 0.9f;
    [SerializeField] private int hitPulseSortingOrderBoost = 2;

    protected Rigidbody2D Rb { get; private set; }
    protected Transform Player { get; private set; }

    private float _health;
    private bool _isDead;
    private readonly Dictionary<int, float> _contactDamageCooldownByTarget = new();
    private float _hitReactionUntil;
    private static Sprite _whiteSprite;

    public float CurrentHealth => _health;
    public float MaxHealth => maxHealth;
    public float HealthNormalized => maxHealth <= 0.0001f ? 0f : Mathf.Clamp01(_health / maxHealth);
    public bool IsDead => _isDead;
    protected bool IsInHitReaction => Time.time < _hitReactionUntil;

    /// <summary>
    /// Scales max health and current health, and optionally local scale (e.g. split-spawn clones).
    /// Call right after Instantiate; runs after Awake on the new instance.
    /// </summary>
    public virtual void ApplyRuntimeScaling(float healthMultiplier, float sizeMultiplier = 1f)
    {
        maxHealth = Mathf.Max(0.01f, maxHealth * healthMultiplier);
        _health = maxHealth;
        if (sizeMultiplier > 0f && !Mathf.Approximately(sizeMultiplier, 1f))
            transform.localScale *= sizeMultiplier;
    }

    protected virtual void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
        _health = maxHealth;

        if (!damageFlash) damageFlash = GetComponent<DamageFlash>();

        var playerGo = GameObject.FindGameObjectWithTag("Player");
        Player = playerGo ? playerGo.transform : null;
    }

    public virtual void TakeHit(float damage, Vector2 knockbackDirection, float knockbackForce)
    {
        if (_isDead) return;
        if (damage <= 0f) return;

        _health -= damage;

        if (damageFlash) damageFlash.Play();

        float safeWeight = Mathf.Max(0.05f, weight);
        Vector2 dir = knockbackDirection.sqrMagnitude > 0.0001f ? knockbackDirection.normalized : Vector2.zero;

        float impulse = knockbackForce / safeWeight;
        Rb.AddForce(dir * impulse, ForceMode2D.Impulse);

        float reactionNorm = Mathf.Clamp01(Mathf.Abs(knockbackForce) / 5f);
        float reactionDuration = Mathf.Lerp(minHitReactionDuration, maxHitReactionDuration, reactionNorm);
        _hitReactionUntil = Mathf.Max(_hitReactionUntil, Time.time + Mathf.Max(0f, reactionDuration));

        if (showHitPulse)
            SpawnHitPulse();

        OnTookHit(damage, dir, knockbackForce);

        if (_health <= 0f)
            Die();
    }

    protected virtual void OnTookHit(float damage, Vector2 direction, float knockbackForce) { }

    protected virtual void Die()
    {
        if (_isDead) return;
        _isDead = true;
        EventBus<EnemyKilledEvent>.Raise(new EnemyKilledEvent { EnemyType = GetType().Name });
        Destroy(gameObject);
    }

    protected bool TryDealContactDamage(Component hitComponent, float damage, float intervalSeconds, float knockbackForce)
    {
        return TryDealContactDamage(hitComponent, damage, intervalSeconds, knockbackForce, false, Vector2.zero);
    }

    protected bool TryDealContactDamage(Component hitComponent, float damage, float intervalSeconds, float knockbackForce, bool useOverrideDirection, Vector2 overrideDirection)
    {
        if (_isDead) return false;
        if (hitComponent == null) return false;
        if (damage <= 0f) return false;

        IDamageable damageable = hitComponent.GetComponentInParent<IDamageable>();
        Component damageableComponent = damageable as Component;
        if (damageableComponent == null) return false;

        // Enemy contact damage should only affect the tracked player.
        if (Player != null)
        {
            Transform t = damageableComponent.transform;
            if (t != Player && !t.IsChildOf(Player))
                return false;
        }

        int targetId = damageableComponent.GetInstanceID();
        if (_contactDamageCooldownByTarget.TryGetValue(targetId, out float nextAllowed) && Time.time < nextAllowed)
            return false;

        Vector2 knockbackDirection = useOverrideDirection
            ? overrideDirection
            : (Vector2)(damageableComponent.transform.position - transform.position);
        if (knockbackDirection.sqrMagnitude > 0.0001f)
            knockbackDirection.Normalize();
        else
            knockbackDirection = Vector2.up;

        damageable.TakeHit(damage, knockbackDirection, knockbackForce);
        _contactDamageCooldownByTarget[targetId] = Time.time + Mathf.Max(0.05f, intervalSeconds);
        return true;
    }

    private void SpawnHitPulse()
    {
        if (hitPulseDuration <= 0f) return;

        SpriteRenderer sourceRenderer = GetComponentInChildren<SpriteRenderer>();
        if (!sourceRenderer) return;

        var go = new GameObject("EnemyHitPulseFx");
        go.transform.position = new Vector3(transform.position.x, transform.position.y, transform.position.z - 0.001f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSprite();
        sr.color = hitPulseColor;
        sr.sortingOrder = sourceRenderer.sortingOrder + hitPulseSortingOrderBoost;
        sr.sortingLayerID = sourceRenderer.sortingLayerID;
        go.transform.localScale = Vector3.one * Mathf.Max(0.01f, hitPulseStartScale);

        StartCoroutine(HitPulseRoutine(go.transform, sr));
    }

    private IEnumerator HitPulseRoutine(Transform fxTransform, SpriteRenderer sr)
    {
        float duration = Mathf.Max(0.01f, hitPulseDuration);
        float elapsed = 0f;
        Color startColor = sr.color;
        float startScale = Mathf.Max(0.01f, hitPulseStartScale);
        float endScale = Mathf.Max(startScale + 0.01f, hitPulseEndScale);

        while (elapsed < duration && sr)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            Color c = startColor;
            c.a = Mathf.Lerp(startColor.a, 0f, t);
            sr.color = c;
            if (fxTransform)
                fxTransform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, eased);
            yield return null;
        }

        if (sr)
            Destroy(sr.gameObject);
    }

    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite) return _whiteSprite;
        Texture2D texture = Texture2D.whiteTexture;
        _whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        return _whiteSprite;
    }
}
