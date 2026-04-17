using UnityEngine;

public class MeleeSwingWeapon : WeaponBase
{
    [Header("Swing Shape")]
    [SerializeField] private float hitRadius = 0.85f;
    [SerializeField] private float hitOriginRangeFactor = 0.7f;
    [SerializeField] private float swingVfxRangeFactor = 0.95f;

    [Tooltip("Angle of the swing cone in degrees (centered on attack direction).")]
    [Range(10f, 180f)]
    [SerializeField] private float coneAngle = 80f;

    [Header("VFX")]
    [SerializeField] private SpriteRenderer swingVfxRenderer;
    [SerializeField] private Animator swingVfxAnimator;
    [SerializeField] private string swingVfxStatePrefix = "SwingVFX";

    public override void Attack(Vector2 direction, LayerMask enemyLayer)
    {
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.right;
        direction.Normalize();

        float effectiveRange = Mathf.Max(0.1f, GetRange());
        float overlapRadius = Mathf.Clamp(hitRadius, 0.1f, effectiveRange);
        Vector2 swingCenter = (Vector2)transform.position + direction * (effectiveRange * hitOriginRangeFactor);

        PlaySwingVfx(direction, effectiveRange);

        Collider2D[] hits = Physics2D.OverlapCircleAll(swingCenter, overlapRadius, enemyLayer);

        float cosThreshold = Mathf.Cos((coneAngle * 0.5f) * Mathf.Deg2Rad);
        float dmg = GetDamage();
        float kb = GetKnockback();
        int hitCount = 0;

        foreach (var h in hits)
        {
            if (!h) continue;

            Vector2 toEnemy = (Vector2)h.bounds.center - swingCenter;
            Vector2 toEnemyDir = toEnemy.sqrMagnitude > 0.0001f ? toEnemy.normalized : Vector2.zero;

            if (Vector2.Dot(direction.normalized, toEnemyDir) < cosThreshold)
                continue;

            var damageable = h.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeHit(dmg, direction, kb);
                hitCount++;
            }
        }
        
        EventBus<PlayerMeleeAttackEvent>.Raise(new PlayerMeleeAttackEvent
        {
            Position = swingCenter,
            Direction = direction,
            Damage = dmg,
            EnemiesHit = hitCount
        });
    }

    private void PlaySwingVfx(Vector2 direction, float effectiveRange)
    {
        if (!swingVfxRenderer) return;
        if (!swingVfxAnimator) return;

        Vector2 snappedDir = SnapTo8Directions(direction);

        float localDistance = effectiveRange * swingVfxRangeFactor;
        Vector3 localOffset = new Vector3(snappedDir.x * localDistance, snappedDir.y * localDistance, swingVfxRenderer.transform.localPosition.z);
        swingVfxRenderer.transform.localPosition = localOffset;

        swingVfxRenderer.flipX = false;
        swingVfxRenderer.flipY = false;

        string stateName = $"{swingVfxStatePrefix}_{BuildDirectionSuffix(snappedDir)}";
        if (swingVfxAnimator.HasState(0, Animator.StringToHash(stateName)))
            swingVfxAnimator.Play(stateName, 0, 0f);
    }

    private static Vector2 SnapTo8Directions(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return Vector2.right;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        angle = Mathf.Round(angle / 45f) * 45f;
        float rad = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
    }

    private static string BuildDirectionSuffix(Vector2 dir)
    {
        if (dir.y >= 0.6f && dir.x >= 0.3f) return "UR";
        if (dir.y <= -0.6f && dir.x >= 0.3f) return "DR";
        if (dir.y >= 0.6f && dir.x <= -0.3f) return "UL";
        if (dir.y <= -0.6f && dir.x <= -0.3f) return "DL";
        if (dir.x >= 0.6f) return "R";
        if (dir.x <= -0.6f) return "L";
        if (dir.y >= 0f) return "U";
        return "D";
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        float effectiveRange = Mathf.Max(0.1f, GetRange());
        float overlapRadius = Mathf.Clamp(hitRadius, 0.1f, effectiveRange);
        Vector2 swingCenter = (Vector2)transform.position + Vector2.right * (effectiveRange * hitOriginRangeFactor);
        Gizmos.DrawWireSphere(swingCenter, overlapRadius);
    }
}