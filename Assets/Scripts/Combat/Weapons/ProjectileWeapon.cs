using UnityEngine;

public class ProjectileWeapon : WeaponBase
{
    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float projectileSpeedOverride = -1f;

    public override void Attack(Vector2 direction, LayerMask enemyLayer, Transform target = null)
    {
        if (!projectilePrefab) return;

        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.right;

        Transform spawnT = firePoint ? firePoint : transform;
        EnemyBase enemy = target ? target.GetComponentInParent<EnemyBase>() : null;
        FireConfiguredProjectile(spawnT.position, direction.normalized, enemy, AttackKind.Ranged, "ranged_attack", triggerOnHitEffects: true);
    }

    public void FireConfiguredProjectileAt(EnemyBase target, AttackKind attackKind, string sourceId, bool triggerOnHitEffects)
    {
        if (target == null)
            return;

        Transform spawnT = firePoint ? firePoint : transform;
        Vector2 direction = (target.transform.position - spawnT.position);
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.right;

        FireConfiguredProjectile(spawnT.position, direction.normalized, target, attackKind, sourceId, triggerOnHitEffects);
    }

    private void FireConfiguredProjectile(Vector2 position, Vector2 direction, EnemyBase target, AttackKind attackKind, string sourceId, bool triggerOnHitEffects)
    {
        var projObject = Instantiate(projectilePrefab, position, Quaternion.identity);
        var projectile = projObject.GetComponent<SimpleProjectile>();
        if (!projectile)
            return;

        var runtime = GetComponentInParent<PlayerUpgradeRuntime>();
        var snapshot = runtime != null
            ? runtime.BuildAttackSnapshot(attackKind, position, target, 0)
            : default;

        float damageValue = snapshot.ApplyTo(GetDamage());
        projectile.Fire(
            direction,
            damageValue,
            GetKnockback(),
            new DamageContext(gameObject, transform.root.gameObject, attackKind, sourceId, triggersOnHitEffects: triggerOnHitEffects));
        runtime?.NotifyAttackPerformed(attackKind, snapshot);
    }
}
