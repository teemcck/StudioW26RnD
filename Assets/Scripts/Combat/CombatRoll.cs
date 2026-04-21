using UnityEngine;

public static class CombatRoll
{
    public static bool TryRollCrit(PlayerStats stats, out float critMultiplier)
    {
        if (stats == null) { critMultiplier = 1f; return false; }

        float chance = Mathf.Clamp01(stats.CritChance);
        if (chance <= 0f) { critMultiplier = 1f; return false; }

        if (Random.value <= chance)
        {
            critMultiplier = Mathf.Max(1f, stats.CritMultiplier);
            return true;
        }

        critMultiplier = 1f;
        return false;
    }
}
