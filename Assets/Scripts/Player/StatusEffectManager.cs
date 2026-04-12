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
    
    private void Awake()
    {
        _playerStats = GetComponent<PlayerStats>();
    }
    
    /// <summary>
    /// Apply a new status effect. If it already exists, add a stack (up to maxStacks).
    /// </summary>
    public void Apply(string effectId, float duration, int maxStacks = 1)
    {
        if (_activeEffects.TryGetValue(effectId, out var existing))
        {
            existing.AddStack();
        }
        else
        {
            var effect = new StatusEffect(effectId, duration, maxStacks);
            _activeEffects[effectId] = effect;
        }
    }
    
    /// <summary>
    /// Remove a status effect immediately.
    /// </summary>
    public void Remove(string effectId)
    {
        _activeEffects.Remove(effectId);
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
    public void ApplyStatModifier(string effectId, PlayerStatType stat, float flatDelta = 0f, float multiplierDelta = 0f)
    {
        if (_activeEffects.TryGetValue(effectId, out var effect))
        {
            if (flatDelta != 0f) _playerStats.AddFlat(stat, flatDelta);
            if (multiplierDelta != 0f) _playerStats.AddMultiplier(stat, multiplierDelta);
        }
    }
    
    /// <summary>
    /// Remove a stat modifier (inverse of Apply).
    /// </summary>
    public void RemoveStatModifier(string effectId, PlayerStatType stat, float flatDelta = 0f, float multiplierDelta = 0f)
    {
        if (flatDelta != 0f) _playerStats.AddFlat(stat, -flatDelta);
        if (multiplierDelta != 0f) _playerStats.AddMultiplier(stat, -multiplierDelta);
    }
    
    private void Update()
    {
        var expiredEffects = new List<string>();
        
        foreach (var kvp in _activeEffects)
        {
            kvp.Value.timeRemaining -= Time.deltaTime;
            if (kvp.Value.IsExpired)
                expiredEffects.Add(kvp.Key);
        }
        
        foreach (var id in expiredEffects)
            _activeEffects.Remove(id);
    }
}
