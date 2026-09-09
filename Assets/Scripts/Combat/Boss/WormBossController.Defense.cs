using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Incoming damage, shield absorption, HP reactions, and shield-break feedback for WormBossController.
/// EnemyBase remains the HP authority; breaking a shield never spills damage into HP.
/// </summary>
public sealed partial class WormBossController
{
    // Shield is independent of EnemyBase HP. Broken shields cannot use passive regeneration.
    private float _currentShield;

    private float _lastShieldDamageTime;

    private bool _shieldBroken;

    // A brief HP reaction and the longer shield-break reaction share the combat stun flag.
    private Coroutine _damageRoutine;

    private bool _isStunned;

    // Keep a stable surfaced tint so repeated shield pulses do not compound their colors.
    private Coroutine _shieldPingRoutine;

    private Color _shieldPingBaseColor = Color.white;

    private Coroutine _shieldBreakFeedbackRoutine;

    // Child visuals wobble locally; root sprites use the root-rotation fallback in the feedback routine.
    private Vector3 _spriteVisualBaseLocal;

    private Quaternion _spriteVisualBaseLocalRot = Quaternion.identity;

    private bool _spriteVisualIsChild;

    /// <summary>
    /// Derives the phase from remaining HP; thresholds are inclusive and a large hit may skip a phase.
    /// </summary>
    public int CurrentPhase
    {
        get
        {
            if (HealthNormalized <= phaseThreeHp) return 3;
            if (HealthNormalized <= phaseTwoHp) return 2;
            return 1;
        }
    }

    /// <summary>
    /// Current shield fill relative to this phase, clamped for the health bar.
    /// </summary>
    public float CurrentShieldNormalized => GetMaxShieldForCurrentPhase() <= 0.0001f ? 0f : Mathf.Clamp01(_currentShield / GetMaxShieldForCurrentPhase());

    /// <summary>
    /// Reads the current phase's shield capacity using the existing short-array fallback behavior.
    /// </summary>
    private float GetMaxShieldForCurrentPhase()
    {
        return GetArrayValue(maxShieldByPhase, CurrentPhase - 1, 58f);
    }

    /// <summary>
    /// Consumes the entire hit whenever shield remains, discarding overflow damage and starting stun when it breaks.
    /// </summary>
    private bool AbsorbDamageIntoShield(float damage, Vector2 shieldBreakKnockbackHint)
    {
        if (damage <= 0f || _currentShield <= 0f)
            return false;

        float maxS = GetMaxShieldForCurrentPhase();
        float absorbed = Mathf.Min(_currentShield, damage);
        _currentShield -= absorbed;
        ResolveBossAudio()?.PlayShieldDamageSfx();
        if (maxS > 0f && _currentShield < maxS)
            _lastShieldDamageTime = Time.time;
        UpdateShieldUI();
        PlayShieldPingFlash();
        if (_currentShield <= 0f)
        {
            _currentShield = 0f;
            _shieldBroken = true;
            if (healthBarUI) healthBarUI.NotifyShieldBroken();
            PriorityCancelOngoingMoves();
            _isStunned = true;
            _state = InternalState.Stunned;
            if (_rb)
                _rb.linearVelocity = Vector2.zero;
            CrossFadeAnimState(StateDamage, 0.1f);
            if (_shieldBreakFeedbackRoutine != null)
                StopCoroutine(_shieldBreakFeedbackRoutine);
            Vector2 kb = shieldBreakKnockbackHint.sqrMagnitude > 0.0001f
                ? shieldBreakKnockbackHint.normalized
                : -AimDirectionToPlayer();
            _shieldBreakFeedbackRoutine = StartCoroutine(ShieldBreakFeedbackRoutine(kb));
        }

        return true;
    }

    /// <summary>
    /// Routes direct hits through encounter immunity and shield absorption before delegating HP damage to EnemyBase.
    /// </summary>
    public override void TakeHit(float damage, Vector2 knockbackDirection, float knockbackForce, DamageContext context = default)
    {
        if (IsDead || _state == InternalState.Dead) return;
        if (_introActive) return;
        if (_phaseTransitionActive) return;
        if (damage <= 0f) return;

        if (_immuneDuringDigMove || _digInvulnerable)
            return;

        float maxS = GetMaxShieldForCurrentPhase();

        // A breaking hit is fully consumed even when its damage exceeds the remaining shield.
        if (AbsorbDamageIntoShield(damage, knockbackDirection))
            return;

        if (maxS > 0f && _currentShield < maxS)
            _lastShieldDamageTime = Time.time;

        ResolveBossAudio()?.PlayBossDamageSfx();
        float healthBarNormBeforeHpHit = HealthNormalized;
        base.TakeHit(damage, knockbackDirection, knockbackForce, context);
        if (IsDead)
        {
            _deathHealthBarDrainFromNormalized = healthBarNormBeforeHpHit;
            return;
        }

        if (!_attackActive)
            StartDamageReaction();
    }

    /// <summary>
    /// Routes periodic damage through the same immunity and shield rules without adding a normal hit reaction.
    /// </summary>
    public override void ApplyStatusDamage(float damage, DamageContext context)
    {
        if (IsDead || _state == InternalState.Dead) return;
        if (_introActive) return;
        if (_phaseTransitionActive) return;
        if (damage <= 0f) return;
        if (_immuneDuringDigMove || _digInvulnerable) return;

        if (AbsorbDamageIntoShield(damage, -AimDirectionToPlayer()))
            return;

        base.ApplyStatusDamage(damage, context);
    }

    /// <summary>
    /// Restarts the short HP hit reaction so repeated idle hits refresh its duration.
    /// </summary>
    private void StartDamageReaction()
    {
        if (_damageRoutine != null) StopCoroutine(_damageRoutine);
        _damageRoutine = StartCoroutine(DamageReactionRoutine());
    }

    /// <summary>
    /// Temporarily stuns an idle boss and restores its idle animation when the reaction completes.
    /// </summary>
    private IEnumerator DamageReactionRoutine()
    {
        _isStunned = true;
        _state = InternalState.Stunned;
        PlayAnimState(StateDamage);
        if (_rb) _rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(damageStunDuration);
        _isStunned = false;
        if (!IsDead)
        {
            _state = InternalState.Idle;
            PlayAnimState(StateIdle);
        }
        _damageRoutine = null;
    }

    /// <summary>
    /// Restarts the shield tint pulse from the cached surfaced color.
    /// </summary>
    private void PlayShieldPingFlash()
    {
        if (!spriteRenderer) return;
        if (_shieldPingRoutine != null) StopCoroutine(_shieldPingRoutine);
        _shieldPingRoutine = StartCoroutine(ShieldPingRoutine());
    }

    /// <summary>
    /// Pulses the sprite tint over scaled time, then restores its original color.
    /// </summary>
    private IEnumerator ShieldPingRoutine()
    {
        if (!spriteRenderer) yield break;
        Color baseColor = _shieldPingBaseColor;
        float t = 0f;
        while (t < shieldPingDuration && spriteRenderer)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / shieldPingDuration);
            float amount = Mathf.Sin(u * Mathf.PI);
            spriteRenderer.color = Color.Lerp(baseColor, shieldPingColor, amount);
            yield return null;
        }
        if (spriteRenderer) spriteRenderer.color = baseColor;
        _shieldPingRoutine = null;
    }

    /// <summary>
    /// Pushes normalized shield fill to the optional encounter HUD.
    /// </summary>
    private void UpdateShieldUI()
    {
        if (!healthBarUI) return;
        healthBarUI.SetShield(CurrentShieldNormalized);
    }

    /// <summary>
    /// Restores a child sprite's authored local pose after procedural stun wobble.
    /// </summary>
    private void ResetSpriteVisualLocalIfChild()
    {
        if (!_spriteVisualIsChild || !spriteRenderer) return;
        spriteRenderer.transform.localPosition = _spriteVisualBaseLocal;
        spriteRenderer.transform.localRotation = _spriteVisualBaseLocalRot;
    }

    /// <summary>
    /// Regenerates an unbroken shield after its idle delay; only phase transitions restore a broken shield.
    /// </summary>
    private void TickShieldRegeneration()
    {
        float maxShield = GetMaxShieldForCurrentPhase();
        float fillDur = Mathf.Max(0.1f, shieldRegenFillSeconds);
        float regenPerSec = maxShield / fillDur;
        if (!_phaseTransitionActive && !_shieldBroken && maxShield > 0f && _currentShield < maxShield
            && Time.time - _lastShieldDamageTime >= shieldRegenIdleSeconds)
        {
            _currentShield = Mathf.Min(maxShield, _currentShield + regenPerSec * Time.deltaTime);
            UpdateShieldUI();
        }
    }

    /// <summary>
    /// Combines hitstop, knockback, shockwave, and wobble until stun ends or a phase transition takes over.
    /// </summary>
    private IEnumerator ShieldBreakFeedbackRoutine(Vector2 knockbackDirection)
    {
        CameraController camCtrl = phaseTransitionCamera ? phaseTransitionCamera : FindFirstObjectByType<CameraController>();
        if (camCtrl)
            camCtrl.Shake(Mathf.Max(0.05f, shieldBreakCameraShake));

        ResolveBossAudio()?.PlayShieldBreakSfx();

        ShieldBreakShockwaveVfx.Spawn(transform, spriteRenderer);

        if (HitstopController.Instance != null)
            HitstopController.Instance.Freeze(0.055f, priority: 7);
        // Gameplay time may be frozen here; a scaled wait would stall the impact feedback.
        yield return new WaitForSecondsRealtime(0.055f);

        if (Rb)
        {
            Rb.linearVelocity = Vector2.zero;
            Vector2 dir = knockbackDirection.sqrMagnitude > 0.0001f ? knockbackDirection.normalized : -AimDirectionToPlayer();
            Rb.AddForce(dir * shieldBreakKnockbackForce, ForceMode2D.Impulse);
        }

        float endTime = Time.time + Mathf.Max(0.1f, shieldBreakStunDuration);
        float zWobbleBase = transform.eulerAngles.z;

        while (Time.time < endTime && !IsDead && !_phaseTransitionActive)
        {
            float t = Time.time;
            if (_spriteVisualIsChild && spriteRenderer)
            {
                float wx = Mathf.Sin(t * 13f) * shieldBreakChildWobbleX
                    + Mathf.Sin(t * 21f) * (shieldBreakChildWobbleX * 0.35f);
                float wy = Mathf.Sin(t * 10f) * shieldBreakChildWobbleY
                    + Mathf.Sin(t * 16.5f) * (shieldBreakChildWobbleY * 0.4f);
                float rz = Mathf.Sin(t * 11.3f) * shieldBreakChildWobbleDegrees
                    + Mathf.Sin(t * 18f) * (shieldBreakChildWobbleDegrees * 0.42f);
                spriteRenderer.transform.localPosition = _spriteVisualBaseLocal + new Vector3(wx, wy, 0f);
                spriteRenderer.transform.localRotation = _spriteVisualBaseLocalRot * Quaternion.Euler(0f, 0f, rz);
            }
            else
            {
                float dz = Mathf.Sin(t * 11f) * shieldBreakWobbleDegrees
                    + Mathf.Sin(t * 17.5f) * (shieldBreakWobbleDegrees * 0.42f)
                    + Mathf.Sin(t * 6.2f) * (shieldBreakWobbleDegrees * 0.22f);
                transform.rotation = Quaternion.Euler(0f, 0f, zWobbleBase + dz);
            }

            yield return null;
        }

        if (!_spriteVisualIsChild)
            transform.rotation = Quaternion.Euler(0f, 0f, zWobbleBase);

        ResetSpriteVisualLocalIfChild();

        _isStunned = false;
        _shieldBreakFeedbackRoutine = null;
        if (!IsDead && !_phaseTransitionActive)
        {
            _state = InternalState.Idle;
            PlayAnimState(StateIdle);
        }
    }
}
