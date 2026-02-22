using UnityEngine;

public class PlayerStats : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 20f;

    [Header("Damage Tuning")]
    [Tooltip("Seconds of invulnerability after being hit.")]
    [SerializeField] private float invulnerableTime = 0.2f;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;

    private float _invulnerableUntil;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeHit(float damage, Vector2 knockbackDirection, float knockbackForce)
    {
        if (damage <= 0f) return;
        if (Time.time < _invulnerableUntil) return;

        _invulnerableUntil = Time.time + invulnerableTime;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        Debug.Log($"Player hit for {damage}. HP: {CurrentHealth}/{maxHealth}");

        if (CurrentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        Debug.Log("Player died.");
        var controller = GetComponent<PlayerController>();
        if (controller) controller.enabled = false;

        var weapons = GetComponent<PlayerWeaponController>();
        if (weapons) weapons.enabled = false;
    }

    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
    }
}