using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    private PlayerStats playerStats;

    [Header("Damage Tuning")]
    [Tooltip("Seconds of invulnerability after being hit.")]
    [SerializeField] private float invulnerableTime = 0.75f;
    [SerializeField] private float damageAnimationDuration = 0.46f;
    [SerializeField] private float damageStunDuration = 0.42f;
    [SerializeField] private float damageVisualKnockbackSpeed = 0.7f;
    [SerializeField] private float deathAnimationDuration = 0.32f;

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
    private Coroutine _hitStopCo;
    private bool _isHitStopActive;
    private float _savedTimeScale = 1f;
    private PlayerUpgradeRuntime _upgradeRuntime;

    public float CurrentHealth { get; private set; }

    private float _invulnerableUntil;

    private void Start()
    {
        _spawnTime = Time.time;
        playerStats = GetComponent<PlayerStats>();
        _playerController = GetComponent<PlayerController>();
        _upgradeRuntime = GetComponent<PlayerUpgradeRuntime>();
        playerStats.StatChanged += OnStatChanged;
        float maxHealth = playerStats.Get(PlayerStatType.MaxHealth);
        CurrentHealth = maxHealth;
        if (!damageFlash) damageFlash = GetComponent<DamageFlash>();
        if (!cameraController) cameraController = FindObjectOfType<CameraController>();
    }

    private void OnDestroy()
    {
        if (playerStats != null)
            playerStats.StatChanged -= OnStatChanged;
    }

    public void TakeHit(float damage, Vector2 knockbackDirection, float knockbackForce, DamageContext context = default)
    {
        if (damage <= 0f) return;
        if (Time.time < _invulnerableUntil) return;

        float adjustedDamage = ApplyDamageReduction(damage);

        float totalInvulnerability = Mathf.Max(invulnerableTime, damageStunDuration);
        _invulnerableUntil = Time.time + totalInvulnerability;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - adjustedDamage);
        if (_playerController)
        {
            float stunDuration = Mathf.Max(damageStunDuration, damageAnimationDuration * 0.9f);
            _playerController.PlayDamageAnimation(knockbackDirection, damageAnimationDuration);
            _playerController.ApplyDamageStun(knockbackDirection, stunDuration, damageVisualKnockbackSpeed);
        }
        if (_hitStopCo != null)
        {
            StopCoroutine(_hitStopCo);
            _hitStopCo = null;
            if (_isHitStopActive)
            {
                Time.timeScale = _savedTimeScale;
                _isHitStopActive = false;
            }
        }
        _hitStopCo = StartCoroutine(HitStopRoutine(hitStopDuration));

        if (cameraController) cameraController.Shake(hitShakeIntensity);

        if (damageFlash)
        {
            damageFlash.Play();
            damageFlash.PlayInvulnerabilityBlink(totalInvulnerability, invulnerabilityBlinkCount, 0.08f);
        }

        Debug.Log($"Player hit for {adjustedDamage}. HP: {CurrentHealth}/{playerStats.MaxHealth}");
        EventBus<PlayerDamagedEvent>.Raise(new PlayerDamagedEvent {Amount = adjustedDamage, RemainingHP = CurrentHealth, HitPosition = transform.position, Source = context.SourceId ?? "enemy"}); 
        
        if (CurrentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        if (_dead) return;
        _dead = true;
        
        Debug.Log("Player died.");
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

    private System.Collections.IEnumerator HitStopRoutine(float duration)
    {
        if (duration <= 0f) yield break;

        _savedTimeScale = Time.timeScale;
        _isHitStopActive = true;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);

        // Restore to what the game had before hit-stop.
        Time.timeScale = _savedTimeScale;
        _isHitStopActive = false;
        _hitStopCo = null;
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

        float adjustedDamage = ApplyDamageReduction(damage);
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
