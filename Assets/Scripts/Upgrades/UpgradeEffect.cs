using UnityEngine;

/// <summary>
/// Passed to every effect when it is applied or removed.
/// Acts as a service locator scoped to a single upgrade event.
/// Add more references here as the game grows, effects only take what they need.
/// </summary>
public class UpgradeContext
{
    public PlayerController Player      { get; }
    public PlayerStats      Stats       { get; }
    public EnemySpawnManager SpawnManager { get; }
    public GameRules        GameRules   { get; }
    // Add: LootManager, ProjectileSystem, AbilitySystem, etc. over time

    public UpgradeContext(
        PlayerController player,
        PlayerStats stats,
        EnemySpawnManager spawnManager,
        GameRules gameRules)
    {
        Player       = player;
        Stats        = stats;
        SpawnManager = spawnManager;
        GameRules    = gameRules;
    }

    /// <summary>
    /// Convenience factory, pulls live instances from the scene.
    /// Call this once when the upgrade screen opens rather than per-apply.
    /// </summary>
    public static UpgradeContext FromScene(PlayerController player)
    {
        return new UpgradeContext(
            player,
            player.GetComponent<PlayerStats>(),
            EnemySpawnManager.Instance,
            GameRules.Instance
        );
    }
}

/// <summary>
/// Base class for every discrete effect an upgrade can have.
/// Subclass this, never instantiate directly.
///
/// Subclasses live in their own files and are referenced by UpgradeEffectSO
/// via a polymorphic [SerializeReference] list, so they appear as a dropdown
/// in the Unity Inspector with full custom fields per effect type.
///
/// Apply / Remove must be pure (no side effects outside the context) 
/// This makes testing and run-resets safe.
/// </summary>
[System.Serializable]
public abstract class UpgradeEffect
{
    [Tooltip("Readable note shown only in the Inspector. No gameplay effect.")]
    public string editorNote;

    /// <summary>Apply this effect once when the player picks the upgrade.</summary>
    public abstract void Apply(UpgradeContext ctx);

    /// <summary>Undo this effect (e.g. run reset, debug revoke).</summary>
    public abstract void Remove(UpgradeContext ctx);

    /// <summary>
    /// Optional: called every frame if the effect needs continuous evaluation
    /// (e.g. a damage-over-time aura). Return false if unused, UpgradeManager
    /// will skip the Tick loop for this effect.
    /// </summary>
    public virtual bool NeedsTick => false;
    public virtual void Tick(UpgradeContext ctx, float deltaTime) { }

    /// <summary>
    /// Human-readable summary of what this effect does,
    /// used to auto-generate upgrade card descriptions if desired.
    /// </summary>
    public abstract string GetDescription();
}
