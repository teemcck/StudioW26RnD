using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    private PlayerStats playerStats;

    [Header("Damage Tuning")]
    [Tooltip("Seconds of invulnerability after being hit.")]
    [SerializeField] private float invulnerableTime = 0.75f;
    [Tooltip("Default duration for BeginTeleporterArrivalGrace when not passing an explicit value.")]
    [SerializeField] private float defaultTeleporterArrivalGraceSeconds = 0.8f;
    [SerializeField] private float damageAnimationDuration = 0.46f;
    [SerializeField] private float damageStunDuration = 0.42f;
    [SerializeField] private float damageVisualKnockbackSpeed = 0.7f;
    [SerializeField] private float deathAnimationDuration = 0.32f;

    [Header("Passive Regeneration")]
    [Tooltip("Seconds after taking damage before passive regeneration resumes.")]
    [SerializeField] private float passiveRegenDelayAfterDamage = 10f;

    [Header("Hit Feedback")]
    [SerializeField] private float hitStopDuration = 0.05f;
    [SerializeField] private float hitShakeIntensity = 0.2f;
    [SerializeField] private int invulnerabilityBlinkCount = 5;

    [Header("VFX")]
    [SerializeField] private DamageFlash damageFlash;
    [SerializeField] private CameraController cameraController;

    private float _spawnTime;
    private bool _dead;
    private PlayerController _playerController;
    private PlayerDashController _dashController;
    private PlayerUpgradeRuntime _upgradeRuntime;

    public float CurrentHealth { get; private set; }

    /// <summary>
    /// Sets HP after cross-scene stat import (boss transition). Does not raise heal/damage events.
    /// </summary>
    public void ApplyBossTransitionPreserve(float absoluteHealth)
    {
        if (_dead)
            return;

        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();
        if (playerStats == null)
            return;

        float max = playerStats.MaxHealth;
        CurrentHealth = Mathf.Clamp(absoluteHealth, 0f, max);
    }

    /// <summary>CurrentHealth / MaxHealth, clamped to [0, 1]. Drives low-HP music/vignette.</summary>
    public float HealthNormalized
    {
        get
        {
            float max = playerStats != null ? playerStats.MaxHealth : 0f;
            if (max <= 0f)
                return 0f;
            return Mathf.Clamp01(CurrentHealth / max);
        }
    }

    private float _invulnerableUntil;
    private float _passiveRegenBlockedUntil;
    private float _teleportArrivalGraceUntil;

    /// <summary>
    /// After chunk teleports / floor start: brief window where enemy damage is ignored (contact, projectiles, etc.).
    /// </summary>
    public void BeginTeleporterArrivalGrace(float durationSeconds = -1f)
    {
        float d = durationSeconds >= 0f ? durationSeconds : defaultTeleporterArrivalGraceSeconds;
        d = Mathf.Max(0f, d);
        float until = Time.time + d;
        if (until > _teleportArrivalGraceUntil)
            _teleportArrivalGraceUntil = until;
    }

    public bool IsInTeleporterArrivalGrace => Time.time < _teleportArrivalGraceUntil;

    private void Start()
    {
        _spawnTime = Time.time;
        playerStats = GetComponent<PlayerStats>();
        _playerController = GetComponent<PlayerController>();
        _dashController = GetComponent<PlayerDashController>();
        _upgradeRuntime = GetComponent<PlayerUpgradeRuntime>();
        playerStats.StatChanged += OnStatChanged;
        float maxHealth = playerStats.Get(PlayerStatType.MaxHealth);
        CurrentHealth = maxHealth;
        if (!damageFlash) damageFlash = GetComponent<DamageFlash>();
        if (!cameraController)
            cameraController = Object.FindFirstObjectByType<CameraController>(FindObjectsInactive.Exclude);
    }

    private void OnDestroy()
    {
        if (playerStats != null)
            playerStats.StatChanged -= OnStatChanged;
    }

    private void Update()
    {
        if (_dead || playerStats == null)
            return;

        if (AudioManager.Instance == null)
            return;

        const float threshold = 0.25f;
        float norm = HealthNormalized;
        if (norm < threshold)
        {
            float t = 1f - Mathf.Clamp01(norm / threshold);
            AudioManager.Instance.SetLowHpFilter(true, t);
        }
        else
        {
            AudioManager.Instance.SetLowHpFilter(false, 0f);
        }

        float passiveRegenPerSecond = playerStats.HealthRegen;
        if (passiveRegenPerSecond <= 0f || Time.time < _passiveRegenBlockedUntil)
            return;

        float maxHealth = playerStats.MaxHealth;
        if (CurrentHealth >= maxHealth)
            return;

        Heal(passiveRegenPerSecond * Time.deltaTime);
    }

    public void TakeHit(float damage, Vector2 knockbackDirection, float knockbackForce, DamageContext context = default)
    {
        if (damage <= 0f) return;
        if (Time.time < _teleportArrivalGraceUntil) return;
        if (Time.time < _invulnerableUntil) return;
        if (StartupUpgradeDebugState.InfiniteHealthEnabled)
            return;

        if (_dashController != null && _dashController.IsDodgeInvulnerable)
        {
            HandlePerfectDodge();
            return;
        }

        float adjustedDamage = ApplyDamageReduction(damage);
        BlockPassiveRegen();

        float totalInvulnerability = Mathf.Max(invulnerableTime, damageStunDuration);
        _invulnerableUntil = Time.time + totalInvulnerability;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - adjustedDamage);
        if (_playerController)
        {
            float stunDuration = Mathf.Max(damageStunDuration, damageAnimationDuration * 0.9f);
            _playerController.PlayDamageAnimation(knockbackDirection, damageAnimationDuration);
            _playerController.ApplyDamageStun(knockbackDirection, stunDuration, damageVisualKnockbackSpeed);
        }
        if (Hitstop.Instance != null)
            Hitstop.Instance.Freeze(hitStopDuration, priority: 5);

        if (cameraController) cameraController.Shake(hitShakeIntensity);

        if (damageFlash)
        {
            damageFlash.Play();
            damageFlash.PlayInvulnerabilityBlink(totalInvulnerability, invulnerabilityBlinkCount, 0.08f);
        }

        EventBus<PlayerDamagedEvent>.Raise(new PlayerDamagedEvent { Amount = adjustedDamage, RemainingHP = CurrentHealth, HitPosition = transform.position, Source = context.SourceId ?? "enemy" });

        if (CurrentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        if (_dead) return;
        _dead = true;

        if (_playerController)
            _playerController.PlayDeathAnimation(_playerController.LastMoveDirection);

        var weapons = GetComponent<PlayerWeaponController>();
        if (weapons) weapons.enabled = false;
        var dashes = GetComponent<PlayerDashController>();
        if (dashes) dashes.enabled = false;

        EventBus<PlayerDiedEvent>.Raise(new PlayerDiedEvent
        {
            Position = transform.position,
            SurvivedForSeconds = Time.time - _spawnTime
        });

        Destroy(gameObject, deathAnimationDuration);
    }

    private void HandlePerfectDodge()
    {
        EventBus<PerfectDodgeEvent>.Raise(new PerfectDodgeEvent { Position = transform.position });

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayPerfectDodge();

        if (Hitstop.Instance != null)
            Hitstop.Instance.Freeze(0.08f, priority: 3);

        if (damageFlash != null)
            damageFlash.Play(GameColors.PerfectDodge);

        if (cameraController != null)
            cameraController.ShakeTap();

        PerfectDodgeSpeedBuff.ApplyTo(gameObject);
    }


    public void Heal(float amount)
    {
        if (_dead) return;
        if (amount <= 0f) return;
        
        CurrentHealth = Mathf.Min(playerStats.MaxHealth, CurrentHealth + amount);
        
        EventBus<PlayerHealedEvent>.Raise(new PlayerHealedEvent {Amount = amount, NewHP = CurrentHealth});
    }

    public void ApplyDirectDamage(float damage, string sourceId)
    {
        if (_dead || damage <= 0f)
            return;
        if (Time.time < _teleportArrivalGraceUntil)
            return;
        if (StartupUpgradeDebugState.InfiniteHealthEnabled)
            return;

        float adjustedDamage = ApplyDamageReduction(damage);
        BlockPassiveRegen();
        CurrentHealth = Mathf.Max(0f, CurrentHealth - adjustedDamage);
        EventBus<PlayerDamagedEvent>.Raise(new PlayerDamagedEvent
        {
            Amount = adjustedDamage,
            RemainingHP = CurrentHealth,
            HitPosition = transform.position,
            Source = sourceId
        });

        if (CurrentHealth <= 0f)
            Die();
    }

    private float ApplyDamageReduction(float damage)
    {
        float multiplier = _upgradeRuntime != null ? _upgradeRuntime.GetIncomingDamageMultiplier() : 1f;
        return Mathf.Max(0f, damage * multiplier);
    }

    private void BlockPassiveRegen()
    {
        _passiveRegenBlockedUntil = Time.time + Mathf.Max(0f, passiveRegenDelayAfterDamage);
    }

    private void OnStatChanged(PlayerStatType statType, float oldValue, float newValue)
    {
        if (statType != PlayerStatType.MaxHealth)
            return;

        float delta = newValue - oldValue;
        if (delta > 0f)
            CurrentHealth += delta;

        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, newValue);
    }
}
