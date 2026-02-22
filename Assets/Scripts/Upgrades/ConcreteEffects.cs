using UnityEngine;

// STAT EFFECTS
// flat, additive, and percentage multiplier

/// <summary>
/// Adds a flat value to a single PlayerStat field.
/// Example: +2 dash count, +5 attack damage.
/// </summary>
[System.Serializable]
public class StatFlatEffect : UpgradeEffect
{
    public PlayerStatType stat;
    public float value;

    public override void Apply(UpgradeContext ctx)  => ctx.Stats.AddFlat(stat, value);
    public override void Remove(UpgradeContext ctx) => ctx.Stats.AddFlat(stat, -value);
    public override string GetDescription() => $"{(value >= 0 ? "+" : "")}{value} {stat.ToDisplayString()}";
}

/// <summary>
/// Multiplies a single PlayerStat by (1 + percent).
/// Example: percent = 1.0 -> double damage. percent = -0.25 -> 25% less move speed.
/// </summary>
[System.Serializable]
public class StatMultiplierEffect : UpgradeEffect
{
    public PlayerStatType stat;
    [Tooltip("Additive multiplier delta. 1.0 = +100%. -0.25 = -25%.")]
    public float percent;

    public override void Apply(UpgradeContext ctx) => ctx.Stats.AddMultiplier(stat, percent);
    public override void Remove(UpgradeContext ctx) => ctx.Stats.AddMultiplier(stat, -percent);

    public override string GetDescription()
    {
        string sign = percent >= 0 ? "+" : "";
        return $"{sign}{percent * 100:0}% {stat.ToDisplayString()}";
    }
}

// SPAWN EFFECTS, modify enemy spawn parameters

/// <summary>
/// Multiplies the enemy spawn count/rate by a given factor.
/// Example: 1.5 -> 50% more enemies. 0.5 -> half as many.
/// </summary>
[System.Serializable]
public class SpawnMultiplierEffect : UpgradeEffect
{
    [Tooltip("Multiplier applied to the spawn manager's global spawn scale. " +
             "1.5 = 50% more enemies. Stack carefully.")]
    public float multiplier = 1.5f;

    public override void Apply(UpgradeContext ctx)
        => ctx.SpawnManager.AddSpawnMultiplier(multiplier);

    public override void Remove(UpgradeContext ctx)
        => ctx.SpawnManager.AddSpawnMultiplier(1f / multiplier);   // inverse

    public override string GetDescription()
        => $"{multiplier:0.##}× enemy spawn rate";
}

/// <summary>
/// Adds or removes specific enemy types from the spawn pool.
/// Example: "Elites Only" upgrade that disables normal enemies and enables elite variants.
/// </summary>
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
        // Reverse: add back what was removed, remove what was added
        foreach (var tag in removeFromPool) ctx.SpawnManager.AddEnemyType(tag);
        foreach (var tag in addToPool) ctx.SpawnManager.RemoveEnemyType(tag);
    }

    public override string GetDescription()
    {
        var parts = new System.Collections.Generic.List<string>();
        if (addToPool.Length > 0)    parts.Add($"Adds {string.Join(", ", addToPool)} to spawns");
        if (removeFromPool.Length > 0) parts.Add($"Removes {string.Join(", ", removeFromPool)} from spawns");
        return string.Join(". ", parts);
    }
}

// GAME RULE EFFECTS, modify broader game rules

/// <summary>
/// Changes a global GameRules value (gold multiplier, XP rate, floor count, etc.)
/// </summary>
[System.Serializable]
public class GameRuleEffect : UpgradeEffect
{
    public GameRuleType ruleType;
    public float flatDelta;
    public float multiplierDelta; // additive % delta (same convention as StatMultiplierEffect)

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
        var parts = new System.Collections.Generic.List<string>();
        if (flatDelta != 0) parts.Add($"{(flatDelta >= 0 ? "+" : "")}{flatDelta} {s}");
        if (multiplierDelta != 0) parts.Add($"{(multiplierDelta >= 0 ? "+" : "")}{multiplierDelta * 100:0}% {s}");
        return string.Join(", ", parts);
    }
}

// TRIGGER / EVENT EFFECTS, fire logic on specific game events

/// <summary>
/// Base for effects that hook into game events rather than mutating stats.
/// Subclass this for "on kill", "on dash", "on hit", etc. effects.
/// Subscribe in Apply, unsubscribe in Remove, always pair them.
///
/// Example: OnKillEffect that spawns a coin at the enemy's position.
/// </summary>
[System.Serializable]
public abstract class TriggerEffect : UpgradeEffect
{
    // Apply/Remove handle event subscription, no stat mutation needed.
    // Subclasses define which event they hook and what they do.
    public override string GetDescription() => "(Trigger effect, see subclass)";
}

/// <summary>
/// Concrete example: heals the player for a flat amount on each kill.
/// Needs further implementation.
/// </summary>
[System.Serializable]
public class OnKillHealEffect : TriggerEffect
{
    [Tooltip("HP restored per kill.")] 
    float healAmount = 5f;
    
    protected IEventBinding<PlayerKilledEnemyEvent> _binding;

    public override void Apply(UpgradeContext ctx)
    {
        // _health  = ctx.Player.GetComponent<PlayerController>().health;
        _binding = EventBus<PlayerKilledEnemyEvent>.Register(HandleKill);
    }

    public override void Remove(UpgradeContext ctx)
        => EventBus<PlayerKilledEnemyEvent>.Unsubscribe(_binding);

    // private void HandleKill(PlayerKilledEnemyEvent evt)
    //     => _health?.Heal(healAmount);

    // Finish when PlayerController updated to have more control variables.
    private void HandleKill(PlayerKilledEnemyEvent evt)
    {
        Debug.Log("Healed player: " + healAmount);
    }

    public override string GetDescription() => $"Heal {healAmount} HP on kill";
}

/// <summary>
/// Concrete example: increases attack damage for a few seconds after dashing.
/// Effect made to showcase "assassin" upgrade. Can be used elsewhere.
/// </summary>
[System.Serializable]
public class PostDashDamageBuffEffect : TriggerEffect
{
    public float damageBonus = 5f;
    public float duration = 3f;

    private PlayerStats _stats;
    private float _timer = 0f;
    private bool _buffActive = false;
    
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
            _stats.AddFlat(PlayerStatType.AttackDamage, damageBonus);
            _buffActive = true;
        }
        _timer = duration;  // Refresh timer on each dash
    }

    public override void Tick(UpgradeContext ctx, float dt)
    {
        if (!_buffActive) return;
        _timer -= dt;
        if (_timer <= 0f) RemoveBuff();
    }

    private void RemoveBuff()
    {
        _stats.AddFlat(PlayerStatType.AttackDamage, -damageBonus);
        _buffActive = false;
    }

    public override string GetDescription()
        => $"+{damageBonus} attack damage for {duration}s after dashing";
}

// CONDITIONAL EFFECTS, apply different sub-effects based on context

/// <summary>
/// Applies one set of effects if the player meets a condition, another set otherwise.
/// Example: "Berserker," +50% damage below 30% HP, -10% damage above 30% HP.
///
/// This wraps other UpgradeEffects rather than duplicating logic.
/// </summary>
[System.Serializable]
public class ConditionalEffect : UpgradeEffect
{
    public ConditionalType condition;
    public float           threshold;   // meaning depends on ConditionalType

    [SerializeReference]
    public System.Collections.Generic.List<UpgradeEffect> whenTrue  = new();
    [SerializeReference]
    public System.Collections.Generic.List<UpgradeEffect> whenFalse = new();

    // ConditionalEffects are managed by their own runtime component, not directly.
    // Apply registers the condition check; Remove cleans it up.
    public override void Apply(UpgradeContext ctx)
        => ctx.Player.GetComponent<ConditionalEffectRunner>()?.Register(this, ctx);

    public override void Remove(UpgradeContext ctx)
        => ctx.Player.GetComponent<ConditionalEffectRunner>()?.Unregister(this, ctx);

    public override bool NeedsTick => false;  // ConditionalEffectRunner handles its own Update

    public override string GetDescription() => $"Conditional effect ({condition} {threshold})";
}

public enum ConditionalType { HealthBelow, HealthAbove, FloorBelow, FloorAbove, Custom }
