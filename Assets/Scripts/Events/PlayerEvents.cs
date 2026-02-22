using UnityEngine;

// PLAYER EVENTS

/// <summary>Player's HP dropped to zero. Triggers game over.</summary>
public struct PlayerDiedEvent
{
    public Vector2 Position;
    public float SurvivedForSeconds;
}

/// <summary>Player took damage from any source.</summary>
public struct PlayerDamagedEvent
{
    public float Amount;
    public float RemainingHP;
    public Vector2 HitPosition;
    public string Source;              // "enemy", "corruption", "hazard" etc.
}

public struct PlayerDashedEvent
{
    public Vector2 Position;
}

/// <summary>Player was healed (upgrade on-kill heal, pickup, etc.).</summary>
public struct PlayerHealedEvent
{
    public float Amount;
    public float NewHP;
}

/// <summary>Player killed an enemy.</summary>
public struct PlayerKilledEnemyEvent
{
    public Vector2 EnemyPosition;
    public string EnemyType;
    public int TotalKillsThisRun;
}