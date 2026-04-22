using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Stat type enum - add new stats here; everything else auto-picks them up

// This can obviously be expanded on later, these are just examples.
public enum PlayerStatType
{
    // Movement
    MoveSpeed,
    // Dash
    DashSpeed, DashCount, DashCooldown, DashDistance,
    // Combat
    AttackDamage, AttackSpeed, AttackRange, CritChance, CritMultiplier,
    // Defence
    MaxHealth, HealthRegen, DamageReduction, DodgeChance,
    // Economy
    XPMultiplier
}

public static class PlayerStatTypeExtensions
{
    public static string ToDisplayString(this PlayerStatType t) => t switch
    {
        PlayerStatType.MoveSpeed      => "Move Speed",
        PlayerStatType.DashSpeed      => "Dash Speed",
        PlayerStatType.DashCount      => "Dash Count",
        PlayerStatType.DashCooldown   => "Dash Cooldown",
        PlayerStatType.DashDistance   => "Dash Distance",
        PlayerStatType.AttackDamage   => "Attack Damage",
        PlayerStatType.AttackSpeed    => "Attack Speed",
        PlayerStatType.AttackRange    => "Attack Range",
        PlayerStatType.CritChance     => "Crit Chance",
        PlayerStatType.CritMultiplier => "Crit Multiplier",
        PlayerStatType.MaxHealth      => "Max Health",
        PlayerStatType.HealthRegen    => "Health Regen",
        PlayerStatType.DamageReduction => "DamageReduction",
        PlayerStatType.DodgeChance    => "Dodge Chance",
        PlayerStatType.XPMultiplier   => "XP Multiplier",
        _                             => t.ToString()
    };
}

// Individual stat, base value + flat bonuses + multiplier bonuses

[Serializable]
public class Stat
{
    [SerializeField] private float _baseValue;

    private float _flatBonus = 0f;
    private float _multiplierBonus  = 0f;   // sum of additive % deltas (0.5 = +50%)

    public Stat(float baseValue) => _baseValue = baseValue;

    /// <summary>Final computed value: (base + flat) × (1 + multipliers).</summary>
    public float Value => (_baseValue + _flatBonus) * (1f + _multiplierBonus);

    public void AddFlat(float delta) => _flatBonus += delta;
    public void AddMultiplier(float delta) => _multiplierBonus += delta;

    /// <summary>Clamp helpers, call after all modifications if needed.</summary>
    public float ValueClamped(float min, float max) => Mathf.Clamp(Value, min, max);

    public (float baseValue, float flatBonus, float multBonus) GetLayerTuple() =>
        (_baseValue, _flatBonus, _multiplierBonus);

    public void SetLayerTuple(float baseValue, float flatBonus, float multBonus)
    {
        _baseValue = baseValue;
        _flatBonus = flatBonus;
        _multiplierBonus = multBonus;
    }
}

// PlayerStats, owns all stats, exposes typed accessors

/// <summary>
/// Central stat store for the player.
/// All values are computed via Stat instances (base + flat + multiplier layers),
/// so upgrades compose correctly regardless of application order.
///
/// UpgradeEffects call AddFlat / AddMultiplier.
/// Game systems read the typed properties (Ex: Stats.AttackDamage).
/// </summary>
public class PlayerStats : MonoBehaviour
{
    public delegate void StatChangedHandler(PlayerStatType statType, float oldValue, float newValue);
    public event StatChangedHandler StatChanged;

    // Serialised base values, set these the inspector

    [Header("Movement")]
    [SerializeField] private float baseMoveSpeed   = 3f;

    [Header("Dash")]
    [SerializeField] private float baseDashSpeed   = 10f;
    [SerializeField] private int   baseDashCount   = 1;
    [SerializeField] private float baseDashCooldown = 1f;
    [SerializeField] private float baseDashDistance = 3f;

    [Header("Combat")]
    [SerializeField] private float baseAttackDamage  = 10f;
    [SerializeField] private float baseAttackSpeed   = 1f;
    [SerializeField] private float baseAttackRange   = 1.5f;
    [SerializeField] private float baseCritChance    = 0f;
    [SerializeField] private float baseCritMultiplier = 2f;

    [Header("Defence")]
    [SerializeField] private float baseMaxHealth  = 100f;
    [SerializeField] private float baseHealthRegen = 0.2f;
    [SerializeField] private float baseDamageReduction    = 0f;
    [SerializeField] private float baseDodgeChance = 0f;

    [Header("Economy")]
    [SerializeField] private float baseXPMultiplier   = 1f;
    
    // Runtime stat instances

    private Dictionary<PlayerStatType, Stat> _stats;

    private void Awake() => InitStats();

    /// <summary>Other components (e.g. <see cref="PlayerHudUI"/>) can read stats in Awake before this Awake runs — ensure the table exists.</summary>
    private void EnsureStatsInitialized()
    {
        if (_stats == null)
            InitStats();
    }

    /// <summary>Used when copying stats across scenes (e.g. gameplay → boss).</summary>
    public void EnsureStatsInitializedPublic() => EnsureStatsInitialized();

    private Stat GetStat(PlayerStatType type)
    {
        EnsureStatsInitialized();
        return _stats[type];
    }

    private void InitStats()
    {
        _stats = new Dictionary<PlayerStatType, Stat>
        {
            [PlayerStatType.MoveSpeed]      = new(baseMoveSpeed),
            [PlayerStatType.DashSpeed]      = new(baseDashSpeed),
            [PlayerStatType.DashCount]      = new(baseDashCount),
            [PlayerStatType.DashCooldown]   = new(baseDashCooldown),
            [PlayerStatType.DashDistance]   = new(baseDashDistance),
            [PlayerStatType.AttackDamage]   = new(baseAttackDamage),
            [PlayerStatType.AttackSpeed]    = new(baseAttackSpeed),
            [PlayerStatType.AttackRange]    = new(baseAttackRange),
            [PlayerStatType.CritChance]     = new(baseCritChance),
            [PlayerStatType.CritMultiplier] = new(baseCritMultiplier),
            [PlayerStatType.MaxHealth]      = new(baseMaxHealth),
            [PlayerStatType.HealthRegen]    = new(baseHealthRegen),
            [PlayerStatType.DamageReduction] = new(baseDamageReduction),
            [PlayerStatType.DodgeChance]    = new(baseDodgeChance),
            [PlayerStatType.XPMultiplier]   = new(baseXPMultiplier)
        };
    }
    
    // Modification API (called by UpgradeEffect subclasses)

    public void AddFlat(PlayerStatType type, float delta)
    {
        EnsureStatsInitialized();
        if (_stats.TryGetValue(type, out var stat))
        {
            float before = stat.Value;
            stat.AddFlat(delta);
            StatChanged?.Invoke(type, before, stat.Value);
        }
        else
            Debug.LogWarning($"[PlayerStats] Unknown stat type: {type}");
    }

    public void AddMultiplier(PlayerStatType type, float delta)
    {
        EnsureStatsInitialized();
        if (_stats.TryGetValue(type, out var stat))
        {
            float before = stat.Value;
            stat.AddMultiplier(delta);
            StatChanged?.Invoke(type, before, stat.Value);
        }
        else
            Debug.LogWarning($"[PlayerStats] Unknown stat type: {type}");
    }

    public float Get(PlayerStatType type)
    {
        EnsureStatsInitialized();
        return _stats.TryGetValue(type, out var s) ? s.Value : 0f;
    }

    /// <summary>Full per-stat layers for cross-scene continuity (base + flat + additive mult chain).</summary>
    public Dictionary<PlayerStatType, (float baseValue, float flatBonus, float multBonus)> ExportStatLayerDictionary()
    {
        EnsureStatsInitialized();
        var d = new Dictionary<PlayerStatType, (float, float, float)>();
        foreach (PlayerStatType t in Enum.GetValues(typeof(PlayerStatType)))
        {
            if (!_stats.TryGetValue(t, out var st))
                continue;
            d[t] = st.GetLayerTuple();
        }

        return d;
    }

    /// <summary>Replaces stat layers from a prior capture (after upgrade reapply, to correct prefab base drift).</summary>
    public void ImportStatLayers(Dictionary<PlayerStatType, (float baseValue, float flatBonus, float multBonus)> layers)
    {
        EnsureStatsInitialized();
        foreach (var kvp in layers)
        {
            if (!_stats.TryGetValue(kvp.Key, out var st))
                continue;
            var L = kvp.Value;
            st.SetLayerTuple(L.baseValue, L.flatBonus, L.multBonus);
        }
    }
    
    // Typed property accessors - use these to improve code clarity elsewhere

    public float MoveSpeed      => GetStat(PlayerStatType.MoveSpeed).Value;
    public float DashSpeed      => GetStat(PlayerStatType.DashSpeed).Value;
    public int   DashCount      => Mathf.RoundToInt(GetStat(PlayerStatType.DashCount).Value);
    public float DashCooldown   => Mathf.Max(0.05f, GetStat(PlayerStatType.DashCooldown).Value);
    public float DashDistance   => GetStat(PlayerStatType.DashDistance).Value;
    public float AttackDamage   => GetStat(PlayerStatType.AttackDamage).Value;
    public float AttackSpeed    => GetStat(PlayerStatType.AttackSpeed).Value;
    public float AttackRange    => GetStat(PlayerStatType.AttackRange).Value;
    public float CritChance     => Mathf.Clamp01(GetStat(PlayerStatType.CritChance).Value);
    public float CritMultiplier => GetStat(PlayerStatType.CritMultiplier).Value;
    public float MaxHealth      => GetStat(PlayerStatType.MaxHealth).Value;
    public float HealthRegen    => Mathf.Max(0f, GetStat(PlayerStatType.HealthRegen).Value);
    public float DamageReduction          => GetStat(PlayerStatType.DamageReduction).Value;
    public float DodgeChance    => Mathf.Clamp01(GetStat(PlayerStatType.DodgeChance).Value);
    public float XPMultiplier   => GetStat(PlayerStatType.XPMultiplier).Value;

    /// <summary>Wipes all bonuses - call at run start.</summary>
    public void ResetToBase() => InitStats();
}

