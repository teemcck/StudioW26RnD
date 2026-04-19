using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages temporary status effects on the player.
/// Handles application, removal, ticking, and stat interactions.
/// </summary>
public class StatusEffectManager : MonoBehaviour
{
    private Dictionary<string, StatusEffect> _activeEffects = new();
    private PlayerStats _playerStats;
    private PlayerHealth _playerHealth;
    private PlayerUpgradeRuntime _upgradeRuntime;
    private float _poisonTickTimer;
    private int _appliedSwiftnessStacks;
    private int _appliedConfusionStacks;
    
    private void Awake()
    {
        _playerStats = GetComponent<PlayerStats>();
        _playerHealth = GetComponent<PlayerHealth>();
        _upgradeRuntime = GetComponent<PlayerUpgradeRuntime>();
    }
    
    /// <summary>
    /// Apply a new status effect. If it already exists, add a stack (up to maxStacks).
    /// </summary>
    public void Apply(string effectId, float duration, int maxStacks = 1, bool isPermanent = false)
    {
        if (_upgradeRuntime != null &&
            _upgradeRuntime.IsImmuneToNegativeStatuses &&
            StatusEffectIds.IsNegative(effectId))
        {
            return;
        }

        if (_activeEffects.TryGetValue(effectId, out var existing))
        {
            existing.maxStacks = Mathf.Max(existing.maxStacks, maxStacks);
            existing.isPermanent |= isPermanent;
            existing.duration = isPermanent ? 0f : Mathf.Max(existing.duration, duration);
            existing.AddStack();
        }
        else
        {
            var effect = new StatusEffect(effectId, duration, maxStacks, isPermanent);
            _activeEffects[effectId] = effect;
        }

        RefreshStatBackedEffects();
    }
    
    /// <summary>
    /// Remove a status effect immediately.
    /// </summary>
    public void Remove(string effectId)
    {
        if (_activeEffects.Remove(effectId))
            RefreshStatBackedEffects();
    }

    public void SetPermanentStacks(string effectId, int stacks)
    {
        if (stacks <= 0)
        {
            Remove(effectId);
            return;
        }

        if (_activeEffects.TryGetValue(effectId, out var existing))
        {
            existing.maxStacks = Mathf.Max(existing.maxStacks, stacks);
            existing.currentStacks = stacks;
            existing.isPermanent = true;
            existing.duration = 0f;
            existing.timeRemaining = float.PositiveInfinity;
        }
        else
        {
            var effect = new StatusEffect(effectId, 0f, stacks, isPermanent: true)
            {
                currentStacks = stacks,
                timeRemaining = float.PositiveInfinity
            };
            _activeEffects[effectId] = effect;
        }

        RefreshStatBackedEffects();
    }
    
    /// <summary>
    /// Check if a status effect is currently active.
    /// </summary>
    public bool Has(string effectId)
    {
        return _activeEffects.ContainsKey(effectId);
    }
    
    /// <summary>
    /// Get the current stack count of an effect (returns 0 if not active).
    /// </summary>
    public int GetStackCount(string effectId)
    {
        return _activeEffects.TryGetValue(effectId, out var effect) ? effect.currentStacks : 0;
    }
    
    /// <summary>
    /// Apply a temporary stat modifier while an effect is active.
    /// This is typically called from within upgrade effects.
    /// </summary>
    private void Update()
    {
        TickPoison();
        var expiredEffects = new List<string>();
        
        foreach (var kvp in _activeEffects)
        {
            if (kvp.Value.isPermanent)
                continue;

            kvp.Value.timeRemaining -= Time.deltaTime;
            if (kvp.Value.IsExpired)
                expiredEffects.Add(kvp.Key);
        }
        
        foreach (var id in expiredEffects)
            _activeEffects.Remove(id);

        if (expiredEffects.Count > 0)
            RefreshStatBackedEffects();
    }

    private void TickPoison()
    {
        int poisonStacks = GetStackCount(StatusEffectIds.Poison);
        if (poisonStacks <= 0 || _playerHealth == null)
        {
            _poisonTickTimer = 0f;
            return;
        }

        _poisonTickTimer += Time.deltaTime;
        while (_poisonTickTimer >= 1f)
        {
            _poisonTickTimer -= 1f;
            _playerHealth.ApplyDirectDamage(poisonStacks * 2f, "status_poison");
        }
    }

    private void RefreshStatBackedEffects()
    {
        int swiftnessStacks = Mathf.Min(3, GetStackCount(StatusEffectIds.Swiftness));
        int confusionStacks = Mathf.Min(3, GetStackCount(StatusEffectIds.Confusion));

        int swiftnessDelta = swiftnessStacks - _appliedSwiftnessStacks;
        if (swiftnessDelta != 0)
        {
            float delta = swiftnessDelta * 0.15f;
            _playerStats.AddMultiplier(PlayerStatType.MoveSpeed, delta);
            _playerStats.AddMultiplier(PlayerStatType.AttackSpeed, delta);
            _appliedSwiftnessStacks = swiftnessStacks;
        }

        int confusionDelta = confusionStacks - _appliedConfusionStacks;
        if (confusionDelta != 0)
        {
            float delta = confusionDelta * -0.15f;
            _playerStats.AddMultiplier(PlayerStatType.MoveSpeed, delta);
            _playerStats.AddMultiplier(PlayerStatType.AttackSpeed, delta);
            _appliedConfusionStacks = confusionStacks;
        }
    }
}
