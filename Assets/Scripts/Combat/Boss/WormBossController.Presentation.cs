using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Animation, facing, buried visibility, HUD binding, warnings, rubble, and audio lookup for WormBossController.
/// </summary>
public sealed partial class WormBossController
{
    // Animator state names also identify clips when resolving authored timing.

    private const string StateIdle = "BossWorm_Idle";

    private const string StateShoot = "BossWorm_Shoot";

    private const string StateMelee = "BossWorm_Melee";

    private const string StateDigging = "BossWorm_Digging";

    private const string StateRising = "BossWorm_Rising";

    private const string StateLaserCharge = "BossWorm_LaserCharge";

    private const string StateLaserFire = "BossWorm_LaserFire";

    private const string StatePhaseChange = "BossWorm_PhaseChange";

    private const string StateDamage = "BossWorm_Damage";

    private const string StateDying = "BossWorm_Dying";

    private BossAudioManager _bossAudioCached;

    private float _baseAlpha = 1f;

    private int _sortingLayerId;

    private readonly List<BossAttackIndicator> _activeIndicators = new();

    private BossAttackIndicator _trackedIndicator;

    private float _lastFlipTime = -999f;

    private ShadowCaster2D[] _shadowCasters;


    /// <summary>
    /// Prefers the Inspector audio reference and caches a scene fallback when one is available.
    /// </summary>
    private BossAudioManager ResolveBossAudio()
    {
        if (bossAudioManager)
            return bossAudioManager;
        if (_bossAudioCached)
            return _bossAudioCached;
        _bossAudioCached = FindFirstObjectByType<BossAudioManager>();
        return _bossAudioCached;
    }

    /// <summary>
    /// Binds initial HP and shield values and synchronizes observed phase before the fight begins.
    /// </summary>
    private void BindHealthBar()
    {
        if (!healthBarUI)
            healthBarUI = FindFirstObjectByType<BossHealthBarUI>(FindObjectsInactive.Include);
        if (!healthBarUI) return;
        healthBarUI.Bind(this);
        healthBarUI.SetHealth(HealthNormalized);
        healthBarUI.SetShield(CurrentShieldNormalized);
        _lastObservedPhase = CurrentPhase;
        _phaseChangePending = false;
    }

    /// <summary>
    /// Applies authored sprite facing with a horizontal dead zone and cooldown to prevent rapid flipping.
    /// </summary>
    private void FaceDirection(Vector2 direction)
    {
        if (!spriteRenderer) return;
        if (Mathf.Abs(direction.x) < Mathf.Max(0.001f, flipThreshold)) return;
        bool playerOnRight = direction.x > 0f;
        bool desiredFlipX = spriteFacesRightByDefault ? (direction.x < 0f) : playerOnRight;
        if (desiredFlipX == spriteRenderer.flipX) return;
        if (Time.time - _lastFlipTime < Mathf.Max(0f, minFlipInterval)) return;
        spriteRenderer.flipX = desiredFlipX;
        _lastFlipTime = Time.time;
    }

    /// <summary>
    /// Starts a named state from its first frame and restores normal Animator playback.
    /// </summary>
    private void PlayAnimState(string stateName)
    {
        if (!animator) return;
        if (!animator.enabled) animator.enabled = true;
        animator.speed = 1f;
        animator.Play(stateName, 0, 0f);
    }

    /// <summary>
    /// Blends into a named state after restoring Animator playback for an interrupting reaction.
    /// </summary>
    private void CrossFadeAnimState(string stateName, float duration)
    {
        if (!animator) return;
        if (!animator.enabled) animator.enabled = true;
        animator.speed = 1f;
        animator.CrossFade(stateName, duration, 0, 0f);
    }

    /// <summary>
    /// Looks up clip duration by the encounter's matching state/clip names, using fallback seconds if unavailable.
    /// </summary>
    private float GetAnimClipLength(string stateName, float fallback)
    {
        if (!animator || animator.runtimeAnimatorController == null) return fallback;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip && clip.name == stateName)
                return clip.length;
        }
        return fallback;
    }

    /// <summary>
    /// Keeps buried immunity, collision, sprite visibility, and 2D shadow casting in sync.
    /// </summary>
    private void SetUndergroundVisuals(bool underground)
    {
        _digInvulnerable = underground;
        if (_mainCollider) _mainCollider.enabled = !underground;
        if (spriteRenderer)
        {
            spriteRenderer.enabled = !underground;
            if (!underground)
            {
                Color c = spriteRenderer.color;
                c.a = _baseAlpha;
                spriteRenderer.color = c;
                _shieldPingBaseColor = c;
            }
        }

        // No ground shadow while burrowed; restore 2D shadow casting when surfaced.
        bool castShadow = !underground;
        if (_shadowCasters != null)
        {
            for (int i = 0; i < _shadowCasters.Length; i++)
            {
                ShadowCaster2D sc = _shadowCasters[i];
                if (sc != null)
                    sc.castsShadows = castShadow;
            }
        }
    }

    /// <summary>
    /// Creates a rectangular warning using shared encounter styling and optionally records it for tracking.
    /// </summary>
    private BossAttackIndicator SpawnRectIndicator(Vector2 center, Vector2 size, float angleDegrees,
        float duration, float imminentFraction, bool tracked = false)
    {
        if (indicatorBaseSprite == null) return null;
        BossAttackIndicator ind = CreateIndicator();
        ind.Begin(BossAttackIndicator.Shape.Rect, indicatorBaseSprite, indicatorImminentSprite,
            indicatorBaseColor, indicatorImminentColor,
            center, size, angleDegrees,
            duration, imminentFraction, 5.5f, indicatorSortingOrder, _sortingLayerId);
        if (tracked) _trackedIndicator = ind;
        return ind;
    }

    /// <summary>
    /// Creates a circular warning using shared encounter styling and optionally records it for tracking.
    /// </summary>
    private BossAttackIndicator SpawnCircleIndicator(Vector2 center, float radius,
        float duration, float imminentFraction, bool tracked = false)
    {
        if (indicatorBaseSprite == null) return null;
        BossAttackIndicator ind = CreateIndicator();
        ind.Begin(BossAttackIndicator.Shape.Circle, indicatorBaseSprite, indicatorImminentSprite,
            indicatorBaseColor, indicatorImminentColor,
            center, new Vector2(radius, radius), 0f,
            duration, imminentFraction, 5.5f, indicatorSortingOrder, _sortingLayerId);
        if (tracked) _trackedIndicator = ind;
        return ind;
    }

    /// <summary>
    /// Registers each temporary warning so interruption and disable cleanup can destroy it.
    /// </summary>
    private BossAttackIndicator CreateIndicator()
    {
        GameObject go = new GameObject("BossIndicator");
        go.transform.position = new Vector3(0f, 0f, transform.position.z + 0.05f);
        BossAttackIndicator ind = go.AddComponent<BossAttackIndicator>();
        _activeIndicators.Add(ind);
        return ind;
    }

    /// <summary>
    /// Moves the digging warning while the target is still allowed to follow the player.
    /// </summary>
    private void UpdateTrackedIndicatorCenter(Vector2 center)
    {
        if (!_trackedIndicator) return;
        _trackedIndicator.UpdateCenter(center);
    }

    /// <summary>
    /// Marks current warnings imminent when digging locks its final strike position.
    /// </summary>
    private void ForceAllIndicatorsImminent()
    {
        foreach (BossAttackIndicator ind in _activeIndicators)
        {
            if (ind) ind.ForceImminent();
        }
    }

    /// <summary>
    /// Destroys every owned warning and clears tracked and laser references together.
    /// </summary>
    private void DestroyActiveIndicator()
    {
        foreach (BossAttackIndicator ind in _activeIndicators)
        {
            if (ind) Destroy(ind.gameObject);
        }
        _activeIndicators.Clear();
        _laserStretchSegments.Clear();
        _trackedIndicator = null;
    }

    /// <summary>
    /// Spawns self-cleaning puffs; a supplied path tangent spreads them across the tunnel rather than radially.
    /// </summary>
    private void SpawnRubbleBurst(Vector3 position, int puffs, float lifetime, Color color, Vector2 trailDirection = default)
    {
        if (puffs <= 0) return;
        bool alongPath = trailDirection.sqrMagnitude > 0.0001f;
        Vector2 dirN = alongPath ? trailDirection.normalized : Vector2.right;
        Vector2 perp = new Vector2(-dirN.y, dirN.x);

        for (int i = 0; i < puffs * 3; i++)
        {
            Vector2 offset;
            Vector2 burstVel;
            if (alongPath)
            {
                float along = Random.Range(-0.03f, 0.1f);
                float across = Random.Range(-0.11f, 0.11f);
                offset = dirN * along + perp * across + Random.insideUnitCircle * 0.035f;
                burstVel = perp * Random.Range(-0.65f, 0.65f) + dirN * Random.Range(-0.12f, 0.45f) + Random.insideUnitCircle * 0.12f;
            }
            else
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float rad = Random.Range(0.12f, 0.28f);
                offset = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
                float a2 = Random.Range(0f, Mathf.PI * 2f);
                burstVel = new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * Random.Range(0.45f, 1.15f) + Random.insideUnitCircle * 0.15f;
            }

            GameObject go = new GameObject("BossRubblePuff");
            go.transform.position = new Vector3(position.x + offset.x, position.y + offset.y, position.z + 0.02f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = RuntimeSprites.White;
            sr.color = color;
            if (spriteRenderer)
                sr.sortingLayerID = spriteRenderer.sortingLayerID;
            sr.sortingOrder = rubbleSortingOrder;
            float s = Random.Range(1.5f, 2.5f);
            go.transform.localScale = Vector3.one * s;
            go.AddComponent<BossRubblePuffFx>().Run(sr, color, lifetime, burstVel);
        }
    }

}
