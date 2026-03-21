using UnityEngine;

public class LobbingEnemy : EnemyBase
{
    [Header("Lob attack")]
    [SerializeField] private LobbingProjectile projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float horizontalShotSpeed = 2.6f;

    [Header("Spacing")]
    [SerializeField] private float desiredDistance = 5.5f;
    [SerializeField] private float shootCooldown = 2f;

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

        Vector2 toPlayer = (Vector2)(Player.position - transform.position);
        float dist = toPlayer.magnitude;
        Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.zero;
        const float deadZone = 0.35f;

        if (dist > desiredDistance + deadZone)
            Rb.AddForce(dir * moveSpeed, ForceMode2D.Force);

        if (Time.time < _nextShootTime || !projectilePrefab) return;

        _nextShootTime = Time.time + shootCooldown;

        Vector2 fire = firePoint.position;
        Vector2 target = GetPlayerFeetWorld(Player);

        var proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        proj.FireBallistic(fire, target, horizontalShotSpeed);
    }

    private static Vector2 GetPlayerFeetWorld(Transform player)
    {
        var col = player.GetComponent<Collider2D>();
        if (col == null)
            return player.position;

        Bounds b = col.bounds;
        return new Vector2(b.center.x, b.min.y);
    }
}
