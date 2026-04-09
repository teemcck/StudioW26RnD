/// <summary>
/// Single source of truth for numeric ranges shared across level generation and balance.
/// </summary>
public static class GameConstants
{
    /// <summary>Lowest rolled difficulty (inclusive). Used for preview, XP, and enemy fill mapping.</summary>
    public const int MinDifficulty = 1;

    /// <summary>Highest rolled difficulty (inclusive).</summary>
    public const int MaxDifficulty = 5;

    /// <summary>Minimum number of map chunks (islands) per level.</summary>
    public const int MinChunkCount = 3;

    /// <summary>Maximum number of map chunks per level.</summary>
    public const int MaxChunkCount = 7;

    /// <summary>Enemy spawn density at MinDifficulty: percent of spawn-layer tiles that get an enemy.</summary>
    public const float MinEnemyFillPercent = 1f;

    /// <summary>Enemy spawn density at MaxDifficulty.</summary>
    public const float MaxEnemyFillPercent = 3f;
}
