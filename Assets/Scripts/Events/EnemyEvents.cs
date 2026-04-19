using UnityEngine;

// ENEMY EVENTS

/// <summary>An enemy took damage.</summary>
public struct EnemyDamagedEvent
{
    public EnemyBase Enemy;
    public float DamageDealt;
    public float RemainingHealth;
    public Vector2 Position;
    public DamageContext Context;
}

/// <summary>An enemy has died.</summary>
public struct EnemyKilledEvent
{
    public EnemyBase Enemy;
    public string EnemyType;
    public Vector2 Position;
    public DamageContext Context;
}
