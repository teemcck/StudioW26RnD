using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracking dig attacks and tactical underground repositioning.
/// Both use the same arena navigation cache and path scratch buffer; the scheduler runs only one move at a time.
/// </summary>
public sealed partial class WormBossController
{
    // Dig and reposition never run concurrently, so they can reuse one path buffer.
    private readonly List<Vector3Int> _pathScratch = new();

    // Buried visibility owns one immunity flag; the full dive/travel/rise sequence owns the other.
    private bool _digInvulnerable;

    private bool _immuneDuringDigMove;

    private Coroutine _repositionRoutine;

    /// <summary>
    /// Moves the Rigidbody2D when present; the Transform fallback preserves the existing depth.
    /// </summary>
    private void SetBossWorldPosition(Vector2 world)
    {
        float z = transform.position.z;
        if (_rb)
            _rb.position = world;
        else
            transform.position = new Vector3(world.x, world.y, z);
    }

    /// <summary>
    /// Tracks the player underground, locks the warning cell, then applies emergence damage at the authored animation time.
    /// </summary>
    private IEnumerator DigRoutine()
    {
        _immuneDuringDigMove = true;
        PlayAnimState(StateDigging);
        ResolveBossAudio()?.PlayDigSfx();
        float digClipLen = GetAnimClipLength(StateDigging, 0.62f);
        yield return new WaitForSeconds(digClipLen);

        SetUndergroundVisuals(true);

        Vector3Int followingCell = _arena.NearestWalkableCell(Player ? Player.position : (Vector2)transform.position);
        Vector3Int startCell = _arena.GetCurrentCell();
        float indicatorRadius = digStrikeRadius * 1.15f;
        SpawnCircleIndicator(_arena.CellCenterWorld(followingCell), indicatorRadius,
            digTrackDuration + digLockDuration,
            digLockDuration / Mathf.Max(0.01f, digTrackDuration + digLockDuration),
            tracked: true);

        float trackElapsed = 0f;
        float nextRubble = 0f;
        while (trackElapsed < digTrackDuration)
        {
            trackElapsed += Time.deltaTime;
            if (Player)
            {
                Vector3Int candidate = _arena.NearestWalkableCell(Player.position);
                if (candidate != followingCell)
                {
                    followingCell = candidate;
                    UpdateTrackedIndicatorCenter(_arena.CellCenterWorld(followingCell));
                }
            }

            float u = Mathf.Clamp01(trackElapsed / Mathf.Max(0.01f, digTrackDuration));
            Vector2 pos;
            Vector2 trailDir = Vector2.right;
            if (_arena.TryGetWalkablePath(startCell, followingCell, _pathScratch))
            {
                float len = _arena.MeasurePathWorldLength(_pathScratch);
                if (len < 0.001f)
                    pos = _arena.CellCenterWorld(_pathScratch[0]);
                else
                {
                    float distAlong = u * len;
                    pos = _arena.PointOnWalkablePathAtDistance(_pathScratch, distAlong);
                    trailDir = _arena.TangentOnWalkablePathAtDistance(_pathScratch, distAlong, 0.12f);
                }
            }
            else
            {
                Vector2 a = _arena.CellCenterWorld(startCell);
                Vector2 b = _arena.CellCenterWorld(followingCell);
                pos = Vector2.Lerp(a, b, u);
                trailDir = (b - a).sqrMagnitude > 0.0001f ? (b - a).normalized : Vector2.right;
            }

            SetBossWorldPosition(pos);

            if (trackElapsed >= nextRubble)
            {
                nextRubble += rubbleTrailInterval;
                SpawnRubbleBurst(transform.position, 2, 1.2f, rubbleColor, trailDir);
            }
            yield return null;
        }

        // Stop following the player before the final warning, giving a stable destination to dodge.
        Vector3Int lockedPrimary = followingCell;
        ForceAllIndicatorsImminent();

        List<Vector3Int> strikeCells = new() { lockedPrimary };

        yield return new WaitForSeconds(digLockDuration);

        Vector2 risePos = _arena.CellCenterWorld(lockedPrimary);
        SetBossWorldPosition(risePos);

        SetUndergroundVisuals(false);
        PlayAnimState(StateRising);
        ResolveBossAudio()?.PlayRiseSfx();
        foreach (Vector3Int cell in strikeCells)
            SpawnRubbleBurst(_arena.CellCenterWorld(cell), 5, 1.52f, riseRubbleColor, default);

        float riseClipLen = GetAnimClipLength(StateRising, 0.72f);
        float riseDmgDelay = riseClipLen * Mathf.Clamp01(digRiseDamageNormalizedTime);
        yield return new WaitForSeconds(riseDmgDelay);

        DealDamageOnCells(strikeCells, digStrikeRadius, digDamage, 4f);
        DestroyActiveIndicator();

        yield return new WaitForSeconds(riseClipLen - riseDmgDelay);
        EndAttack();
    }

    /// <summary>
    /// Burrows to a sampled destination at underground speed, then deals emergence damage before returning to idle.
    /// </summary>
    private IEnumerator RepositionRoutine()
    {
        _immuneDuringDigMove = true;
        _state = InternalState.Repositioning;
        Vector3Int targetCell = ChooseRepositionTarget();
        Vector3 startWorld = transform.position;
        Vector2 targetWorld2 = _arena.CellCenterWorld(targetCell);
        Vector3 targetWorld = new Vector3(targetWorld2.x, targetWorld2.y, transform.position.z);
        if (Vector3.Distance(startWorld, targetWorld) < 0.25f)
        {
            _immuneDuringDigMove = false;
            _state = InternalState.Idle;
            yield break;
        }

        PlayAnimState(StateDigging);
        ResolveBossAudio()?.PlayDigSfx();
        float digClipLen = GetAnimClipLength(StateDigging, 0.62f);
        for (float digT = 0f; digT < digClipLen; digT += Time.deltaTime)
        {
            if (_phaseTransitionActive)
            {
                _immuneDuringDigMove = false;
                _state = InternalState.Idle;
                yield break;
            }
            yield return null;
        }
        if (_phaseTransitionActive)
        {
            _immuneDuringDigMove = false;
            _state = InternalState.Idle;
            yield break;
        }

        SetUndergroundVisuals(true);

        Vector3Int reposFrom = _arena.WorldToCell((Vector2)startWorld);
        bool pathOk = _arena.TryGetWalkablePath(reposFrom, targetCell, _pathScratch);
        float pathWorldLen = pathOk && _pathScratch.Count > 0 ? _arena.MeasurePathWorldLength(_pathScratch) : 0f;
        if (pathWorldLen < 0.02f)
        {
            pathOk = false;
            pathWorldLen = Vector2.Distance((Vector2)startWorld, targetWorld2);
        }
        pathWorldLen = Mathf.Max(0.02f, pathWorldLen);
        float travelTime = pathWorldLen / Mathf.Max(0.5f, undergroundSpeed);
        float elapsed = 0f;
        float nextRubble = 0f;
        Vector3 lastRubblePos = startWorld;

        while (elapsed < travelTime)
        {
            if (_phaseTransitionActive)
            {
                _immuneDuringDigMove = false;
                SetUndergroundVisuals(false);
                _state = InternalState.Idle;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / travelTime);
            Vector2 pos;
            Vector2 trailDir = Vector2.right;
            if (pathOk && _pathScratch.Count > 0)
            {
                float distAlong = t * pathWorldLen;
                pos = _arena.PointOnWalkablePathAtDistance(_pathScratch, distAlong);
                trailDir = _arena.TangentOnWalkablePathAtDistance(_pathScratch, distAlong, 0.1f);
            }
            else
            {
                pos = Vector2.Lerp((Vector2)startWorld, targetWorld2, t);
                Vector2 seg = targetWorld2 - (Vector2)startWorld;
                trailDir = seg.sqrMagnitude > 0.0001f ? seg.normalized : Vector2.right;
            }

            SetBossWorldPosition(pos);

            if (elapsed >= nextRubble)
            {
                nextRubble += rubbleTrailInterval;
                Vector3 here = transform.position;
                if ((here - lastRubblePos).sqrMagnitude > 0.005f)
                {
                    SpawnRubbleBurst(here, 2, 1.12f, rubbleColor, trailDir);
                    lastRubblePos = here;
                }
            }
            yield return null;
        }

        SetBossWorldPosition(new Vector2(targetWorld.x, targetWorld.y));

        SetUndergroundVisuals(false);
        PlayAnimState(StateRising);
        ResolveBossAudio()?.PlayRiseSfx();
        SpawnRubbleBurst(targetWorld, 5, 1.52f, riseRubbleColor, default);

        float riseClipLen = GetAnimClipLength(StateRising, 0.72f);
        float riseDmgDelay = riseClipLen * 0.55f;
        for (float riseT = 0f; riseT < riseDmgDelay; riseT += Time.deltaTime)
        {
            if (_phaseTransitionActive)
            {
                _immuneDuringDigMove = false;
                _state = InternalState.Idle;
                yield break;
            }
            yield return null;
        }
        if (_phaseTransitionActive)
        {
            _immuneDuringDigMove = false;
            _state = InternalState.Idle;
            yield break;
        }

        DealDamageOnCells(new List<Vector3Int> { targetCell }, repositionEmergeRadius, repositionEmergeDamage, 3f);

        float riseRemain = Mathf.Max(0f, riseClipLen - riseDmgDelay);
        for (float riseT = 0f; riseT < riseRemain; riseT += Time.deltaTime)
        {
            if (_phaseTransitionActive)
            {
                _immuneDuringDigMove = false;
                _state = InternalState.Idle;
                yield break;
            }
            yield return null;
        }

        _immuneDuringDigMove = false;
        _state = InternalState.Idle;
        PlayAnimState(StateIdle);
    }

    /// <summary>
    /// Requests a retreat for a low, unbroken shield; otherwise favors destinations near melee range.
    /// </summary>
    private Vector3Int ChooseRepositionTarget()
    {
        Vector2 playerPos = Player ? (Vector2)Player.position : (Vector2)transform.position;
        float shieldNorm = CurrentShieldNormalized;
        bool wantDistance = !_shieldBroken && shieldNorm > 0.01f && shieldNorm < shieldRegenSeekThreshold;
        return _arena.ChooseRepositionTarget(playerPos, wantDistance, minRepositionDistance,
            maxRepositionDistance, repositionIdealPlayerDistance, meleeRange);
    }
}
