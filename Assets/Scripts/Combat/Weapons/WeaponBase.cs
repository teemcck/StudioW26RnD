using UnityEngine;

public abstract class WeaponBase : MonoBehaviour
{
    [Header("Base Weapon Stats")]
    [SerializeField] protected float damage = 3f;
    [SerializeField] protected float knockbackForce = 6f;
    [SerializeField] protected float range = 1f;

    protected float DamageMult { get; private set; } = 1f;
    protected float KnockbackMult { get; private set; } = 1f;
    protected float CooldownMult { get; private set; } = 1f;
    protected float RangeMult { get; private set; } = 1f;

    public float GetDamage() => damage * DamageMult;
    public float GetKnockback() => knockbackForce * KnockbackMult;
    public float GetRange() => range * RangeMult;
    public float GetCooldown() => 1f / CooldownMult;

    public abstract void Attack(Vector2 direction, LayerMask enemyLayer);
}
