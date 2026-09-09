using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the code-spawned visual root under an enemy: the floating health bar (delegated to
/// <see cref="EnemyHealthBarVisual"/>) and the status-applied burst sprites. Bounds and sorting come from the
/// enemy's own sprite renderers.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyBase))]
public sealed class EnemyWorldVisuals : MonoBehaviour
{
    private sealed class VisualRootMarker : MonoBehaviour { }

    private readonly List<SpriteRenderer> _trackedRenderers = new();
    private readonly Dictionary<StatusEffectId, Coroutine> _activeStatusAnimations = new();

    [Header("Status Burst Sprites")]
    [SerializeField] private List<Sprite> poisonAppliedFrames = new();
    [SerializeField] private List<Sprite> confusionAppliedFrames = new();
    [SerializeField] private List<Sprite> swiftnessAppliedFrames = new();
    [SerializeField] private List<Sprite> frailtyAppliedFrames = new();

    [Header("Status Burst Timing")]
    [SerializeField] private float statusFrameDuration = 0.06f;
    [SerializeField] private float statusYOffset = 0.24f;
    [SerializeField] private float statusScaleMultiplier = 1.25f;

    private EnemyBase _enemy;
    private EnemyStatusEffectController _statusEffects;
    private EliteModifier _elite;
    private bool _worldHealthBarEnabled = true;
    private Transform _visualRoot;
    private EnemyHealthBarVisual _healthBar;

    private void Awake()
    {
        _enemy = GetComponent<EnemyBase>();
        _statusEffects = GetComponent<EnemyStatusEffectController>();
        _elite = GetComponent<EliteModifier>();
        _worldHealthBarEnabled = _enemy == null || _enemy.UsesWorldFloatingHealthBar;
        CacheTrackedRenderers();
        CreateVisuals();
    }

    private void OnEnable()
    {
        if (_visualRoot == null)
        {
            CacheTrackedRenderers();
            CreateVisuals();
        }
    }

    private void LateUpdate()
    {
        if (_enemy == null || _visualRoot == null)
            return;

        if (_enemy.IsDead)
            return;

        if (_elite == null)
        {
            _elite = GetComponent<EliteModifier>();
            if (_elite != null && _worldHealthBarEnabled && _healthBar != null && !_healthBar.HasEliteOverlay)
                NotifyEliteAttached();
        }

        if (!_worldHealthBarEnabled || _healthBar == null)
            return;

        if (_enemy.CurrentHealth <= 0f)
        {
            _healthBar.SetVisible(false);
            return;
        }

        _healthBar.SetVisible(true);
        _healthBar.UpdateLayout(GetVisualBounds(), _enemy.HealthNormalized, GetHealthBarColor(), _elite);
    }

    private void OnDisable()
    {
        CleanupVisualRoot();
    }

    private void OnDestroy()
    {
        CleanupVisualRoot();
    }

    private void CacheTrackedRenderers()
    {
        _trackedRenderers.Clear();
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr != null && ShouldTrackRenderer(sr))
                _trackedRenderers.Add(sr);
        }
    }

    private void CreateVisuals(bool skipReuseExisting = false)
    {
        if (!skipReuseExisting && TryReuseExistingVisualRoot())
            return;

        GameObject root = new GameObject($"{name}_WorldVisuals");
        _visualRoot = root.transform;
        _visualRoot.SetParent(transform, false);
        _visualRoot.localPosition = Vector3.zero;
        _visualRoot.gameObject.AddComponent<VisualRootMarker>();

        if (_worldHealthBarEnabled)
            _healthBar = EnemyHealthBarVisual.Create(_visualRoot, GetSortingLayerName(), GetBaseSortingOrder(), _elite != null);
    }

    public void NotifyStatusApplied(StatusEffectId effectId)
    {
        if (_enemy == null || _enemy.IsDead || _visualRoot == null)
            return;

        List<Sprite> frames = GetStatusFrames(effectId);
        if (frames == null || frames.Count == 0)
            return;

        if (_activeStatusAnimations.TryGetValue(effectId, out Coroutine running) && running != null)
            StopCoroutine(running);

        _activeStatusAnimations[effectId] = StartCoroutine(PlayStatusAnimation(effectId, frames));
    }

    public void RebuildVisuals()
    {
        Transform oldRoot = _visualRoot;
        _visualRoot = null;
        _healthBar = null;

        StopStatusAnimations();

        if (oldRoot != null)
            Destroy(oldRoot.gameObject);

        CacheTrackedRenderers();
        CreateVisuals(skipReuseExisting: true);
    }

    /// <summary>
    /// Call when <see cref="EliteModifier"/> is added at runtime after Awake (e.g. map spawner).
    /// Refreshes the elite reference and rebuilds the health bar so shield UI appears immediately.
    /// </summary>
    public void NotifyEliteAttached()
    {
        _elite = GetComponent<EliteModifier>();
        if (_elite == null)
            return;
        RebuildVisuals();
    }

    private bool TryReuseExistingVisualRoot()
    {
        VisualRootMarker[] markers = GetComponentsInChildren<VisualRootMarker>(true);
        if (markers.Length == 0)
            return false;

        _visualRoot = markers[0].transform;
        for (int i = 1; i < markers.Length; i++)
        {
            if (markers[i] != null)
                Destroy(markers[i].gameObject);
        }

        _healthBar = EnemyHealthBarVisual.TryReuse(_visualRoot, GetSortingLayerName(), GetBaseSortingOrder());
        if (_worldHealthBarEnabled)
        {
            if (_healthBar == null)
                _healthBar = EnemyHealthBarVisual.Create(_visualRoot, GetSortingLayerName(), GetBaseSortingOrder(), _elite != null);
        }
        else if (_healthBar != null)
        {
            _healthBar.Destroy();
            _healthBar = null;
        }

        return true;
    }

    private IEnumerator PlayStatusAnimation(StatusEffectId effectId, List<Sprite> frames)
    {
        Bounds bounds = GetVisualBounds();

        GameObject go = new GameObject($"{effectId}_AppliedFx");
        go.transform.SetParent(_visualRoot, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RuntimeSprites.UnitWhite;
        sr.color = Color.white;
        sr.drawMode = SpriteDrawMode.Simple;
        sr.sortingLayerName = GetSortingLayerName();
        sr.sortingOrder = GetBaseSortingOrder() + 14;
        sr.transform.position = new Vector3(bounds.center.x, bounds.max.y + statusYOffset, bounds.center.z - 0.003f);
        sr.transform.localScale = Vector3.one * Mathf.Max(0.01f, statusScaleMultiplier);

        float frameDelay = Mathf.Max(0.01f, statusFrameDuration);
        for (int i = 0; i < frames.Count; i++)
        {
            if (sr == null)
                yield break;

            bounds = GetVisualBounds();
            sr.transform.position = new Vector3(bounds.center.x, bounds.max.y + statusYOffset, bounds.center.z - 0.003f);
            sr.sprite = frames[i];
            yield return new WaitForSeconds(frameDelay);
        }

        if (sr != null)
            Destroy(sr.gameObject);

        _activeStatusAnimations.Remove(effectId);
    }

    private Bounds GetVisualBounds()
    {
        if (_trackedRenderers.Count == 0)
            return new Bounds(transform.position, Vector3.one * 0.75f);

        Bounds bounds = _trackedRenderers[0].bounds;
        for (int i = 1; i < _trackedRenderers.Count; i++)
        {
            if (_trackedRenderers[i] != null)
                bounds.Encapsulate(_trackedRenderers[i].bounds);
        }

        return bounds;
    }

    private bool ShouldTrackRenderer(SpriteRenderer sr)
    {
        if (sr == null)
            return false;

        if (_visualRoot != null && sr.transform.IsChildOf(_visualRoot))
            return false;

        string n = sr.gameObject.name.ToLowerInvariant();
        if (n.Contains("vfx") || n.Contains("fx") || n.Contains("slash") || n.Contains("muzzle") || n.Contains("pulse"))
            return false;

        return true;
    }

    private string GetSortingLayerName()
    {
        foreach (SpriteRenderer sr in _trackedRenderers)
        {
            if (sr != null)
                return sr.sortingLayerName;
        }

        return "Default";
    }

    private int GetBaseSortingOrder()
    {
        int highest = 0;
        foreach (SpriteRenderer sr in _trackedRenderers)
        {
            if (sr != null)
                highest = Mathf.Max(highest, sr.sortingOrder);
        }

        return highest;
    }

    private void StopStatusAnimations()
    {
        foreach (Coroutine running in _activeStatusAnimations.Values)
        {
            if (running != null)
                StopCoroutine(running);
        }

        _activeStatusAnimations.Clear();
    }

    private void CleanupVisualRoot()
    {
        StopStatusAnimations();

        if (_visualRoot != null)
            Destroy(_visualRoot.gameObject);

        _visualRoot = null;
        _healthBar = null;
    }

    private List<Sprite> GetStatusFrames(StatusEffectId effectId)
    {
        return effectId switch
        {
            StatusEffectId.Poison => poisonAppliedFrames,
            StatusEffectId.Confusion => confusionAppliedFrames,
            StatusEffectId.Swiftness => swiftnessAppliedFrames,
            StatusEffectId.Frailty => frailtyAppliedFrames,
            _ => null
        };
    }

    private Color GetHealthBarColor()
    {
        if (_statusEffects == null)
            return EnemyHealthBarVisual.BaseColor;

        List<Color> activeColors = new List<Color>(4);
        AddStatusColorIfActive(activeColors, StatusEffectId.Poison);
        AddStatusColorIfActive(activeColors, StatusEffectId.Confusion);
        AddStatusColorIfActive(activeColors, StatusEffectId.Swiftness);
        AddStatusColorIfActive(activeColors, StatusEffectId.Frailty);

        if (activeColors.Count == 0)
            return EnemyHealthBarVisual.BaseColor;

        Color mixed = Color.black;
        foreach (Color c in activeColors)
            mixed += c;

        mixed /= activeColors.Count;
        mixed.a = EnemyHealthBarVisual.BaseColor.a;
        return mixed;
    }

    private void AddStatusColorIfActive(List<Color> colors, StatusEffectId effectId)
    {
        if (_statusEffects != null && _statusEffects.Has(effectId))
            colors.Add(GetStatusColor(effectId));
    }

    private static Color GetStatusColor(StatusEffectId effectId)
    {
        return effectId switch
        {
            StatusEffectId.Poison => new Color(0.3f, 0.95f, 0.35f, 1f),
            StatusEffectId.Confusion => new Color(0.75f, 0.35f, 1f, 1f),
            StatusEffectId.Swiftness => new Color(0.25f, 0.9f, 1f, 1f),
            StatusEffectId.Frailty => new Color(1f, 0.2f, 0.35f, 1f),
            _ => EnemyHealthBarVisual.BaseColor
        };
    }
}
