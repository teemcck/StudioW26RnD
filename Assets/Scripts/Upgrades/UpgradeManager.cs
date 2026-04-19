using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    [Header("Registries (drag all SOs here)")]
    [SerializeField] private List<UpgradeDisplaySO> allDisplays = new();
    [SerializeField] private List<UpgradeEffectsSO> allEffects  = new();

    [Header("External References")]
    [SerializeField] private UpgradeUIHandler upgradeUIHandler;
    private PlayerController playerController; // Set at runtime.

    // Lookup maps built once at Awake.
    private Dictionary<string, UpgradeEffectsSO>  _effectMap  = new();
    private Dictionary<string, UpgradeDisplaySO>  _displayMap = new();

    // Runtime run state.
    private Dictionary<string, int> _stacks = new();
    private List<(UpgradeEffect effect, UpgradeContext ctx)> _tickingEffects = new();

    private UpgradeContext _cachedContext;
    private PlayerController _trackedPlayer;

    // Manager lifecycle.

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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
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

    // Public API (lookup)

    public bool TryGetEffect(string id, out UpgradeEffectsSO so)  => _effectMap.TryGetValue(id, out so);
    public bool TryGetDisplay(string id, out UpgradeDisplaySO so) => _displayMap.TryGetValue(id, out so);
    public int GetStack(string id) => _stacks.TryGetValue(id, out int s) ? s : 0;
    public List<UpgradeDisplaySO> GetAppliedUpgradeDisplays()
    {
        var results = new List<UpgradeDisplaySO>();
        foreach (var pair in _stacks)
        {
            if (pair.Value <= 0) continue;
            if (!_displayMap.TryGetValue(pair.Key, out var display) || display == null) continue;

            for (int i = 0; i < pair.Value; i++)
                results.Add(display);
        }

        return results;
    }

    public int GetTotalUpgradeCount()
    {
        int total = 0;
        foreach (var pair in _stacks)
            total += pair.Value;
        return total;
    }

    public int CountUpgradesByMinimumRarity(UpgradeRarity minimumRarity)
    {
        int count = 0;
        foreach (var pair in _stacks)
        {
            if (pair.Value <= 0) continue;
            if (!_displayMap.TryGetValue(pair.Key, out var display)) continue;
            if (display.rarity < minimumRarity) continue;
            count += pair.Value;
        }

        return count;
    }

    public int CountUpgradesWithTrait(UpgradeTrait trait, string excludeUpgradeId = null)
    {
        int count = 0;
        foreach (var pair in _stacks)
        {
            if (pair.Value <= 0) continue;
            if (pair.Key == excludeUpgradeId) continue;
            if (!_effectMap.TryGetValue(pair.Key, out var effect)) continue;
            if (effect.traits == null) continue;

            foreach (var candidate in effect.traits)
            {
                if (candidate != trait) continue;
                count += pair.Value;
                break;
            }
        }

        return count;
    }

    public bool AreOnlyOtherUpgradesCommon(string excludeUpgradeId = null)
    {
        foreach (var pair in _stacks)
        {
            if (pair.Value <= 0) continue;
            if (pair.Key == excludeUpgradeId) continue;
            if (!_displayMap.TryGetValue(pair.Key, out var display)) continue;
            if (display.rarity != UpgradeRarity.Common) return false;
        }

        return true;
    }

    // Public API (upgrade screen)

    /// <summary>
    /// Called by GameplayHandler to open the upgrade screen with n choices.
    /// Passes the choices to UpgradeUIHandler for display.
    /// </summary>
    public void OpenUpgradeSelection(int count)
    {
        List<UpgradeDisplaySO> choices = GetRandomUpgradeChoices(count);
        upgradeUIHandler.DisplayUpgrades(choices);
    }

    /// <summary>
    /// Called by UpgradeUIHandler when the player clicks a card.
    /// Applies the upgrade and returns true on success.
    /// </summary>
    public bool ApplyUpgradeFromDisplay(UpgradeDisplaySO display)
    {
        return ApplyUpgrade(display.upgradeID, playerController);
    }

    // Public API (apply/revoke)

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
        ctx?.Runtime?.RefreshDynamicModifiers(this);

        foreach (var effect in effectSO.effects)
            if (effect != null && effect.NeedsTick)
                _tickingEffects.Add((effect, ctx));

        return true;
    }

    public void RevokeUpgrade(string id, PlayerController player)
    {
        if (!_effectMap.TryGetValue(id, out var effectSO)) return;
        if (GetStack(id) <= 0) return;

        _stacks[id]--;
        var ctx = GetOrBuildContext(player);
        effectSO.Remove(ctx);
        ctx?.Runtime?.RefreshDynamicModifiers(this);

        _tickingEffects.RemoveAll(pair => effectSO.effects.Contains(pair.effect));
    }

    // Public API

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

    // Run lifecycle.

    public void ResetRun(PlayerController player)
    {
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
        ctx?.Runtime?.RefreshDynamicModifiers(this);
    }

    // Helpers

    private UpgradeContext GetOrBuildContext(PlayerController player)
    {
        if (_cachedContext == null || _trackedPlayer != player)
        {
            // If no player provided, try to find one
            if (player == null)
            {
                var found = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                if (found.Length > 0)
                {
                    player = found[0];
                }
                else
                {
                    Debug.LogError("No PlayerController found when building upgrade context!");
                    return null;
                }
            }
            
            _cachedContext = UpgradeContext.FromScene(player);
            _trackedPlayer = player;
            playerController = player;
        }
        return _cachedContext;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var found = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        if (found.Length == 0)
            return;

        var nextPlayer = found[0];
        if (_trackedPlayer == nextPlayer && _cachedContext != null)
            return;

        playerController = nextPlayer;
        _trackedPlayer = nextPlayer;
        _cachedContext = UpgradeContext.FromScene(nextPlayer);
        ReapplyStacksToTrackedPlayer();
    }

    private void ReapplyStacksToTrackedPlayer()
    {
        if (_cachedContext == null)
            return;

        _tickingEffects.Clear();

        foreach (var kvp in _stacks)
        {
            if (!_effectMap.TryGetValue(kvp.Key, out var effectSO))
                continue;

            for (int i = 0; i < kvp.Value; i++)
            {
                effectSO.Apply(_cachedContext);

                foreach (var effect in effectSO.effects)
                {
                    if (effect != null && effect.NeedsTick)
                        _tickingEffects.Add((effect, _cachedContext));
                }
            }
        }

        _cachedContext.Runtime?.RefreshDynamicModifiers(this);
    }

    private static float RarityWeight(UpgradeRarity r) => r switch
    {
        UpgradeRarity.Common    => 60f,
        UpgradeRarity.Uncommon  => 25f,
        UpgradeRarity.Rare      => 12f,
        UpgradeRarity.Epic      =>  6f,
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
