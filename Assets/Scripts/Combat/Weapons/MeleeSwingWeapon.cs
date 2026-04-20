using System.Collections.Generic;
using UnityEngine;

public class MeleeSwingWeapon : WeaponBase
{
    private const float ReachBuffer = 0.08f;
    private static readonly Vector2 FallbackAnchorLocal = new Vector2(0f, 0.24f);

    [Header("Melee")]
    [SerializeField] private float hitRadius = 0.85f;
    [SerializeField] [Range(0.2f, 1f)] private float hitOriginRangeFactor = 0.7f;
    [SerializeField] private float pointBlankConeBypassDistance = 0.45f;
    [Range(10f, 180f)] [SerializeField] private float coneAngle = 80f;

    [Header("VFX")]
    [SerializeField] private float swingVfxRangeFactor = 0.95f;
    [SerializeField] private SpriteRenderer swingVfxRenderer;
    [SerializeField] private Animator swingVfxAnimator;
    [SerializeField] private string swingVfxStatePrefix = "SwingVFX";

    public override void Attack(Vector2 direction, LayerMask enemyLayer, Transform target = null)
    {
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.right;
        direction.Normalize();

        float effectiveRange = Mathf.Max(0.1f, GetRange());
        float overlapRadius = Mathf.Clamp(hitRadius, 0.1f, effectiveRange);
        Transform pivotRoot = GetPivotRoot();
        Vector2 hitAnchor = GetHitAnchorWorld(pivotRoot);
        Vector2 swingCenter = hitAnchor + direction * (effectiveRange * hitOriginRangeFactor);
        float queryRadius = Mathf.Max(pointBlankConeBypassDistance + 0.1f, effectiveRange + overlapRadius + ReachBuffer);

        PlaySwingVfx(direction, effectiveRange);
        Collider2D[] candidates = Physics2D.OverlapCircleAll(hitAnchor, queryRadius, enemyLayer);

        float cosThreshold = Mathf.Cos((coneAngle * 0.5f) * Mathf.Deg2Rad);
        var landed = new Dictionary<int, Component>(8);
        foreach (var h in candidates)
        {
            if (!h)
                continue;
            var rootDamageable = h.GetComponentInParent<IDamageable>();
            if (rootDamageable is not Component comp)
                continue;
            int id = comp.GetInstanceID();
            if (landed.ContainsKey(id))
                continue;
            if (!IsTargetHit(h, direction, swingCenter, hitAnchor, overlapRadius, cosThreshold))
                continue;
            landed[id] = comp;
        }

        var runtime = GetComponentInParent<PlayerUpgradeRuntime>();
        EnemyBase primaryTarget = target ? target.GetComponentInParent<EnemyBase>() : null;
        var snapshot = runtime != null
            ? runtime.BuildAttackSnapshot(AttackKind.Melee, hitAnchor, primaryTarget, landed.Count)
            : default;

        float dmg = snapshot.ApplyTo(GetDamage());
        float kb = GetKnockback();
        int hitCount = 0;

        foreach (var comp in landed.Values)
        {
            if (comp is not IDamageable damageable)
                continue;
            Vector2 kbDir = (Vector2)comp.transform.position - hitAnchor;
            if (kbDir.sqrMagnitude < 0.0001f)
                kbDir = direction;
            else
                kbDir.Normalize();
            damageable.TakeHit(dmg, kbDir, kb, new DamageContext(gameObject, transform.root.gameObject, AttackKind.Melee, "melee_attack", triggersOnHitEffects: true));
            hitCount++;
        }

        runtime?.NotifyAttackPerformed(AttackKind.Melee, snapshot);
        
        EventBus<PlayerMeleeAttackEvent>.Raise(new PlayerMeleeAttackEvent
        {
            Position = swingCenter,
            Direction = direction,
            Damage = dmg,
            EnemiesHit = hitCount,
            EnemiesInRange = landed.Count
        });
    }

    private bool IsTargetHit(Collider2D h, Vector2 direction, Vector2 swingCenter, Vector2 hitAnchor, float overlapRadius, float cosThreshold)
    {
        Vector2 enemyPointFromPlayer = h.ClosestPoint(hitAnchor);
        float dPlayer = Vector2.Distance(hitAnchor, enemyPointFromPlayer);
        if (dPlayer <= pointBlankConeBypassDistance)
            return true;

        Vector2 enemyPointFromSwing = h.ClosestPoint(swingCenter);
        float dSwing = Vector2.Distance(swingCenter, enemyPointFromSwing);
        if (dSwing > overlapRadius + ReachBuffer)
            return false;

        Vector2 toEnemy = enemyPointFromSwing - swingCenter;
        Vector2 toEnemyDir = toEnemy.sqrMagnitude > 0.0001f ? toEnemy.normalized : Vector2.zero;
        return Vector2.Dot(direction, toEnemyDir) >= cosThreshold;
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

    private Transform GetPivotRoot()
    {
        Transform t = transform;
        var rb = GetComponentInParent<Rigidbody2D>();
        if (rb != null)
            return rb.transform;
        return t.root;
    }

    private Vector2 GetHitAnchorWorld(Transform pivotRoot)
    {
        var anchor = pivotRoot.GetComponent<PlayerCombatAnchor>() ?? pivotRoot.GetComponentInChildren<PlayerCombatAnchor>();
        if (anchor != null)
            return anchor.WorldHitAnchor;
        Vector3 w = pivotRoot.TransformPoint(new Vector3(FallbackAnchorLocal.x, FallbackAnchorLocal.y, 0f));
        return new Vector2(w.x, w.y);
    }

    private void OnDrawGizmosSelected()
    {
        float effectiveRange = Mathf.Max(0.1f, GetRange());
        float overlapRadius = Mathf.Clamp(hitRadius, 0.1f, effectiveRange);
        Transform pivot = GetPivotRoot();
        Vector2 anchor = GetHitAnchorWorld(pivot);
        Vector2 swingCenter = anchor + Vector2.right * (effectiveRange * hitOriginRangeFactor);
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        Gizmos.DrawWireSphere(swingCenter, overlapRadius);
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.65f);
        Gizmos.DrawWireSphere(anchor, pointBlankConeBypassDistance);
        Gizmos.color = new Color(0.4f, 0.9f, 0.4f, 0.5f);
        Gizmos.DrawWireSphere(pivot.position, 0.04f);
    }
}
