using UnityEngine;

public class ProjectileWeapon : WeaponBase
{
    [Header("Projectile")]
    [SerializeField] private SimpleProjectile projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float projectileSpeedOverride = -1f;

    public override void Attack(Vector2 direction, LayerMask enemyLayer)
    {
        if (!projectilePrefab) return;

        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.right;

        Transform spawnT = firePoint ? firePoint : transform;

        var proj = Instantiate(projectilePrefab, spawnT.position, Quaternion.identity);

        proj.Fire(direction.normalized);

    }
}
