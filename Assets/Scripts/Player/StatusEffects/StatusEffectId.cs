/// <summary>
/// Every status effect the game knows about. Using an enum (not strings) means an invalid effect cannot
/// be constructed and switch statements can be checked for completeness.
/// </summary>
public enum StatusEffectId
{
    Poison,
    Confusion,
    Swiftness,
    Frailty,
}

public static class StatusEffectIdExtensions
{
    public static bool IsNegative(this StatusEffectId effectId)
    {
        return effectId == StatusEffectId.Poison
            || effectId == StatusEffectId.Confusion
            || effectId == StatusEffectId.Frailty;
    }

    /// <summary>Lowercase key used for tooltip catalog lookups and damage-source ids.</summary>
    public static string ToKey(this StatusEffectId effectId)
    {
        return effectId switch
        {
            StatusEffectId.Poison => "poison",
            StatusEffectId.Confusion => "confusion",
            StatusEffectId.Swiftness => "swiftness",
            StatusEffectId.Frailty => "frailty",
            _ => effectId.ToString().ToLowerInvariant(),
        };
    }
}
