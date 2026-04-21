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

<<<<<<< Updated upstream
    protected void ApplySplitMeleeTuning(float attackRangeMultiplier, float moveSpeedMultiplier)
    {
        attackRange = Mathf.Max(0.05f, attackRange * attackRangeMultiplier);
        moveSpeed *= moveSpeedMultiplier;
    }

    protected void MultiplyMeleeContactDamage(float factor)
    {
        contactDamage = Mathf.Max(0.01f, contactDamage * factor);
=======
    protected void ApplyMeleeRuntimeScaling(float rangeMultiplier, float damageMultiplier, float cooldownMultiplier = 1f)
    {
        attackRange = Mathf.Max(0.05f, attackRange * Mathf.Max(0f, rangeMultiplier));
        contactDamage = Mathf.Max(0.05f, contactDamage * Mathf.Max(0f, damageMultiplier));
        attackCooldown = Mathf.Max(0.05f, attackCooldown * Mathf.Max(0.05f, cooldownMultiplier));
>>>>>>> Stashed changes
    }

    protected virtual void FixedUpdate()
    {
        if (!Player) return;

        Vector2 playerWorld = GetPlayerCombatWorldPoint();
        Vector2 toPlayer = playerWorld - (Vector2)transform.position;
        float dist = toPlayer.magnitude;

        if (dist > attackRange)
        {
            Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.zero;
            Rb.AddForce(dir * EffectiveMoveSpeed, ForceMode2D.Force);
        }
        else if (UseContinuousContactDamageWhileInRange)
        {
            float cooldown = attackCooldown / Mathf.Max(0.1f, EffectiveAttackSpeedMultiplier);
            if (TryDealContactDamage(Player, contactDamage, cooldown, 0f, true, toPlayer))
                AudioManager.Instance?.PlayUfoAttack(0.85f);
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
