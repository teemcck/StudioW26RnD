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
        Destroy(gameObject);
    }
}
