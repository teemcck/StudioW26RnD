using System;
using System.Collections.Generic;
using UnityEngine;

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
    MaxHealth, Armor, DodgeChance,
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
        PlayerStatType.Armor          => "Armor",
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
    // Serialised base values, set these the inspector

    [Header("Movement")]
    [SerializeField] private float baseMoveSpeed   = 5f;

    [Header("Dash")]
    [SerializeField] private float baseDashSpeed   = 15f;
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
    [SerializeField] private float baseArmor      = 0f;
    [SerializeField] private float baseDodgeChance = 0f;

    [Header("Economy")]
    [SerializeField] private float baseGoldMultiplier = 1f;
    [SerializeField] private float baseXPMultiplier   = 1f;
    
    // Runtime stat instances

    private Dictionary<PlayerStatType, Stat> _stats;

    private void Awake() => InitStats();

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
            [PlayerStatType.Armor]          = new(baseArmor),
            [PlayerStatType.DodgeChance]    = new(baseDodgeChance),
            [PlayerStatType.XPMultiplier]   = new(baseXPMultiplier)
        };
    }
    
    // Modification API (called by UpgradeEffect subclasses)

    public void AddFlat(PlayerStatType type, float delta)
    {
        if (_stats.TryGetValue(type, out var stat))
            stat.AddFlat(delta);
        else
            Debug.LogWarning($"[PlayerStats] Unknown stat type: {type}");
    }

    public void AddMultiplier(PlayerStatType type, float delta)
    {
        if (_stats.TryGetValue(type, out var stat))
            stat.AddMultiplier(delta);
        else
            Debug.LogWarning($"[PlayerStats] Unknown stat type: {type}");
    }

    public float Get(PlayerStatType type)
        => _stats.TryGetValue(type, out var s) ? s.Value : 0f;
    
    // Typed property accessors - use these to improve code clarity elsewhere

    public float MoveSpeed      => _stats[PlayerStatType.MoveSpeed].Value;
    public float DashSpeed      => _stats[PlayerStatType.DashSpeed].Value;
    public int   DashCount      => Mathf.RoundToInt(_stats[PlayerStatType.DashCount].Value);
    public float DashCooldown   => Mathf.Max(0.05f, _stats[PlayerStatType.DashCooldown].Value);
    public float DashDistance   => _stats[PlayerStatType.DashDistance].Value;
    public float AttackDamage   => _stats[PlayerStatType.AttackDamage].Value;
    public float AttackSpeed    => _stats[PlayerStatType.AttackSpeed].Value;
    public float AttackRange    => _stats[PlayerStatType.AttackRange].Value;
    public float CritChance     => Mathf.Clamp01(_stats[PlayerStatType.CritChance].Value);
    public float CritMultiplier => _stats[PlayerStatType.CritMultiplier].Value;
    public float MaxHealth      => _stats[PlayerStatType.MaxHealth].Value;
    public float Armor          => _stats[PlayerStatType.Armor].Value;
    public float DodgeChance    => Mathf.Clamp01(_stats[PlayerStatType.DodgeChance].Value);
    public float XPMultiplier   => _stats[PlayerStatType.XPMultiplier].Value;

    /// <summary>Wipes all bonuses - call at run start.</summary>
    public void ResetToBase() => InitStats();
}
