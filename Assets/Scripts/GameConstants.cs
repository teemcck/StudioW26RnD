/// <summary>
/// Single source of truth for numeric ranges shared across floor generation and balance.
/// </summary>
public static class GameConstants
{
    /// <summary>Lowest rolled difficulty (inclusive). Used for preview, XP, and enemy fill mapping.</summary>
    public const int MinDifficulty = 1;

    /// <summary>Highest rolled difficulty (inclusive).</summary>
    public const int MaxDifficulty = 5;

    /// <summary>Minimum number of map chunks (islands) per floor.</summary>
    public const int MinChunkCount = 2;

    /// <summary>Maximum number of map chunks per floor.</summary>
    public const int MaxChunkCount = 4;

    /// <summary>Enemy spawn density at MinDifficulty: percent of spawn-layer tiles that get an enemy.</summary>
    public const float MinEnemyFillPercent = 1f;

    /// <summary>Enemy spawn density at MaxDifficulty.</summary>
    public const float MaxEnemyFillPercent = 2f;
}

public enum WorldBand
{
    WorldOne,
    WorldTwo
}

public static class WorldProgression
{
    public const int WorldOneFloorCount = 5;
    public const int WorldTwoFloorCount = 5;
    public const int WorldTwoStartFloorIndex = WorldOneFloorCount;
    public const int BossFloorIndex = WorldOneFloorCount + WorldTwoFloorCount;

    public static WorldBand GetBandForFloor(int floorIndex)
    {
        return floorIndex < WorldTwoStartFloorIndex
            ? WorldBand.WorldOne
            : WorldBand.WorldTwo;
    }

    public static bool IsBossFloor(int floorIndex) => floorIndex >= BossFloorIndex;

    public static bool IsWorldTwoTransition(int floorIndex) => floorIndex == WorldTwoStartFloorIndex;
}
