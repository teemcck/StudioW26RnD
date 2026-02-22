using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central registry and runtime coordinator for the upgrade system.
///
/// Responsibilities:
///   1. Build lookup dictionaries at startup.
///   2. Apply upgrades to the player via UpgradeContext.
///   3. Drive per-frame Tick on any active ticking effects.
///   4. Serve randomized card selections to the upgrade screen UI.
///   5. Track per-upgrade stack counts for the current run.
/// </summary>
public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [Header("Registries — drag all SOs here")]
    [SerializeField] private List<UpgradeDisplaySO> allDisplays = new();
    [SerializeField] private List<UpgradeEffectSO>  allEffects  = new();
    
    // Lookup maps  (built once at Awake)

    private Dictionary<string, UpgradeEffectSO>  _effectMap  = new();
    private Dictionary<string, UpgradeDisplaySO> _displayMap = new();
    
    // Runtime run state

    /// <summary>How many times the player has acquired each upgrade this run.</summary>
    private Dictionary<string, int> _stacks = new();

    /// <summary>All effects that need a per-frame Tick, paired with their context.</summary>
    private List<(UpgradeEffect effect, UpgradeContext ctx)> _tickingEffects = new();

    // Cached context, rebuilt when the scene changes
    // Not sure if we will have scene changes, but this is here just in case.
    private UpgradeContext _cachedContext;
    private PlayerController _trackedPlayer;
    
    // Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildMaps();
    }

    private void Update()
    {
        if (_tickingEffects.Count == 0) return;
        float dt = Time.deltaTime;
        foreach (var (effect, ctx) in _tickingEffects)
            effect.Tick(ctx, dt);
    }

    private void BuildMaps()
    {
        _effectMap.Clear();
        _displayMap.Clear();

        foreach (var so in allEffects)
        {
            if (so == null) continue;
            if (!_effectMap.TryAdd(so.upgradeID, so))
                Debug.LogWarning($"[UpgradeManager] Duplicate effect ID: '{so.upgradeID}'");
        }

        foreach (var so in allDisplays)
        {
            if (so == null) continue;
            if (!_displayMap.TryAdd(so.upgradeID, so))
                Debug.LogWarning($"[UpgradeManager] Duplicate display ID: '{so.upgradeID}'");
        }

        Debug.Log($"[UpgradeManager] Registered {_effectMap.Count} effects / {_displayMap.Count} displays.");
    }
    
    // Public API, Lookup

    public bool TryGetEffect(string id, out UpgradeEffectSO so)  => _effectMap.TryGetValue(id, out so);
    public bool TryGetDisplay(string id, out UpgradeDisplaySO so) => _displayMap.TryGetValue(id, out so);
    public int  GetStack(string id) => _stacks.TryGetValue(id, out int s) ? s : 0;
    
    // Public API, Apply

    /// <summary>
    /// Apply an upgrade by ID. Call this when the player selects a card.
    /// </summary>
    public bool ApplyUpgrade(string id, PlayerController player)
    {
        if (!_effectMap.TryGetValue(id, out var effectSO))
        {
            Debug.LogError($"[UpgradeManager] No effect found for ID '{id}'.");
            return false;
        }

        int currentStack = GetStack(id);
        if (effectSO.maxStacks != -1 && currentStack >= effectSO.maxStacks)
        {
            Debug.LogWarning($"[UpgradeManager] '{id}' already at max stacks ({effectSO.maxStacks}).");
            return false;
        }

        _stacks[id] = currentStack + 1;

        var ctx = GetOrBuildContext(player);
        effectSO.Apply(ctx);

        // Register any new ticking effects
        foreach (var effect in effectSO.effects)
            if (effect != null && effect.NeedsTick)
                _tickingEffects.Add((effect, ctx));

        return true;
    }

    /// <summary>
    /// Revoke an upgrade (for debug / specific mechanics like curse removal).
    /// </summary>
    public void RevokeUpgrade(string id, PlayerController player)
    {
        if (!_effectMap.TryGetValue(id, out var effectSO)) return;
        if (GetStack(id) <= 0) return;

        _stacks[id]--;
        var ctx = GetOrBuildContext(player);
        effectSO.Remove(ctx);

        // Clean up ticking effects that belonged to this SO
        _tickingEffects.RemoveAll(pair =>
            effectSO.effects.Contains(pair.effect));
    }
    
    // Public API, Randomised upgrade selection for the upgrade screen

    /// <summary>
    /// Returns <paramref name="count"/> UpgradeDisplaySOs for the upgrade screen.
    /// Filters out fully-stacked upgrades, weights by rarity.
    /// </summary>
    public List<UpgradeDisplaySO> GetRandomUpgradeChoices(int count, bool rarityWeighted = true)
    {
        var pool = new List<(UpgradeDisplaySO display, float weight)>();

        foreach (var display in allDisplays)
        {
            if (!_effectMap.TryGetValue(display.upgradeID, out var effect)) continue;
            int stacks = GetStack(display.upgradeID);
            if (effect.maxStacks != -1 && stacks >= effect.maxStacks) continue;

            float w = rarityWeighted ? RarityWeight(display.rarity) : 1f;
            pool.Add((display, w));
        }

        var result = new List<UpgradeDisplaySO>();
        int picks = Mathf.Min(count, pool.Count);

        for (int i = 0; i < picks; i++)
        {
            int idx = WeightedRandom(pool);
            result.Add(pool[idx].display);
            pool.RemoveAt(idx);
        }

        return result;
    }
    
    // Run lifecycle

    /// <summary>Call at the start of a new run to wipe all state.</summary>
    public void ResetRun(PlayerController player)
    {
        // Revoke every active upgrade cleanly so Remove() fires on all effects
        var ctx = GetOrBuildContext(player);
        foreach (var kvp in _stacks)
        {
            if (!_effectMap.TryGetValue(kvp.Key, out var so)) continue;
            for (int i = 0; i < kvp.Value; i++)
                so.Remove(ctx);
        }

        _stacks.Clear();
        _tickingEffects.Clear();
        _cachedContext = null;
    }
    
    // Helpers

    private UpgradeContext GetOrBuildContext(PlayerController player)
    {
        // Rebuild if the player reference changed (e.g. after a scene load)
        if (_cachedContext == null || _trackedPlayer != player)
        {
            _cachedContext = UpgradeContext.FromScene(player);
            _trackedPlayer = player;
        }
        return _cachedContext;
    }

    private static float RarityWeight(UpgradeRarity r) => r switch
    {
        UpgradeRarity.Common    => 60f,
        UpgradeRarity.Uncommon  => 25f,
        UpgradeRarity.Rare      => 12f,
        UpgradeRarity.Legendary =>  3f,
        _                       => 60f
    };

    private static int WeightedRandom(List<(UpgradeDisplaySO d, float w)> pool)
    {
        float total = 0f;
        foreach (var (_, w) in pool) total += w;
        float roll = Random.Range(0f, total);
        float cum  = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            cum += pool[i].w;
            if (roll <= cum) return i;
        }
        return pool.Count - 1;
    }
}
