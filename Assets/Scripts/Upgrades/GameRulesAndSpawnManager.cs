using System.Collections.Generic;
using UnityEngine;

// GAME RULES

public enum GameRuleType
{
    XPDropRate,
    RoomCount,
    EliteSpawnChance,
    EliteHealthMultiplier
    // More things can be added here as we figure out the game better.
}

public static class GameRuleTypeExtensions
{
    public static string ToDisplayString(this GameRuleType t) => t switch
    {
        GameRuleType.XPDropRate           => "XP Drop Rate",
        GameRuleType.RoomCount            => "Room Count",
        GameRuleType.EliteSpawnChance     => "Elite Spawn Chance",
        GameRuleType.EliteHealthMultiplier => "Elite HP Multiplier",
        _                                 => t.ToString()
    };
}

/// <summary>
/// Holds global rule values for the current run.
/// Upgrade effects modify these; game systems read them.
/// Same flat+multiplier model as PlayerStats.
/// </summary>
public class GameRules : MonoBehaviour
{
    public static GameRules Instance { get; private set; }

    [Header("Base Rule Values")]
    [SerializeField] private float baseGoldDropRate         = 1f;
    [SerializeField] private float baseXPDropRate           = 1f;
    [SerializeField] private int   baseRoomCount            = 10;
    [SerializeField] private float baseEliteSpawnChance     = 0.1f;
    [SerializeField] private float baseEliteHealthMultiplier = 1f;

    private Dictionary<GameRuleType, Stat> _rules;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        InitRules();
    }

    private void InitRules()
    {
        _rules = new Dictionary<GameRuleType, Stat>
        {
            [GameRuleType.XPDropRate] = new(baseXPDropRate),
            [GameRuleType.RoomCount] = new(baseRoomCount),
            [GameRuleType.EliteSpawnChance] = new(baseEliteSpawnChance),
            [GameRuleType.EliteHealthMultiplier] = new(baseEliteHealthMultiplier)
        };
    }

    public void AddFlat(GameRuleType type, float delta) => _rules[type].AddFlat(delta);
    public void AddMultiplier(GameRuleType type, float delta) => _rules[type].AddMultiplier(delta);
    public float Get(GameRuleType type) => _rules[type].Value;

    public void ResetToBase() => InitRules();
}

// ENEMY SPAWN MANAGER

/// <summary>
/// Manages the enemy spawn pool and global spawn scale for the current floor/room.
/// Upgrade effects call the public API here; the spawn logic reads from it.
///
/// This will be fleshed out more later when working on actual enemy spawning.
/// </summary>
public class EnemySpawnManager : MonoBehaviour
{
    public static EnemySpawnManager Instance { get; private set; }

    /// <summary>
    /// Cumulative product of all spawn multipliers.
    /// 1.0 = baseline. 1.5 = 50% more enemies. 2.0 = double.
    /// </summary>
    public float SpawnScale { get; private set; } = 1f;

    private HashSet<string> _activeEnemyTypes = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>Additively stacks a new spawn scale modifier.</summary>
    public void AddSpawnMultiplier(float multiplier)
    {
        SpawnScale = Mathf.Max(0f, SpawnScale + multiplier);
        Debug.Log($"[SpawnManager] New spawn scale: {SpawnScale:F2}×");
    }
    
    // This might not be needed (?) would be cool if some upgrades added enemy types.
    // Ex: curse upgrade that adds extra low health spiders (or something) to farm kills. 
    
    public void AddEnemyType(string tag)
    {
        _activeEnemyTypes.Add(tag);
        Debug.Log($"[SpawnManager] Added enemy type: {tag}");
    }

    public void RemoveEnemyType(string tag)
    {
        _activeEnemyTypes.Remove(tag);
        Debug.Log($"[SpawnManager] Removed enemy type: {tag}");
    }

    public bool IsTypeActive(string tag) => _activeEnemyTypes.Contains(tag);

    public IReadOnlyCollection<string> ActiveTypes => _activeEnemyTypes;

    public void ResetToBase()
    {
        SpawnScale = 1f;
        _activeEnemyTypes.Clear();
    }
}
