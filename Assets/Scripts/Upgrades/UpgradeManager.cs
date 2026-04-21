using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    [Tooltip("Optional; PlayerHudUI also pushes prefabs at runtime.")]
    [SerializeField] private GameObject appliedUpgradeStripIconPrefab;
    [SerializeField] private GameObject appliedUpgradeStripOverflowChipPrefab;

    private AppliedUpgradeStripUI _persistentAppliedUpgradeStrip;

    private PlayerController playerController; // Set at runtime.

    /// <summary>DontDestroyOnLoad strip UI — survives into BossGameplay and stays in sync via <see cref="RefreshAppliedUpgradeStripUi"/>.</summary>
    public AppliedUpgradeStripUI PersistentAppliedUpgradeStrip => _persistentAppliedUpgradeStrip;

    // Lookup maps built once at Awake.
    private Dictionary<string, UpgradeEffectsSO>  _effectMap  = new();
    private Dictionary<string, UpgradeDisplaySO>  _displayMap = new();

    // Runtime run state.
    private Dictionary<string, int> _stacks = new();
    private List<(UpgradeEffect effect, UpgradeContext ctx)> _tickingEffects = new();

    private UpgradeContext _cachedContext;
    private PlayerController _trackedPlayer;
    private Coroutine _deferredRebindRoutine;
    private IEventBinding<PlayerDiedEvent> _playerDiedBinding;

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
        _playerDiedBinding = EventBus<PlayerDiedEvent>.Register(OnPlayerDied);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EventBus<PlayerDiedEvent>.Unsubscribe(_playerDiedBinding);
    }

    private void OnPlayerDied(PlayerDiedEvent evt)
    {
        var pc = FindPreferredPlayerController();
        if (pc != null)
            ResetRun(pc);
        else
            ClearStacksWithoutEffectRemoval();
    }

    /// <summary>
    /// Clears upgrade stacks when no player exists to run effect Remove() (edge case after scene tear-down).
    /// </summary>
    private void ClearStacksWithoutEffectRemoval()
    {
        _stacks.Clear();
        _tickingEffects.Clear();
        _cachedContext = null;
        _trackedPlayer = null;
        playerController = null;
        RefreshAppliedUpgradeStripUi();
        PlayerHudUI.InvalidateAllDisplayedValues();
        StartupUpgradeDebugState.AlignWithUpgradeManager(this);
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
    public PlayerController CurrentPlayer => playerController;
    public List<UpgradeDisplaySO> GetAppliedUpgradeDisplays()
    {
        var results = new List<UpgradeDisplaySO>();
        foreach (var display in allDisplays)
        {
            if (display == null || string.IsNullOrEmpty(display.upgradeID))
                continue;
            if (GetStack(display.upgradeID) <= 0)
                continue;
            results.Add(display);
        }

        return results;
    }

    public static void RefreshAppliedUpgradeStripUi()
    {
        if (Instance != null && Instance._persistentAppliedUpgradeStrip != null)
        {
            Instance._persistentAppliedUpgradeStrip.RefreshFromManager();
            return;
        }

        foreach (var strip in Object.FindObjectsByType<AppliedUpgradeStripUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            strip.RefreshFromManager();
    }

    /// <summary>
    /// Creates a screen-space overlay strip under this DDOL object (once) and removes legacy scene strips
    /// so only one strip exists. Call from <see cref="PlayerHudUI"/> with the same prefabs as the player HUD.
    /// </summary>
    public void EnsurePersistentAppliedUpgradeStrip(GameObject iconPrefab, GameObject chipPrefab)
    {
        if (iconPrefab != null)
            appliedUpgradeStripIconPrefab = iconPrefab;
        if (chipPrefab != null)
            appliedUpgradeStripOverflowChipPrefab = chipPrefab;

        if (_persistentAppliedUpgradeStrip == null)
            TryRebindExistingAppliedUpgradeStrip();

        if (_persistentAppliedUpgradeStrip != null)
        {
            _persistentAppliedUpgradeStrip.AssignDefaultPrefabsIfEmpty(appliedUpgradeStripIconPrefab, appliedUpgradeStripOverflowChipPrefab);
            _persistentAppliedUpgradeStrip.RefreshFromManager();
            DestroyLegacyAppliedUpgradeStripRoots(_persistentAppliedUpgradeStrip.gameObject);
            return;
        }

        var host = new GameObject("PersistentAppliedUpgradeStrip");
        host.transform.SetParent(transform, false);

        var canvas = host.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        host.AddComponent<GraphicRaycaster>();

        var scaler = host.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _persistentAppliedUpgradeStrip = host.AddComponent<AppliedUpgradeStripUI>();
        _persistentAppliedUpgradeStrip.AssignDefaultPrefabsIfEmpty(appliedUpgradeStripIconPrefab, appliedUpgradeStripOverflowChipPrefab);
        _persistentAppliedUpgradeStrip.EnsureStripRootUnderCanvas(canvas);
        _persistentAppliedUpgradeStrip.RefreshFromManager();

        DestroyLegacyAppliedUpgradeStripRoots(host);
    }

    /// <summary>
    /// Rebinds <see cref="_persistentAppliedUpgradeStrip"/> when the field was cleared but a strip still exists
    /// (e.g. scene change / debug jump to World 2 while DDOL manager keeps running).
    /// </summary>
    private void TryRebindExistingAppliedUpgradeStrip()
    {
        var underManager = GetComponentsInChildren<AppliedUpgradeStripUI>(true);
        if (underManager != null && underManager.Length > 0)
        {
            for (int i = 0; i < underManager.Length; i++)
            {
                if (underManager[i] != null)
                {
                    _persistentAppliedUpgradeStrip = underManager[i];
                    EnsureStripHostParentedToManager(_persistentAppliedUpgradeStrip);
                    return;
                }
            }
        }

        var all = Object.FindObjectsByType<AppliedUpgradeStripUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var s = all[i];
            if (s == null)
                continue;
            if (!s.gameObject.name.StartsWith("PersistentAppliedUpgradeStrip"))
                continue;
            _persistentAppliedUpgradeStrip = s;
            EnsureStripHostParentedToManager(s);
            return;
        }

        if (all.Length == 1 && all[0] != null)
        {
            _persistentAppliedUpgradeStrip = all[0];
            EnsureStripHostParentedToManager(all[0]);
        }
    }

    private void EnsureStripHostParentedToManager(AppliedUpgradeStripUI strip)
    {
        if (strip == null)
            return;
        Transform root = strip.transform;
        if (root.parent != transform)
            root.SetParent(transform, worldPositionStays: true);
    }

    static void DestroyLegacyAppliedUpgradeStripRoots(GameObject keepHost)
    {
        var strips = Object.FindObjectsByType<AppliedUpgradeStripUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var strip in strips)
        {
            if (strip == null)
                continue;
            if (strip.gameObject == keepHost)
                continue;
            Destroy(strip.gameObject);
        }
    }

    public int GetTotalUpgradeCount()
    {
        int total = 0;
        foreach (var pair in _stacks)
            total += pair.Value;
        return total;
    }

    public List<UpgradeDisplaySO> GetAllUpgradeDisplays()
    {
        return allDisplays
            .Where(display => display != null && !string.IsNullOrEmpty(display.upgradeID))
            .OrderBy(display => display.upgradeName)
            .ToList();
    }

    public int GetMaxStacksForUpgrade(string id)
    {
        if (string.IsNullOrEmpty(id))
            return 0;
        return _effectMap.TryGetValue(id, out var effect) ? effect.maxStacks : 0;
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
        ResolveUpgradeUiHandler();
        if (upgradeUIHandler == null)
        {
            Debug.LogError("[UpgradeManager] Cannot open upgrade selection because no UpgradeUIHandler is available in the active scene.");
            return;
        }

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

        RefreshAppliedUpgradeStripUi();
        PlayerHudUI.InvalidateAllDisplayedValues();
        StartupUpgradeDebugState.AlignWithUpgradeManager(this);
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
        RefreshAppliedUpgradeStripUi();
        PlayerHudUI.InvalidateAllDisplayedValues();
        StartupUpgradeDebugState.AlignWithUpgradeManager(this);
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
        RefreshAppliedUpgradeStripUi();
        PlayerHudUI.InvalidateAllDisplayedValues();
        StartupUpgradeDebugState.AlignWithUpgradeManager(this);
    }

    // Helpers

    private UpgradeContext GetOrBuildContext(PlayerController player)
    {
        if (player == null)
            player = FindPreferredPlayerController();

        if (player == null)
        {
            Debug.LogError("No PlayerController found when building upgrade context!");
            return null;
        }

        if (_cachedContext != null && _trackedPlayer == player)
            return _cachedContext;

        _cachedContext = UpgradeContext.FromScene(player);
        _trackedPlayer = player;
        playerController = player;
        return _cachedContext;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveUpgradeUiHandler();
        if (_deferredRebindRoutine != null)
        {
            StopCoroutine(_deferredRebindRoutine);
            _deferredRebindRoutine = null;
        }
        _deferredRebindRoutine = StartCoroutine(DeferredRebindPlayerAfterSceneLoad());
    }

    private IEnumerator DeferredRebindPlayerAfterSceneLoad()
    {
        yield return null;

        var nextPlayer = FindPreferredPlayerController();
        if (nextPlayer == null)
        {
            _deferredRebindRoutine = null;
            yield break;
        }

        if (_trackedPlayer == nextPlayer && _cachedContext != null)
        {
            _deferredRebindRoutine = null;
            yield break;
        }

        playerController = nextPlayer;
        _trackedPlayer = nextPlayer;
        _cachedContext = UpgradeContext.FromScene(nextPlayer);
        ReapplyStacksToTrackedPlayer();
        _deferredRebindRoutine = null;
    }

    private static PlayerController FindPreferredPlayerController()
    {
        var tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null)
        {
            var pc = tagged.GetComponent<PlayerController>() ?? tagged.GetComponentInChildren<PlayerController>(true);
            if (pc != null)
                return pc;
        }

        var found = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return found.Length > 0 ? found[0] : null;
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
        RefreshAppliedUpgradeStripUi();
        PlayerHudUI.InvalidateAllDisplayedValues();
        StartupUpgradeDebugState.AlignWithUpgradeManager(this);
    }

    private void ResolveUpgradeUiHandler()
    {
        if (upgradeUIHandler != null)
            return;

        upgradeUIHandler = UpgradeUIHandler.Instance;
        if (upgradeUIHandler == null)
            upgradeUIHandler = FindFirstObjectByType<UpgradeUIHandler>(FindObjectsInactive.Include);
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
