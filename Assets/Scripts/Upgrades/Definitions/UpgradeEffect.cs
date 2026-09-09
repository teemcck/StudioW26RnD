using UnityEngine;

public class UpgradeContext
{
    public PlayerController Player { get; }
    public PlayerStats Stats { get; }
    public StatusEffectManager StatusEffects { get; }
    public PlayerUpgradeManager Runtime { get; }
    public EnemySpawnManager SpawnManager { get; }
    public GameRules GameRules { get; }
    public UpgradeManager UpgradeManager { get; }

    public UpgradeContext(
        PlayerController player,
        PlayerStats stats,
        StatusEffectManager statusEffects,
        PlayerUpgradeManager runtime,
        EnemySpawnManager spawnManager,
        GameRules gameRules,
        UpgradeManager upgradeManager)
    {
        Player = player;
        Stats = stats;
        StatusEffects = statusEffects;
        Runtime = runtime;
        SpawnManager = spawnManager;
        GameRules = gameRules;
        UpgradeManager = upgradeManager;
    }

    public static UpgradeContext FromScene(PlayerController player)
    {
        StatusEffectManager statusEffects = player.GetComponent<StatusEffectManager>() ?? player.gameObject.AddComponent<StatusEffectManager>();
        PlayerUpgradeManager runtime = player.GetComponent<PlayerUpgradeManager>() ?? player.gameObject.AddComponent<PlayerUpgradeManager>();
        return new UpgradeContext(
            player,
            player.GetComponent<PlayerStats>(),
            statusEffects,
            runtime,
            EnemySpawnManager.Instance,
            GameRules.Instance,
            UpgradeManager.Instance
        );
    }
}

[System.Serializable]
public abstract class UpgradeEffect
{
    [Tooltip("Readable note shown only in the Inspector. No gameplay effect.")]
    public string editorNote;

    public abstract void Apply(UpgradeContext ctx);
    public abstract void Remove(UpgradeContext ctx);

    public virtual bool NeedsTick => false;
    public virtual void Tick(UpgradeContext ctx, float deltaTime) { }

    public abstract string GetDescription();
}
