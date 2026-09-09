using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Laser charging, beam extension, tip damage, and temporary warning rows for WormBossController.
/// </summary>
public sealed partial class WormBossController
{
    // The controller owns the active beam until release; its visual component owns the final fade.
    private GameObject _activeBossLaserBeam;

    private bool _wormAnimatorHeldOnLaserFireFrame;

    // Rows are also registered in the shared indicator list so priority cleanup reaches every warning.
    private readonly List<BossAttackIndicator> _laserStretchSegments = new();
    /// <summary>Damageables already hit during the current laser sweep; each target is hit once per sweep.</summary>
    private readonly HashSet<IDamageable> _laserDamagedThisSweep = new HashSet<IDamageable>();

    private bool[] _laserRowFadeStarted;

    /// <summary>
    /// Fires one extending beam in phase two and two separately aimed beams in phase three.
    /// </summary>
    private IEnumerator LaserRoutine()
    {
        int sweepCount = CurrentPhase >= 3 ? 2 : 1;
        for (int sweepIndex = 0; sweepIndex < sweepCount; sweepIndex++)
        {
            Vector2 aim = AimDirectionToPlayer();
            FaceDirection(aim);
            float aimAngleDeg = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            yield return ExecuteSingleLaserSweep(aimAngleDeg);
            if (sweepIndex < sweepCount - 1 && laserBetweenSweepPause > 0f)
                yield return new WaitForSeconds(laserBetweenSweepPause);
        }

        yield return new WaitForSeconds(laserRecoveryDuration);
        EndAttack();
    }

    /// <summary>
    /// Keeps the warned direction fixed while extending the beam; only the advancing tip deals damage.
    /// </summary>
    private IEnumerator ExecuteSingleLaserSweep(float aimAngleDeg)
    {
        float castAngleDeg = aimAngleDeg;
        Vector2 castDir = AngleToVector(castAngleDeg);

        PlayAnimState(StateLaserCharge);

        Vector2 mouthStart = ComputeMouthWorldPosition();
        Vector2 telegraphCenter = mouthStart + castDir * (laserBeamLength * 0.5f);

        BossAttackIndicator chargeTelegraph = SpawnRectIndicator(telegraphCenter,
            new Vector2(laserBeamLength, laserChargeStripeWidth), castAngleDeg,
            Mathf.Max(0.05f, laserChargeDuration), 0.32f);

        yield return new WaitForSeconds(laserChargeDuration);

        if (chargeTelegraph)
        {
            Destroy(chargeTelegraph.gameObject);
            _activeIndicators.Remove(chargeTelegraph);
        }

        if (!bossLaserBeamPrefab)
        {
            PlayAnimState(StateIdle);
            yield break;
        }

        FreezeWormLaserFireHoldPose();
        ResolveBossAudio()?.PlayLaserShootSfx();

        GameObject beamGo = Instantiate(bossLaserBeamPrefab);
        _activeBossLaserBeam = beamGo;
        BossLaserBeamInstance beamFx = beamGo.GetComponent<BossLaserBeamInstance>();
        if (beamFx)
        {
            beamFx.ApplyVisualTint(laserBeamColor);
            if (spriteRenderer) beamFx.CopySortingFrom(spriteRenderer);
        }

        EnsureLaserStretchSegmentsCreated();
        int segN = Mathf.Max(4, laserStretchSegmentCount);
        if (_laserRowFadeStarted == null || _laserRowFadeStarted.Length != segN)
            _laserRowFadeStarted = new bool[segN];
        else
            System.Array.Clear(_laserRowFadeStarted, 0, segN);

        foreach (BossAttackIndicator seg in _laserStretchSegments)
        {
            if (seg) seg.SetVisualEnabled(false);
        }

        float segmentLenFixed = laserBeamLength / segN;
        // Track damageables rather than colliders: one player may expose several hit colliders.
        _laserDamagedThisSweep.Clear();
        float elapsed = 0f;
        float beamZ = transform.position.z - 0.01f;

        while (elapsed < laserFireDuration)
        {
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, laserFireDuration));
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            float beamLen = Mathf.Lerp(laserMinBeamLength, laserBeamLength, eased);
            Vector2 mouthNow = ComputeMouthWorldPosition();
            Vector2 tip = mouthNow + castDir * beamLen;
            float u = beamLen / Mathf.Max(0.001f, laserBeamLength);

            _laserStretchSegments.RemoveAll(static s => s == null);

            if (beamFx)
                beamFx.ApplyBeam(mouthNow, castAngleDeg, beamLen, beamZ);

            for (int i = 0; i < segN && i < _laserStretchSegments.Count; i++)
            {
                BossAttackIndicator seg = _laserStretchSegments[i];
                if (!seg) continue;

                float rowStartU = i / (float)segN;
                if (u < rowStartU)
                {
                    seg.SetVisualEnabled(false);
                    continue;
                }

                seg.SetVisualEnabled(true);
                Vector2 center = mouthNow + castDir * ((i + 0.5f) * segmentLenFixed);
                seg.UpdateRect(center, new Vector2(segmentLenFixed, laserSegmentStripeWidth), castAngleDeg);

                float rowEndU = (i + 1f) / segN;
                float imminentAt = rowEndU - (laserSegmentImminentLead / segN);
                if (u >= imminentAt && seg.CurrentPhase != BossAttackIndicator.Phase.Imminent)
                    seg.ForceImminent();

                if (u >= rowEndU && !_laserRowFadeStarted[i])
                {
                    _laserRowFadeStarted[i] = true;
                    seg.FadeOutAndDestroy(Mathf.Max(0.04f, laserRowFadeDuration));
                }
            }

            DealDamageLaserTip(tip, laserTipHitRadius, laserDamage);

            _activeIndicators.RemoveAll(static r => r == null);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (beamFx)
        {
            Vector2 mouthFinal = ComputeMouthWorldPosition();
            beamFx.ApplyBeam(mouthFinal, castAngleDeg, laserBeamLength, beamZ);
        }

        ReleaseWormLaserFireHoldPose();
        _activeBossLaserBeam = null;

        _laserStretchSegments.RemoveAll(static s => s == null);
        foreach (BossAttackIndicator seg in _laserStretchSegments)
        {
            if (!seg) continue;
            _activeIndicators.Remove(seg);
            Destroy(seg.gameObject);
        }
        _laserStretchSegments.Clear();

        if (beamFx) beamFx.FadeAndDestroy(0.18f);
        else if (beamGo) Destroy(beamGo);
    }

    /// <summary>
    /// Allocates long-lived warning rows that the beam routine explicitly reveals, marks imminent, and fades.
    /// </summary>
    private void EnsureLaserStretchSegmentsCreated()
    {
        if (indicatorBaseSprite == null) return;
        _laserStretchSegments.RemoveAll(static s => s == null);
        int n = Mathf.Max(4, laserStretchSegmentCount);
        if (_laserStretchSegments.Count >= n) return;
        float longDur = laserFireDuration + laserChargeDuration + 8f;
        for (int i = _laserStretchSegments.Count; i < n; i++)
        {
            BossAttackIndicator ind = CreateIndicator();
            ind.Begin(BossAttackIndicator.Shape.Rect, indicatorBaseSprite, indicatorImminentSprite,
                indicatorBaseColor, indicatorImminentColor,
                ComputeMouthWorldPosition(), new Vector2(0.2f, laserSegmentStripeWidth), 0f,
                longDur, 0.08f, 5.5f, indicatorSortingOrder, _sortingLayerId);
            _laserStretchSegments.Add(ind);
        }
    }

    /// <summary>
    /// Destroys beam warning rows and clears fade state when an attack is interrupted.
    /// </summary>
    private void DestroyLaserStretchSegments()
    {
        foreach (BossAttackIndicator seg in _laserStretchSegments)
        {
            if (!seg) continue;
            _activeIndicators.Remove(seg);
            Destroy(seg.gameObject);
        }
        _laserStretchSegments.Clear();
        _laserRowFadeStarted = null;
    }

    /// <summary>
    /// Checks player-layer colliders at the beam tip and hits each damageable at most once per sweep.
    /// </summary>
    private void DealDamageLaserTip(Vector2 tipWorld, float radius, float damage)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(tipWorld, radius, playerLayerMask);
        foreach (Collider2D hit in hits)
        {
            if (!hit) continue;
            IDamageable d = hit.GetComponentInParent<IDamageable>();
            if (d == null || _laserDamagedThisSweep.Contains(d)) continue;
            Vector2 kb = ((Vector2)hit.bounds.center - tipWorld);
            if (kb.sqrMagnitude <= 0.0001f) kb = Vector2.up;
            else kb.Normalize();
            d.TakeHit(damage, kb, 3.5f);
            _laserDamagedThisSweep.Add(d);
        }
    }

    /// <summary>
    /// Evaluates the last firing frame immediately before freezing Animator playback for the extending beam.
    /// </summary>
    private void FreezeWormLaserFireHoldPose()
    {
        if (!animator) return;
        animator.Play(StateLaserFire, 0, 1f);
        animator.Update(0f);
        animator.speed = 0f;
        _wormAnimatorHeldOnLaserFireFrame = true;
    }

    /// <summary>
    /// Restores Animator speed only if the laser routine owns a held firing pose.
    /// </summary>
    private void ReleaseWormLaserFireHoldPose()
    {
        if (!_wormAnimatorHeldOnLaserFireFrame) return;
        _wormAnimatorHeldOnLaserFireFrame = false;
        if (animator) animator.speed = 1f;
    }

    /// <summary>
    /// Mirrors the authored mouth offset using sprite facing; flipping the sprite does not move its Transform.
    /// </summary>
    private Vector2 ComputeMouthWorldPosition()
    {
        Vector2 pos = transform.position;
        Vector2 offset = laserMouthOffset;
        bool flipX = spriteRenderer && spriteRenderer.flipX;
        bool facingRight = spriteFacesRightByDefault ^ flipX;
        if (!facingRight) offset.x = -offset.x;
        return pos + offset;
    }

    /// <summary>
    /// Converts world-space aim in degrees into a unit direction.
    /// </summary>
    private static Vector2 AngleToVector(float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }
}
