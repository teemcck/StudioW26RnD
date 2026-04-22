using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Captures combat-relevant player state before <c>GameplayLoop</c> → <c>BossGameplay</c>
/// so the boss scene player matches the run (stats, HP ratio, XP HUD divisor) even if the boss
/// prefab uses different serialized <see cref="PlayerStats"/> bases.
/// </summary>
public static class PlayerCombatTransitionState
{
    public static bool HasPendingRestore { get; private set; }

    private static Dictionary<PlayerStatType, (float baseValue, float flatBonus, float multBonus)> _layers;
    private static float _healthNormalized = 1f;
    private static int _xpPerFloorForHud = -1;

    /// <summary>Call immediately before loading the boss scene from <see cref="GameplayHandler"/>.</summary>
    public static void Capture(PlayerStats stats, PlayerHealth health, int xpPerFloor)
    {
        if (stats == null || health == null)
            return;

        stats.EnsureStatsInitializedPublic();
        _layers = stats.ExportStatLayerDictionary();
        float max = Mathf.Max(0.0001f, stats.MaxHealth);
        _healthNormalized = Mathf.Clamp01(health.CurrentHealth / max);
        _xpPerFloorForHud = Mathf.Max(1, xpPerFloor);
        HasPendingRestore = true;
    }

    /// <summary>
    /// Applies captured layers and HP after <see cref="UpgradeManager"/> has reapplied upgrade stacks
    /// (so runtime modifiers stay in sync with stacks, while final numbers match the pre-boss run).
    /// </summary>
    public static bool TryConsumeAfterUpgradeReapply(PlayerStats stats, PlayerHealth health)
    {
        if (!HasPendingRestore || _layers == null || stats == null || health == null)
            return false;

        stats.EnsureStatsInitializedPublic();
        stats.ImportStatLayers(_layers);

        float max = Mathf.Max(0.0001f, stats.MaxHealth);
        health.ApplyBossTransitionPreserve(Mathf.Clamp(_healthNormalized * max, 0f, max));

        if (RunStatsTracker.Instance != null && _xpPerFloorForHud > 0)
            RunStatsTracker.Instance.SetXpPerFloorHudOverride(_xpPerFloorForHud);

        Clear();
        return true;
    }

    public static void Clear()
    {
        HasPendingRestore = false;
        _layers = null;
        _healthNormalized = 1f;
        _xpPerFloorForHud = -1;
    }
}
