using UnityEngine;

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

    protected Rigidbody2D Rb { get; private set; }
    protected Transform Player { get; private set; }

    private float _health;
    private bool _isDead;

    public float CurrentHealth => _health;
    public float MaxHealth => maxHealth;
    public float HealthNormalized => maxHealth <= 0.0001f ? 0f : Mathf.Clamp01(_health / maxHealth);
    public bool IsDead => _isDead;

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

        if (_health <= 0f)
            Die();
    }

    protected virtual void Die()
    {
        if (_isDead) return;
        _isDead = true;
        EventBus<EnemyKilledEvent>.Raise(new EnemyKilledEvent { EnemyType = GetType().Name });
        Destroy(gameObject);
    }
}
