using UnityEngine;

public class MeleeEnemy : EnemyBase
{
    [Header("Melee Attack")]
    [SerializeField] private float attackRange = 0.8f;
    [SerializeField] private float attackCooldown = 1.0f;
    [SerializeField] private float contactDamage = 2f;

    /// <summary>When false, subclasses handle contact damage manually (e.g. animation-timed strikes).</summary>
    protected virtual bool UseContinuousContactDamageWhileInRange => true;

    protected float MeleeAttackRange => attackRange;
    protected float MeleeAttackCooldown => attackCooldown;
    protected float MeleeContactDamage => contactDamage;

    protected virtual void FixedUpdate()
    {
        if (!Player) return;

        Vector2 toPlayer = (Player.position - transform.position);
        float dist = toPlayer.magnitude;

        if (dist > attackRange)
        {
            Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.zero;
            Rb.AddForce(dir * EffectiveMoveSpeed, ForceMode2D.Force);
        }
        else if (UseContinuousContactDamageWhileInRange)
        {
            float cooldown = attackCooldown / Mathf.Max(0.1f, EffectiveAttackSpeedMultiplier);
            TryDealContactDamage(Player, contactDamage, cooldown, 0f, true, toPlayer);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!UseContinuousContactDamageWhileInRange)
            return;
        float cooldown = attackCooldown / Mathf.Max(0.1f, EffectiveAttackSpeedMultiplier);
        TryDealContactDamage(collision.collider, contactDamage, cooldown, 0f);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!UseContinuousContactDamageWhileInRange)
            return;
        float cooldown = attackCooldown / Mathf.Max(0.1f, EffectiveAttackSpeedMultiplier);
        TryDealContactDamage(other, contactDamage, cooldown, 0f);
    }
}
