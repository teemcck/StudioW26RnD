using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    private PlayerStats playerStats;
    private float maxHealth;

    [Header("Damage Tuning")]
    [Tooltip("Seconds of invulnerability after being hit.")]
    [SerializeField] private float invulnerableTime = 0.2f;
    
    [Header("VFX")]
    [SerializeField] private DamageFlash damageFlash;

    private float _spawnTime;
    private bool _dead;

    public float CurrentHealth { get; private set; }

    private float _invulnerableUntil;

    private void Awake()
    {
        _spawnTime = Time.time;
        playerStats = GetComponent<PlayerStats>();
        maxHealth = playerStats.Get(PlayerStatType.MaxHealth);
        CurrentHealth = maxHealth;
        if (!damageFlash) damageFlash = GetComponent<DamageFlash>();
    }

    public void TakeHit(float damage, Vector2 knockbackDirection, float knockbackForce)
    {
        if (damage <= 0f) return;
        if (Time.time < _invulnerableUntil) return;

        _invulnerableUntil = Time.time + invulnerableTime;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        if (damageFlash) damageFlash.Play();
        Debug.Log($"Player hit for {damage}. HP: {CurrentHealth}/{maxHealth}");
        EventBus<PlayerDamagedEvent>.Raise(new PlayerDamagedEvent {Amount = damage, RemainingHP = CurrentHealth, HitPosition = transform.position, Source = "enemy"}); 
        
        if (CurrentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        if (_dead) return;
        _dead = true;
        
        Debug.Log("Player died.");
        EventBus<PlayerDiedEvent>.Raise(new PlayerDiedEvent
        {
            Position = transform.position,
            SurvivedForSeconds = Time.time - _spawnTime
        });
        var controller = GetComponent<PlayerController>();
        if (controller) controller.enabled = false;

        var weapons = GetComponent<PlayerWeaponController>();
        if (weapons) weapons.enabled = false;
    }

    public void Heal(float amount)
    {
        if (_dead) return;
        if (amount <= 0f) return;
        
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        
        EventBus<PlayerHealedEvent>.Raise(new PlayerHealedEvent {Amount = amount, NewHP = CurrentHealth});
    }
}