using UnityEngine;

public class MeleeEnemy : EnemyBase
{
    [Header("Melee Attack")]
    [SerializeField] private float attackRange = 0.8f;
    [SerializeField] private float attackCooldown = 1.0f;
    [SerializeField] private float contactDamage = 2f;

    protected virtual void FixedUpdate()
    {
        if (!Player) return;

        Vector2 toPlayer = (Player.position - transform.position);
        float dist = toPlayer.magnitude;

        if (dist > attackRange)
        {
            Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.zero;
            Rb.AddForce(dir * moveSpeed, ForceMode2D.Force);
        }
        else
        {
            TryDealContactDamage(Player, contactDamage, attackCooldown, 0f, true, toPlayer);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryDealContactDamage(collision.collider, contactDamage, attackCooldown, 0f);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDealContactDamage(other, contactDamage, attackCooldown, 0f);
    }
}
