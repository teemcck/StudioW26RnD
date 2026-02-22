using UnityEngine;

// ENEMY EVENTS

/// <summary>
/// An enemy died (from any cause).
/// Maybe this should become "EnemyKilledByPlayer" in the future.
/// I am not sure if there will be a need for death by natural causes.
/// </summary>
public struct EnemyDiedEvent
{
    public string EnemyType;
    public Vector2 Position;
    public bool KilledByPlayer;      // false = fell into corruption, etc.
    public int TotalActiveEnemies;
}