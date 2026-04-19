// GAME EVENTS
// High-level run and session lifecycle events.

/// <summary>Player completed a floor and is moving to the next.</summary>
public struct FloorCompletedEvent
{
    public int FloorLength; // Based on randomized number of chunks
    public int FloorDifficulty; // Based on the rolled difficulty of enemies.
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

/// <summary>A new floor has been loaded and is ready to play.</summary>
public struct FloorLoadedEvent
{
    public int FloorIndex;
    public bool IsFirstFloor;
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
