public static class StatusEffectIds
{
    public const string Poison = "poison";
    public const string Confusion = "confusion";
    public const string Swiftness = "swiftness";
    public const string Frailty = "frailty";

    public static bool IsNegative(string effectId)
    {
        return effectId == Poison || effectId == Confusion || effectId == Frailty;
    }
}

