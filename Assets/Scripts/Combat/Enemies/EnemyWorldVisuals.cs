using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyBase))]
public sealed class EnemyWorldVisuals : MonoBehaviour
{
    private const float HealthBarScale = 0.8f;
    private const float MinBarWidth = 0.6f;
    private const float MaxBarWidth = 1.8f;
    private const float BarHeight = 0.09f;
    private const float BarYOffset = 0.22f;
    private const float FramePadding = 0.03f;

    private static Sprite s_unitSprite;

    private readonly List<SpriteRenderer> _trackedRenderers = new();
    private readonly Dictionary<string, Coroutine> _activeStatusAnimations = new();

    private static readonly Color BaseHealthBarColor = new(0.96f, 0.22f, 0.22f, 0.98f);

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
    private bool _worldHealthBarEnabled = true;
    private Transform _visualRoot;
    private Transform _barRoot;
    private SpriteRenderer _barFrameRenderer;
    private SpriteRenderer _barBackgroundRenderer;
    private SpriteRenderer _barFillRenderer;

    private void Awake()
    {
        _enemy = GetComponent<EnemyBase>();
        _statusEffects = GetComponent<EnemyStatusEffectController>();
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

        if (!_worldHealthBarEnabled || _barRoot == null)
            return;

        if (_enemy.CurrentHealth <= 0f)
        {
            if (_barRoot.gameObject.activeSelf)
                _barRoot.gameObject.SetActive(false);
            return;
        }

        if (!_barRoot.gameObject.activeSelf)
            _barRoot.gameObject.SetActive(true);

        Bounds bounds = GetVisualBounds();
        UpdateHealthBar(bounds);
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
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr != null && ShouldTrackRenderer(sr))
                _trackedRenderers.Add(sr);
        }
    }

    private void CreateVisuals()
    {
        var root = new GameObject($"{name}_WorldVisuals");
        _visualRoot = root.transform;
        _visualRoot.SetParent(transform, false);
        _visualRoot.localPosition = Vector3.zero;

        if (_worldHealthBarEnabled)
            CreateHealthBar();
    }

    public void NotifyStatusApplied(string effectId)
    {
        if (_enemy == null || _enemy.IsDead || _visualRoot == null)
            return;

        List<Sprite> frames = GetStatusFrames(effectId);
        if (frames == null || frames.Count == 0)
            return;

        if (_activeStatusAnimations.TryGetValue(effectId, out var running) && running != null)
            StopCoroutine(running);

        _activeStatusAnimations[effectId] = StartCoroutine(PlayStatusAnimation(effectId, frames));
    }

    private void CreateHealthBar()
    {
        _barRoot = new GameObject("HealthBar").transform;
        _barRoot.SetParent(_visualRoot, false);

        _barFrameRenderer = CreateSpriteRenderer("Frame", _barRoot, new Color(0.04f, 0.04f, 0.04f, 0.95f), 10);
        _barBackgroundRenderer = CreateSpriteRenderer("Background", _barRoot, new Color(0.18f, 0.08f, 0.08f, 0.95f), 11);
        _barFillRenderer = CreateSpriteRenderer("Fill", _barRoot, BaseHealthBarColor, 12);
    }

    private SpriteRenderer CreateSpriteRenderer(string objectName, Transform parent, Color color, int sortingOrder)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(parent, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetUnitSprite();
        sr.color = color;
        sr.sortingLayerName = GetSortingLayerName();
        sr.sortingOrder = GetBaseSortingOrder() + sortingOrder;
        return sr;
    }

    private void UpdateHealthBar(Bounds bounds)
    {
        float barWidth = Mathf.Clamp(bounds.size.x * 0.95f, MinBarWidth, MaxBarWidth) * HealthBarScale;
        float normalized = _enemy.HealthNormalized;
        float fillWidth = Mathf.Max(0.0001f, barWidth * normalized);
        float y = bounds.max.y + BarYOffset;
        float barHeight = BarHeight * HealthBarScale;
        float framePadding = FramePadding * HealthBarScale;

        _barRoot.position = new Vector3(bounds.center.x, y, bounds.center.z);
        _barFrameRenderer.transform.localScale = new Vector3(barWidth + framePadding, barHeight + framePadding, 1f);
        _barBackgroundRenderer.transform.localScale = new Vector3(barWidth, barHeight, 1f);
        _barFillRenderer.transform.localScale = new Vector3(fillWidth, barHeight * 0.82f, 1f);
        _barFillRenderer.color = GetHealthBarColor();

        float leftEdge = -barWidth * 0.5f;
        _barFillRenderer.transform.localPosition = new Vector3(leftEdge + fillWidth * 0.5f, 0f, 0f);
    }

    private IEnumerator PlayStatusAnimation(string effectId, List<Sprite> frames)
    {
        Bounds bounds = GetVisualBounds();
        var sr = CreateSpriteRenderer($"{effectId}_AppliedFx", _visualRoot, Color.white, 14);
        sr.drawMode = SpriteDrawMode.Simple;
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
        foreach (var sr in _trackedRenderers)
        {
            if (sr != null)
                return sr.sortingLayerName;
        }

        return "Default";
    }

    private int GetBaseSortingOrder()
    {
        int highest = 0;
        foreach (var sr in _trackedRenderers)
        {
            if (sr != null)
                highest = Mathf.Max(highest, sr.sortingOrder);
        }

        return highest;
    }

    private void CleanupVisualRoot()
    {
        foreach (var running in _activeStatusAnimations.Values)
        {
            if (running != null)
                StopCoroutine(running);
        }

        _activeStatusAnimations.Clear();

        if (_visualRoot != null)
            Destroy(_visualRoot.gameObject);

        _visualRoot = null;
    }

    private List<Sprite> GetStatusFrames(string effectId)
    {
        return effectId switch
        {
            StatusEffectIds.Poison => poisonAppliedFrames,
            StatusEffectIds.Confusion => confusionAppliedFrames,
            StatusEffectIds.Swiftness => swiftnessAppliedFrames,
            StatusEffectIds.Frailty => frailtyAppliedFrames,
            _ => null
        };
    }

    private Color GetHealthBarColor()
    {
        if (_statusEffects == null)
            return BaseHealthBarColor;

        var activeColors = new List<Color>(4);
        AddStatusColorIfActive(activeColors, StatusEffectIds.Poison);
        AddStatusColorIfActive(activeColors, StatusEffectIds.Confusion);
        AddStatusColorIfActive(activeColors, StatusEffectIds.Swiftness);
        AddStatusColorIfActive(activeColors, StatusEffectIds.Frailty);

        if (activeColors.Count == 0)
            return BaseHealthBarColor;

        Color mixed = Color.black;
        foreach (var c in activeColors)
            mixed += c;

        mixed /= activeColors.Count;
        mixed.a = BaseHealthBarColor.a;
        return mixed;
    }

    private void AddStatusColorIfActive(List<Color> colors, string effectId)
    {
        if (_statusEffects != null && _statusEffects.Has(effectId))
            colors.Add(GetStatusColor(effectId));
    }

    private static Color GetStatusColor(string effectId)
    {
        return effectId switch
        {
            StatusEffectIds.Poison => new Color(0.3f, 0.95f, 0.35f, 1f),
            StatusEffectIds.Confusion => new Color(0.75f, 0.35f, 1f, 1f),
            StatusEffectIds.Swiftness => new Color(0.25f, 0.9f, 1f, 1f),
            StatusEffectIds.Frailty => new Color(1f, 0.2f, 0.35f, 1f),
            _ => BaseHealthBarColor
        };
    }

    private static Sprite GetUnitSprite()
    {
        if (s_unitSprite != null)
            return s_unitSprite;

        var texture = Texture2D.whiteTexture;
        s_unitSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
        return s_unitSprite;
    }
}
