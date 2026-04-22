using System.Collections.Generic;
using UnityEngine;

public enum GameRuleType
{
    XPDropRate,
    RoomCount,
    EliteSpawnChance,
    EliteHealthMultiplier
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

public class GameRules : MonoBehaviour
{
    public static GameRules Instance { get; private set; }

    [Header("Base Rule Values")]
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

public class EnemySpawnManager : MonoBehaviour
{
    public static EnemySpawnManager Instance { get; private set; }

    public float SpawnScale { get; private set; } = 1f;

    private HashSet<string> _activeEnemyTypes = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void AddSpawnMultiplier(float multiplier)
    {
        SpawnScale = Mathf.Max(0f, SpawnScale + multiplier);
    }

    public void AddEnemyType(string tag)
    {
        _activeEnemyTypes.Add(tag);
    }

    public void RemoveEnemyType(string tag)
    {
        _activeEnemyTypes.Remove(tag);
    }

    public bool IsTypeActive(string tag) => _activeEnemyTypes.Contains(tag);

    public IReadOnlyCollection<string> ActiveTypes => _activeEnemyTypes;

    public void ResetToBase()
    {
        SpawnScale = 1f;
        _activeEnemyTypes.Clear();
    }
}
