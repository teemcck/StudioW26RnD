using UnityEngine;

public class RangedEnemy : EnemyBase
{
    [Header("Ranged")]
    [SerializeField] private SimpleProjectile projectilePrefab;
    [SerializeField] private Transform firePoint;

    [SerializeField] private float desiredDistance = 4f;
    [SerializeField] private float shootCooldown = 1.2f;

    private float _nextShootTime;

    protected override void Awake()
    {
        base.Awake();

        if (!firePoint)
            firePoint = transform;
    }

    private void FixedUpdate()
    {
        if (!Player) return;

        Vector2 toPlayer = (Player.position - transform.position);
        float dist = toPlayer.magnitude;

        Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.zero;
        float deadZone = 0.25f;

        if (dist > desiredDistance + deadZone)
            Rb.AddForce(dir * moveSpeed, ForceMode2D.Force);
        else if (dist < desiredDistance - deadZone)
            Rb.AddForce(-dir * moveSpeed, ForceMode2D.Force);

        if (Time.time >= _nextShootTime && projectilePrefab)
        {
            _nextShootTime = Time.time + shootCooldown;
            Shoot(dir);
        }
    }

    private void Shoot(Vector2 directionToPlayer)
    {
        var proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        proj.Fire(directionToPlayer);
    }
}
