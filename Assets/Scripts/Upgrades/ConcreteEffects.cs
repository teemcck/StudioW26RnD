using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StatChangeEffect : UpgradeEffect
{
    public PlayerStatType stat;
    [Tooltip("Additive multiplier delta. 1 = +100%. -.25 = -25%.")]
    public float multiplier;
    [Tooltip("Additive flat. 1 = +1.0. -.25 = -0.25")]
    public float flat;

    public override void Apply(UpgradeContext ctx)
    {
        if (multiplier != 0) ctx.Stats.AddMultiplier(stat, multiplier);
        if (flat != 0) ctx.Stats.AddFlat(stat, flat);
    }

    public override void Remove(UpgradeContext ctx)
    {
        if (multiplier != 0) ctx.Stats.AddMultiplier(stat, -multiplier);
        if (flat != 0) ctx.Stats.AddFlat(stat, -flat);
    }

    public override string GetDescription()
    {
        var parts = new List<string>();

        if (multiplier != 0) parts.Add($"{(multiplier >= 0 ? "+" : "")}{multiplier * 100:0}%");
        if (flat != 0) parts.Add($"{(flat >= 0 ? "+" : "")}{flat}");

        return string.Join(", ", parts);
    }
}

[System.Serializable]
public class SpawnMultiplierEffect : UpgradeEffect
{
    [Tooltip("Multiplier applied to the spawn manager's global spawn scale. " +
             "1.5 = 50% more enemies. Stack carefully.")]
    public float multiplier = 1.5f;

    public override void Apply(UpgradeContext ctx)
        => ctx.SpawnManager.AddSpawnMultiplier(multiplier);

    public override void Remove(UpgradeContext ctx)
        => ctx.SpawnManager.AddSpawnMultiplier(1f / multiplier);

    public override string GetDescription()
        => $"{multiplier:0.##}x enemy spawn rate";
}

[System.Serializable]
public class SpawnPoolModifierEffect : UpgradeEffect
{
    [Tooltip("Enemy type tags to add to the active spawn pool.")]
    public string[] addToPool;

    [Tooltip("Enemy type tags to remove from the active spawn pool.")]
    public string[] removeFromPool;

    public override void Apply(UpgradeContext ctx)
    {
        foreach (var tag in addToPool) ctx.SpawnManager.AddEnemyType(tag);
        foreach (var tag in removeFromPool) ctx.SpawnManager.RemoveEnemyType(tag);
    }

    public override void Remove(UpgradeContext ctx)
    {
        foreach (var tag in removeFromPool) ctx.SpawnManager.AddEnemyType(tag);
        foreach (var tag in addToPool) ctx.SpawnManager.RemoveEnemyType(tag);
    }

    public override string GetDescription()
    {
        var parts = new List<string>();
        if (addToPool.Length > 0) parts.Add($"Adds {string.Join(", ", addToPool)} to spawns");
        if (removeFromPool.Length > 0) parts.Add($"Removes {string.Join(", ", removeFromPool)} from spawns");
        return string.Join(". ", parts);
    }
}

[System.Serializable]
public class GameRuleEffect : UpgradeEffect
{
    public GameRuleType ruleType;
    public float flatDelta;
    public float multiplierDelta;

    public override void Apply(UpgradeContext ctx)
    {
        if (flatDelta != 0) ctx.GameRules.AddFlat(ruleType, flatDelta);
        if (multiplierDelta != 0) ctx.GameRules.AddMultiplier(ruleType, multiplierDelta);
    }

    public override void Remove(UpgradeContext ctx)
    {
        if (flatDelta != 0) ctx.GameRules.AddFlat(ruleType, -flatDelta);
        if (multiplierDelta != 0) ctx.GameRules.AddMultiplier(ruleType, -multiplierDelta);
    }

    public override string GetDescription()
    {
        string s = ruleType.ToDisplayString();
        var parts = new List<string>();
        if (flatDelta != 0) parts.Add($"{(flatDelta >= 0 ? "+" : "")}{flatDelta} {s}");
        if (multiplierDelta != 0) parts.Add($"{(multiplierDelta >= 0 ? "+" : "")}{multiplierDelta * 100:0}% {s}");
        return string.Join(", ", parts);
    }
}

[System.Serializable]
public abstract class TriggerEffect : UpgradeEffect
{
    public override string GetDescription() => "(Trigger effect, see subclass)";
}

[System.Serializable]
public class OnKillHealEffect : TriggerEffect
{
    [Tooltip("HP restored per kill.")]
    float healAmount = 5f;

    protected IEventBinding<EnemyKilledEvent> _binding;
    private PlayerHealth _health;

    public override void Apply(UpgradeContext ctx)
    {
        _health = ctx.Player.GetComponent<PlayerHealth>();
        _binding = EventBus<EnemyKilledEvent>.Register(HandleKill);
    }

    public override void Remove(UpgradeContext ctx)
        => EventBus<EnemyKilledEvent>.Unsubscribe(_binding);

    private void HandleKill(EnemyKilledEvent evt)
    {
        _health?.Heal(healAmount);
    }

    public override string GetDescription() => $"Heal {healAmount} HP on kill";
}

[System.Serializable]
public class PostDashStatEffect : TriggerEffect
{
    [SerializeField] private PlayerStatType stat = PlayerStatType.AttackDamage;
    [SerializeField] private float flatBonus = 5f;
    [SerializeField] private float duration = 3f;

    private PlayerStats _stats;
    private float _timer;
    private bool _buffActive;

    protected IEventBinding<PlayerDashedEvent> _binding;

    public override bool NeedsTick => _buffActive;

    public override void Apply(UpgradeContext ctx)
    {
        _stats = ctx.Stats;
        _binding = EventBus<PlayerDashedEvent>.Register(HandleDash);
    }

    public override void Remove(UpgradeContext ctx)
    {
        EventBus<PlayerDashedEvent>.Unsubscribe(_binding);
        if (_buffActive) RemoveBuff();
    }

    private void HandleDash(PlayerDashedEvent evt)
    {
        if (!_buffActive)
        {
            _stats.AddFlat(stat, flatBonus);
            _buffActive = true;
        }
        _timer = duration;
    }

    public override void Tick(UpgradeContext ctx, float dt)
    {
        if (!_buffActive) return;
        _timer -= dt;
        if (_timer <= 0f) RemoveBuff();
    }

    private void RemoveBuff()
    {
        _stats.AddFlat(stat, -flatBonus);
        _buffActive = false;
    }

    public override string GetDescription()
        => $"+{flatBonus} {stat.ToDisplayString()} for {duration}s after dashing";
}

[System.Serializable]
public class ConditionalEffect : UpgradeEffect
{
    public ConditionalType condition;
    public float threshold;

    [SerializeReference, SubclassSelector]
    public List<UpgradeEffect> whenTrue = new();
    [SerializeReference, SubclassSelector]
    public List<UpgradeEffect> whenFalse = new();

    public override void Apply(UpgradeContext ctx)
        => ctx.Player.GetComponent<ConditionalEffectRunner>()?.Register(this, ctx);

    public override void Remove(UpgradeContext ctx)
        => ctx.Player.GetComponent<ConditionalEffectRunner>()?.Unregister(this, ctx);

    public override bool NeedsTick => false;

    public override string GetDescription() => $"Conditional effect ({condition} {threshold})";
}

public enum ConditionalType { HealthBelow, HealthAbove, FloorBelow, FloorAbove, Custom }

[System.Serializable]
public class OnMeleeAttackPerEnemyEffect : UpgradeEffect
{
    [Tooltip("Damage bonus per enemy within range.")]
    public float damagePerEnemy = 1f;

    [Tooltip("Range to search for nearby enemies (in units).")]
    public float searchRadius = 2f;

    private PlayerStats _stats;
    private PlayerController _player;
    private IEventBinding<PlayerMeleeAttackEvent> _binding;

    public override void Apply(UpgradeContext ctx)
    {
        _stats = ctx.Stats;
        _player = ctx.Player;
        _binding = EventBus<PlayerMeleeAttackEvent>.Register(HandleMeleeAttack);
    }

    public override void Remove(UpgradeContext ctx)
    {
        EventBus<PlayerMeleeAttackEvent>.Unsubscribe(_binding);
    }

    private void HandleMeleeAttack(PlayerMeleeAttackEvent evt)
    {
        if (evt.EnemiesHit <= 0 || _player == null) return;

        float bonusDamage = damagePerEnemy * evt.EnemiesHit;
        _stats.AddFlat(PlayerStatType.AttackDamage, bonusDamage);
        _player.StartCoroutine(StripAttackBonusEndOfFrame(bonusDamage));
    }

    private IEnumerator StripAttackBonusEndOfFrame(float bonusDamage)
    {
        yield return new WaitForEndOfFrame();
        _stats.AddFlat(PlayerStatType.AttackDamage, -bonusDamage);
    }

    public override string GetDescription()
        => $"+{damagePerEnemy} attack damage per enemy in melee range";
}

[System.Serializable]
public class LifestealEffect : UpgradeEffect
{
    [Tooltip("Percentage of damage dealt that is converted to healing (0.2 = 20%).")]
    public float lifestealPercent = 0.2f;

    private PlayerHealth _playerHealth;
    private IEventBinding<EnemyKilledEvent> _binding;

    public override void Apply(UpgradeContext ctx)
    {
        _playerHealth = ctx.Player.GetComponent<PlayerHealth>();
        _binding = EventBus<EnemyKilledEvent>.Register(HandleKill);
    }

    public override void Remove(UpgradeContext ctx)
    {
        EventBus<EnemyKilledEvent>.Unsubscribe(_binding);
    }

    private void HandleKill(EnemyKilledEvent evt)
    {
        if (_playerHealth)
            _playerHealth.Heal(5f);
    }

    public override string GetDescription()
        => $"{lifestealPercent * 100:0}% lifesteal on hits";
}

[System.Serializable]
public class OnAttackApplyStatusEffect : UpgradeEffect
{
    [Tooltip("Name of the status effect to apply (e.g., 'swiftness', 'haste').")]
    public string statusEffectId = "swiftness";

    [Tooltip("Duration of the status effect in seconds.")]
    public float duration = 1f;

    [Tooltip("Maximum stacks of this effect.")]
    public int maxStacks = 3;

    private IEventBinding<PlayerMeleeAttackEvent> _binding;
    private StatusEffectManager _statusManager;

    public override void Apply(UpgradeContext ctx)
    {
        _statusManager = ctx.StatusEffects;
        if (!_statusManager)
        {
            Debug.LogWarning("StatusEffectManager not found!");
            return;
        }
        _binding = EventBus<PlayerMeleeAttackEvent>.Register(HandleAttack);
    }

    public override void Remove(UpgradeContext ctx)
    {
        EventBus<PlayerMeleeAttackEvent>.Unsubscribe(_binding);
    }

    private void HandleAttack(PlayerMeleeAttackEvent evt)
    {
        if (_statusManager)
            _statusManager.Apply(statusEffectId, duration, maxStacks);
    }

    public override string GetDescription()
        => $"Apply {statusEffectId} for {duration}s on attack (stacks up to {maxStacks}x)";
}

[System.Serializable]
public class PerEnemyDamageReductionEffect : UpgradeEffect
{
    [Tooltip("Damage reduction per enemy in range (0.1 = 10% per enemy).")]
    public float reductionPerEnemy = 0.1f;

    [Tooltip("Range to search for enemies.")]
    public float searchRadius = 2f;

    public override void Apply(UpgradeContext ctx)
    {
    }

    public override void Remove(UpgradeContext ctx)
    {
    }

    public override string GetDescription()
        => $"{reductionPerEnemy * 100:0}% damage reduction per nearby enemy";
}
