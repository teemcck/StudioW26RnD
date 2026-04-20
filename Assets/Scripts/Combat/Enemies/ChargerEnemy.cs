using UnityEngine;

public class ChargerEnemy : EnemyBase
{
    private enum Phase
    {
        Chase,
        Windup,
        Charging,
        Cooldown
    }

    [Header("Melee")]
    [SerializeField] private float attackRange = 0.85f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float contactDamage = 2f;

    [Header("Charge")]
    [SerializeField] private float chargeRange = 4f;
    [SerializeField] private float chargeSpeed = 12f;
    [SerializeField] private float chargeDuration = 0.35f;
    [SerializeField] private float windupTime = 0.4f;
    [SerializeField] private float chargeCooldown = 1.8f;

    private Phase _phase = Phase.Chase;
    private float _phaseEndTime;
    private float _nextChargeAvailableTime;
    private Vector2 _chargeDir;

    private void FixedUpdate()
    {
        if (!Player) return;

        Vector2 toPlayer = GetPlayerCombatWorldPoint() - (Vector2)transform.position;
        float dist = toPlayer.magnitude;
        Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.zero;

        switch (_phase)
        {
            case Phase.Chase:
                ChaseAndMelee(dist, dir);
                if (dist <= chargeRange && Time.time >= _nextChargeAvailableTime)
                {
                    _phase = Phase.Windup;
                    _phaseEndTime = Time.time + windupTime;
                    Rb.linearVelocity = Vector2.zero;
                }
                break;

            case Phase.Windup:
                Rb.AddForce(dir * EffectiveMoveSpeed * 0.25f, ForceMode2D.Force);
                if (Time.time >= _phaseEndTime)
                {
                    _phase = Phase.Charging;
                    _chargeDir = dir.sqrMagnitude > 0.0001f ? dir : Vector2.right;
                    _phaseEndTime = Time.time + chargeDuration;
                }
                break;

            case Phase.Charging:
                Rb.linearVelocity = _chargeDir * chargeSpeed;
                if (Time.time >= _phaseEndTime)
                {
                    _phase = Phase.Cooldown;
                    _phaseEndTime = Time.time + chargeCooldown;
                    _nextChargeAvailableTime = _phaseEndTime;
                    Rb.linearVelocity = Vector2.zero;
                }
                break;

            case Phase.Cooldown:
                Rb.AddForce(dir * EffectiveMoveSpeed * 0.6f, ForceMode2D.Force);
                if (Time.time >= _phaseEndTime)
                    _phase = Phase.Chase;
                TryMelee(dist, dir);
                break;
        }
    }

    private void ChaseAndMelee(float dist, Vector2 dir)
    {
        if (dist > attackRange)
            Rb.AddForce(dir * EffectiveMoveSpeed, ForceMode2D.Force);
        TryMelee(dist, dir);
    }

    private void TryMelee(float dist, Vector2 dir)
    {
        if (dist > attackRange) return;
        float cooldown = attackCooldown / Mathf.Max(0.1f, EffectiveAttackSpeedMultiplier);
        if (TryDealContactDamage(Player, contactDamage, cooldown, 0f, true, dir))
            AudioManager.Instance?.PlayUfoAttack(0.9f);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        float cooldown = attackCooldown / Mathf.Max(0.1f, EffectiveAttackSpeedMultiplier);
        if (TryDealContactDamage(collision.collider, contactDamage, cooldown, 0f))
            AudioManager.Instance?.PlayUfoAttack(0.9f);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        float cooldown = attackCooldown / Mathf.Max(0.1f, EffectiveAttackSpeedMultiplier);
        if (TryDealContactDamage(other, contactDamage, cooldown, 0f))
            AudioManager.Instance?.PlayUfoAttack(0.9f);
    }
}
