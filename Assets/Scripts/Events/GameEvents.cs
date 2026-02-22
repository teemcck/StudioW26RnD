// GAME EVENTS
// High-level run and session lifecycle events.

/// <summary>Player completed a level and is moving to the next.</summary>
public struct LevelCompletedEvent
{
    public int LevelLength; // Based on randomized number of chunks
    public int LevelDifficulty; // Based on the rolled difficulty of enemies.
    public float CompletionTimeSeconds;
}

/// <summary>
/// All enemies in the current room have been cleared.
/// This could be used for certain upgrades. (Ex: player gains extra speed after full room clear)
/// </summary>
public struct RoomClearedEvent
{
    public string ZoneID;
    public float  ClearTimeSeconds;
    public int    EnemiesKilled;
}

/// <summary>A new level has been loaded and is ready to play.</summary>
public struct LevelLoadedEvent
{
    public int LevelIndex;
    public bool IsFirstLevel;
}

/// <summary>Player opened the upgrade selection screen.</summary>
public struct UpgradeScreenOpenedEvent
{
    public int OfferedCount; // For if more than 3 cards offered later.
}

/// <summary>Player selected an upgrade from the screen.</summary>
public struct UpgradeSelectedEvent
{
    public string UpgradeID;
    public string UpgradeName;
    public int NewStackCount;
}

/// <summary>
/// The game was paused or unpaused.
/// </summary>
public struct GamePausedEvent
{
    public bool IsPaused;
    public string Reason;              // "menu", "corruption_warning", "cutscene"
}