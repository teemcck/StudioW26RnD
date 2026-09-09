using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Melee and projectile attacks, shared aiming, and outgoing cell damage for WormBossController.
/// </summary>
public sealed partial class WormBossController
{
    /// <summary>
    /// Locks aim and affected cells before the warning, applies one hit per target, then waits for recovery.
    /// </summary>
    private IEnumerator MeleeRoutine()
    {
        Vector2 aim = AimDirectionToPlayer();
        FaceDirection(aim);
        PlayAnimState(StateMelee);

        List<Vector3Int> coneCells = _arena.GetArcCells(transform.position, aim, meleeRange, meleeArcDegrees * 0.5f);
        float totalTelegraph = meleeFramesBeforeWait + meleeChargeWait;

        Vector2 center = (Vector2)transform.position + aim * (meleeRange * 0.5f);
        float width = 2f * meleeRange * Mathf.Tan(meleeArcDegrees * 0.5f * Mathf.Deg2Rad);
        float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        SpawnRectIndicator(center, new Vector2(meleeRange, width), angle, totalTelegraph, meleeImminentFraction);

        yield return new WaitForSeconds(meleeFramesBeforeWait);
        yield return new WaitForSeconds(meleeChargeWait);

        ResolveBossAudio()?.PlayMeleeSfx();
        DealDamageOnCells(coneCells, 0.55f, meleeDamage, 2.5f);
        DestroyActiveIndicator();

        yield return new WaitForSeconds(meleeStrikeDuration);
        yield return new WaitForSeconds(meleeRecoveryDuration);

        EndAttack();
    }

    /// <summary>
    /// Telegraphs the first volley; phase three adds a second volley that aims at the player again.
    /// </summary>
    private IEnumerator ShootRoutine()
    {
        Vector2 aim = AimDirectionToPlayer();
        FaceDirection(aim);
        PlayAnimState(StateShoot);

        float totalTelegraph = shootFramesBeforeWait + shootChargeWait;
        float sightLength = 5f;
        float sightWidth = 0.8f;
        Vector2 sightCenter = (Vector2)transform.position + aim * (sightLength * 0.5f);
        float sightAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        SpawnRectIndicator(sightCenter, new Vector2(sightLength, sightWidth), sightAngle,
            totalTelegraph, 0.32f);

        yield return new WaitForSeconds(shootFramesBeforeWait);
        yield return new WaitForSeconds(shootChargeWait);

        FireBossBullets(aim);
        DestroyActiveIndicator();

        if (CurrentPhase >= 3)
        {
            yield return new WaitForSeconds(shootBurstIntervalPhase3);
            FireBossBullets(AimDirectionToPlayer());
        }

        yield return new WaitForSeconds(shootRecoveryDuration);
        EndAttack();
    }

    /// <summary>
    /// Spawns the phase-specific spread. Projectile speed and damage remain authored on the projectile prefab.
    /// </summary>
    private void FireBossBullets(Vector2 aim)
    {
        if (!bossProjectilePrefab) return;

        ResolveBossAudio()?.PlayRangedShootSfx();

        int bullets;
        float spread;
        if (CurrentPhase >= 3)
        {
            bullets = shootBulletsPhase3;
            spread = shootSpreadPhase3;
        }
        else if (CurrentPhase == 2)
        {
            bullets = shootBulletsPhase2;
            spread = shootSpreadPhase2;
        }
        else
        {
            bullets = 1;
            spread = 0f;
        }

        Vector3 firePos = transform.position;
        for (int i = 0; i < bullets; i++)
        {
            float offset = bullets == 1 ? 0f : Mathf.Lerp(-spread, spread, i / (float)(bullets - 1));
            Vector2 dir = Rotate(aim, offset);
            GameObject go = Instantiate(bossProjectilePrefab, firePos, Quaternion.identity);
            SimpleProjectile proj = go.GetComponent<SimpleProjectile>();
            if (proj)
                proj.Fire(dir, 0f, 0f,
                    new DamageContext(gameObject, gameObject, AttackKind.Ranged, "boss_projectile"));
        }
    }

    /// <summary>
    /// Returns a normalized player direction with a rightward fallback for absent or overlapping targets.
    /// </summary>
    private Vector2 AimDirectionToPlayer()
    {
        if (!Player) return Vector2.right;
        Vector2 d = (Player.position - transform.position);
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.right;
    }

    /// <summary>
    /// Rotates a direction by degrees to distribute projectile volleys symmetrically.
    /// </summary>
    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    /// <summary>
    /// Applies one hit per damageable across all overlapping cell circles, avoiding duplicate hits from multiple colliders.
    /// </summary>
    private void DealDamageOnCells(List<Vector3Int> cells, float radius, float damage, float knockbackForce)
    {
        if (cells == null || cells.Count == 0) return;
        HashSet<IDamageable> dealt = new HashSet<IDamageable>();
        foreach (Vector3Int cell in cells)
        {
            Vector2 center = _arena.CellCenterWorld(cell);
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, playerLayerMask);
            foreach (Collider2D hit in hits)
            {
                if (!hit) continue;
                IDamageable d = hit.GetComponentInParent<IDamageable>();
                if (d == null || dealt.Contains(d)) continue;
                Vector2 kb = ((Vector2)hit.bounds.center - center);
                if (kb.sqrMagnitude <= 0.0001f) kb = Vector2.up;
                else kb.Normalize();
                d.TakeHit(damage, kb, knockbackForce);
                dealt.Add(d);
            }
        }
    }
}
