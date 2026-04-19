using UnityEngine;
using System.Collections.Generic;

public class PlayerWeaponController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController playerController;

    [Header("Weapons")]
    [SerializeField] private WeaponBase primaryWeapon;

    [Header("Idle Attack")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float attacksPerSecond = 2.0f;
    [SerializeField] private float meleeAnimationDuration = 0.35f;

    [Header("Ranged Upgrade Projectiles")]
    [SerializeField] private SimpleProjectile rangedUpgradeProjectilePrefab;
    [SerializeField] private Transform rangedProjectileSpawnPoint;
    [SerializeField] private float rangedProjectileDamage = 3f;
    [SerializeField] private float rangedProjectileKnockback = 6f;
    [SerializeField] private float rangedProjectileSearchRadiusMultiplier = 4f;
    [SerializeField] private float minimumRangedProjectileSearchRadius = 6f;

    private float _nextAttackTime;
    private PlayerStats _playerStats;
    private PlayerUpgradeRuntime _upgradeRuntime;

    private void Awake()
    {
        if (!playerController) playerController = GetComponent<PlayerController>();
        _playerStats = GetComponent<PlayerStats>();
        _upgradeRuntime = GetComponent<PlayerUpgradeRuntime>();
    }

    private void Update()
    {
        if (!primaryWeapon) return;
        if (playerController && playerController.IsControlLocked) return;
        if (Time.time < _nextAttackTime) return;

        Transform target = FindClosestEnemy();
        if (!target) return;

        Vector2 dir = (target.position - transform.position);
        if (dir.sqrMagnitude < 0.0001f)
            dir = playerController ? playerController.LastMoveDirection : Vector2.right;

        primaryWeapon.Attack(dir.normalized, enemyLayer, target);

        if (playerController && primaryWeapon is MeleeSwingWeapon)
            playerController.PlayMeleeAnimation(dir.normalized, meleeAnimationDuration);

        float effectiveAttackSpeed = attacksPerSecond * (_playerStats ? _playerStats.AttackSpeed : 1f);
        float cooldown = effectiveAttackSpeed <= 0f ? 999f : (1f / effectiveAttackSpeed);
        _nextAttackTime = Time.time + (cooldown * primaryWeapon.GetCooldown());
    }

    private Transform FindClosestEnemy()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, primaryWeapon.GetRange(), enemyLayer);

        float best = float.PositiveInfinity;
        Transform bestT = null;

        foreach (var h in hits)
        {
            if (!h) continue;
            float d = (h.transform.position - transform.position).sqrMagnitude;
            if (d < best)
            {
                best = d;
                bestT = h.transform;
            }
        }

        return bestT;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, primaryWeapon.GetRange());
    }

    public void FireEnergyBoltsAtRandomEnemies(int boltCount)
    {
        if (boltCount <= 0)
            return;

        if (!rangedUpgradeProjectilePrefab)
            return;

        List<EnemyBase> nearbyEnemies = GetNearbyEnemyTargets();
        if (nearbyEnemies.Count == 0)
            return;

        Transform spawnPoint = rangedProjectileSpawnPoint ? rangedProjectileSpawnPoint : transform;
        for (int i = 0; i < boltCount; i++)
        {
            EnemyBase target = nearbyEnemies[i % nearbyEnemies.Count];
            if (target == null)
                continue;

            FireUpgradeProjectileAt(target, spawnPoint.position, AttackKind.EnergyBolt, "energy_bolt", triggerOnHitEffects: false);
        }
    }

    private void FireUpgradeProjectileAt(EnemyBase target, Vector2 spawnPosition, AttackKind attackKind, string sourceId, bool triggerOnHitEffects)
    {
        if (!rangedUpgradeProjectilePrefab || target == null)
            return;

        Vector2 direction = (target.transform.position - (Vector3)spawnPosition);
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.right;
        direction.Normalize();

        SimpleProjectile projectile = Instantiate(rangedUpgradeProjectilePrefab, spawnPosition, Quaternion.identity);
        var snapshot = _upgradeRuntime != null
            ? _upgradeRuntime.BuildAttackSnapshot(attackKind, spawnPosition, target, 0)
            : default;

        float damage = snapshot.ApplyTo(rangedProjectileDamage);
        projectile.Fire(
            direction,
            damage,
            rangedProjectileKnockback,
            new DamageContext(gameObject, transform.root.gameObject, attackKind, sourceId, triggersOnHitEffects: triggerOnHitEffects));

        _upgradeRuntime?.NotifyAttackPerformed(attackKind, snapshot);
    }

    private List<EnemyBase> GetNearbyEnemyTargets()
    {
        float rangeFromStats = _playerStats ? _playerStats.AttackRange : 0f;
        float rangeFromWeapon = primaryWeapon ? primaryWeapon.GetRange() : 0f;
        float radius = Mathf.Max(
            minimumRangedProjectileSearchRadius,
            Mathf.Max(rangeFromStats, rangeFromWeapon) * Mathf.Max(1f, rangedProjectileSearchRadiusMultiplier));
        float radiusSq = radius * radius;

        var enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        var nearby = new List<EnemyBase>();
        Vector3 origin = transform.position;

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead)
                continue;

            if ((enemy.transform.position - origin).sqrMagnitude > radiusSq)
                continue;

            nearby.Add(enemy);
        }

        nearby.Sort((a, b) =>
        {
            float da = (a.transform.position - origin).sqrMagnitude;
            float db = (b.transform.position - origin).sqrMagnitude;
            return da.CompareTo(db);
        });

        return nearby;
    }
}
