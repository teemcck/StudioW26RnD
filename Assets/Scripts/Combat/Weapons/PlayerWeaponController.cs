using UnityEngine;

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

    private float _nextAttackTime;

    private void Awake()
    {
        if (!playerController) playerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        if (!primaryWeapon) return;
        if (Time.time < _nextAttackTime) return;

        Transform target = FindClosestEnemy();
        if (!target) return;

        Vector2 dir = (target.position - transform.position);
        if (dir.sqrMagnitude < 0.0001f)
            dir = playerController ? playerController.LastMoveDirection : Vector2.right;

        primaryWeapon.Attack(dir.normalized, enemyLayer);

        if (playerController && primaryWeapon is MeleeSwingWeapon)
            playerController.PlayMeleeAnimation(dir.normalized, meleeAnimationDuration);

        float cooldown = attacksPerSecond <= 0f ? 999f : (1f / attacksPerSecond);
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
}
