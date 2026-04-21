using UnityEngine;

public static class FloorScalingCurve
{
    public const float HealthPerFloor = 0.26f;
    public const float DamagePerFloor = 0.10f;
    public const float WorldTwoHealthBonus = 0.42f;
    public const float WorldTwoDamageBonus = 0.20f;

    private static FloorScalingSettings _cachedSettings;
    private static bool _settingsLoadAttempted;

    public static float GetHealthMult(int floorIndex)
    {
        if (TryGetSettings(out var s))
            return Mathf.Max(0.01f, s.EvaluateHealth(floorIndex));

        float m = 1f + Mathf.Max(0, floorIndex) * HealthPerFloor;
        if (WorldProgression.GetBandForFloor(floorIndex) == WorldBand.WorldTwo)
            m += WorldTwoHealthBonus;
        return m;
    }

    public static float GetDamageMult(int floorIndex)
    {
        if (TryGetSettings(out var s))
            return Mathf.Max(0.01f, s.EvaluateDamage(floorIndex));

        float m = 1f + Mathf.Max(0, floorIndex) * DamagePerFloor;
        if (WorldProgression.GetBandForFloor(floorIndex) == WorldBand.WorldTwo)
            m += WorldTwoDamageBonus;
        return m;
    }

    private static bool TryGetSettings(out FloorScalingSettings settings)
    {
        if (_cachedSettings != null) { settings = _cachedSettings; return true; }
        if (_settingsLoadAttempted) { settings = null; return false; }

        _settingsLoadAttempted = true;
        _cachedSettings = Resources.Load<FloorScalingSettings>("FloorScaling");
        settings = _cachedSettings;
        return settings != null;
    }

    public static void ResetCache()
    {
        _cachedSettings = null;
        _settingsLoadAttempted = false;
    }
}
