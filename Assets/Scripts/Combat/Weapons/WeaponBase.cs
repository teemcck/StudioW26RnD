using UnityEngine;

public abstract class WeaponBase : MonoBehaviour
{
    [Header("Base Weapon Stats")]
    [SerializeField] protected float damage = 3f;
    [SerializeField] protected float knockbackForce = 6f;

    protected float DamageMult { get; private set; } = 1f;
    protected float KnockbackMult { get; private set; } = 1f;
    protected float CooldownMult { get; private set; } = 1f;

    public virtual void ApplyUpgrade(WeaponUpgrade upgrade)
    {
        if (!upgrade) return;
        DamageMult *= upgrade.damageMultiplier;
        KnockbackMult *= upgrade.knockbackMultiplier;
        CooldownMult *= upgrade.cooldownMultiplier;
    }

    public float GetDamage() => damage * DamageMult;
    public float GetKnockback() => knockbackForce * KnockbackMult;

    // Called by controller when we want to attack toward a target.
    public abstract void Attack(Vector2 direction, LayerMask enemyLayer);
}
