using UnityEngine;

public class MeleeEnemy : EnemyBase
{
    [Header("Melee Attack")]
    [SerializeField] private float attackRange = 0.8f;
    [SerializeField] private float attackCooldown = 1.0f;
    [SerializeField] private float contactDamage = 2f;

    private float _nextAttackTime;

    private void FixedUpdate()
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
            if (Time.time >= _nextAttackTime)
            {
                _nextAttackTime = Time.time + attackCooldown;

                var playerHealth = Player.GetComponent<IDamageable>();
                if (playerHealth != null)
                    playerHealth.TakeHit(contactDamage, (Player.position - transform.position).normalized, 0f);
            }
        }
    }
}
