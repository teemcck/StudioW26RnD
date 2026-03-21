using UnityEngine;

public class RangedEnemy : EnemyBase
{
    [Header("Ranged")]
    [SerializeField] private SimpleProjectile projectilePrefab;
    [SerializeField] private Transform firePoint;

    [SerializeField] private float desiredDistance = 4f;
    [SerializeField] private float shootCooldown = 1.2f;
    [SerializeField] private Vector2 playerAimOffset = Vector2.zero;

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
            Vector2 aim = GetPlayerAimWorldPoint(Player);
            Vector2 fromFire = aim - (Vector2)firePoint.position;
            Vector2 shootDir = fromFire.sqrMagnitude > 0.0001f ? fromFire.normalized : dir;
            Shoot(shootDir);
        }
    }

    private Vector2 GetPlayerAimWorldPoint(Transform player)
    {
        var col = player.GetComponent<Collider2D>();
        if (col != null)
            return (Vector2)col.bounds.center + playerAimOffset;
        return (Vector2)player.position + playerAimOffset;
    }

    private void Shoot(Vector2 fireDirection)
    {
        var proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        proj.Fire(fireDirection);
    }
}
