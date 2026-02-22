using UnityEngine;

public class MeleeSwingWeapon : WeaponBase
{
    [Header("Swing Shape")]
    [SerializeField] private float hitRadius = 1.0f;

    [Tooltip("Angle of the swing cone in degrees (centered on attack direction).")]
    [Range(10f, 180f)]
    [SerializeField] private float coneAngle = 90f;

    [Header("VFX")]
    [SerializeField] private Animator swingVfxAnimator;
    [SerializeField] private string playTriggerName = "Swing";

    public override void Attack(Vector2 direction, LayerMask enemyLayer)
    {
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.right;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        if (swingVfxAnimator)
            swingVfxAnimator.SetTrigger(playTriggerName);

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, hitRadius, enemyLayer);

        float cosThreshold = Mathf.Cos((coneAngle * 0.5f) * Mathf.Deg2Rad);
        float dmg = GetDamage();
        float kb = GetKnockback();

        foreach (var h in hits)
        {
            if (!h) continue;

            Vector2 toEnemy = (h.transform.position - transform.position);
            Vector2 toEnemyDir = toEnemy.sqrMagnitude > 0.0001f ? toEnemy.normalized : Vector2.zero;

            if (Vector2.Dot(direction.normalized, toEnemyDir) < cosThreshold)
                continue;

            var damageable = h.GetComponentInParent<IDamageable>();
            if (damageable != null)
                damageable.TakeHit(dmg, direction, kb);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, hitRadius);
    }
}