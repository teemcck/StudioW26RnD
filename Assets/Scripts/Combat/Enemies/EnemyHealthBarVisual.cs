using UnityEngine;

/// <summary>
/// World-space floating health bar (frame, background, fill, optional elite shield overlay) built from unit
/// sprites under an enemy's visual root. Plain class driven by <see cref="EnemyWorldVisuals"/>, which supplies
/// bounds, colours and sorting; this class only owns bar geometry.
/// </summary>
public sealed class EnemyHealthBarVisual
{
    public static readonly Color BaseColor = new(0.96f, 0.22f, 0.22f, 0.98f);

    private const string RootName = "HealthBar";
    private const string EliteOverlayName = "EliteShieldFrameOverlay";
    private const float HealthBarScale = 0.8f;
    private const float MinBarWidth = 0.6f;
    private const float MaxBarWidth = 1.8f;
    private const float BarHeight = 0.09f;
    private const float BarYOffset = 0.22f;
    private const float FramePadding = 0.03f;
    private static readonly Color FrameColor = new(0.04f, 0.04f, 0.04f, 0.95f);
    private static readonly Color BackgroundColor = new(0.18f, 0.08f, 0.08f, 0.95f);

    private readonly Transform _root;
    private readonly string _sortingLayerName;
    private readonly int _baseSortingOrder;
    private SpriteRenderer _frame;
    private SpriteRenderer _background;
    private SpriteRenderer _fill;
    /// <summary>Yellow overlay on the frame: same outer size as <see cref="_frame"/>, width scales from the left with shield.</summary>
    private SpriteRenderer _eliteShieldFrameOverlay;

    public bool HasEliteOverlay => _eliteShieldFrameOverlay != null;

    private EnemyHealthBarVisual(Transform root, string sortingLayerName, int baseSortingOrder)
    {
        _root = root;
        _sortingLayerName = sortingLayerName;
        _baseSortingOrder = baseSortingOrder;
    }

    public static EnemyHealthBarVisual Create(Transform visualRoot, string sortingLayerName, int baseSortingOrder, bool includeEliteOverlay)
    {
        Transform root = new GameObject(RootName).transform;
        root.SetParent(visualRoot, false);

        EnemyHealthBarVisual bar = new EnemyHealthBarVisual(root, sortingLayerName, baseSortingOrder);
        bar._frame = bar.CreateSpriteRenderer("Frame", FrameColor, 10);
        if (includeEliteOverlay)
            bar.CreateEliteShieldFrameOverlay();
        bar._background = bar.CreateSpriteRenderer("Background", BackgroundColor, 12);
        bar._fill = bar.CreateSpriteRenderer("Fill", BaseColor, 13);
        return bar;
    }

    /// <summary>Re-adopts a bar left under a reused visual root, or returns null if none (or an incomplete one) exists.</summary>
    public static EnemyHealthBarVisual TryReuse(Transform visualRoot, string sortingLayerName, int baseSortingOrder)
    {
        Transform root = visualRoot != null ? visualRoot.Find(RootName) : null;
        if (root == null)
            return null;

        EnemyHealthBarVisual bar = new EnemyHealthBarVisual(root, sortingLayerName, baseSortingOrder);
        bar._frame = FindRenderer(root, "Frame");
        bar._background = FindRenderer(root, "Background");
        bar._fill = FindRenderer(root, "Fill");
        bar._eliteShieldFrameOverlay = FindRenderer(root, EliteOverlayName);

        if (bar._frame == null || bar._background == null || bar._fill == null)
        {
            Object.Destroy(root.gameObject);
            return null;
        }

        return bar;
    }

    public void SetVisible(bool visible)
    {
        if (_root != null && _root.gameObject.activeSelf != visible)
            _root.gameObject.SetActive(visible);
    }

    public void Destroy()
    {
        if (_root != null)
            Object.Destroy(_root.gameObject);
    }

    /// <summary>Positions the bar above <paramref name="bounds"/> and sizes the fill; <paramref name="elite"/> may be null.</summary>
    public void UpdateLayout(Bounds bounds, float healthNormalized, Color fillColor, EliteModifier elite)
    {
        float barWidth = Mathf.Clamp(bounds.size.x * 0.95f, MinBarWidth, MaxBarWidth) * HealthBarScale;
        float fillWidth = Mathf.Max(0.0001f, barWidth * healthNormalized);
        float y = bounds.max.y + BarYOffset;
        float barHeight = BarHeight * HealthBarScale;
        float framePadding = FramePadding * HealthBarScale;

        _root.position = new Vector3(bounds.center.x, y, bounds.center.z);
        _frame.transform.localScale = new Vector3(barWidth + framePadding, barHeight + framePadding, 1f);
        _background.transform.localScale = new Vector3(barWidth, barHeight, 1f);
        _fill.transform.localScale = new Vector3(fillWidth, barHeight * 0.82f, 1f);
        _fill.color = fillColor;
        _frame.color = FrameColor;

        if (elite != null)
            UpdateEliteShieldFrameOverlay(barWidth, barHeight, framePadding, elite);

        float leftEdge = -barWidth * 0.5f;
        _fill.transform.localPosition = new Vector3(leftEdge + fillWidth * 0.5f, 0f, 0f);
    }

    private static SpriteRenderer FindRenderer(Transform root, string childName)
    {
        Transform child = root.Find(childName);
        return child ? child.GetComponent<SpriteRenderer>() : null;
    }

    private SpriteRenderer CreateSpriteRenderer(string objectName, Color color, int sortingOffset)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(_root, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RuntimeSprites.UnitWhite;
        sr.color = color;
        sr.sortingLayerName = _sortingLayerName;
        sr.sortingOrder = _baseSortingOrder + sortingOffset;
        return sr;
    }

    private void CreateEliteShieldFrameOverlay()
    {
        Color y = GameColors.EliteAccent;
        y.a = 1f;
        _eliteShieldFrameOverlay = CreateSpriteRenderer(EliteOverlayName, y, 11);
        _eliteShieldFrameOverlay.transform.localPosition = Vector3.zero;
    }

    /// <summary>
    /// Same outer rect as the dark frame; width = full width × shield (left edge fixed, shrinks toward the right).
    /// </summary>
    private void UpdateEliteShieldFrameOverlay(float barWidth, float barHeight, float framePadding, EliteModifier elite)
    {
        if (_eliteShieldFrameOverlay == null)
            CreateEliteShieldFrameOverlay();

        float shieldFrac = elite.HasShield ? Mathf.Clamp01(elite.ShieldNormalized) : 0f;
        if (!elite.HasShield || shieldFrac <= 0.001f)
        {
            _eliteShieldFrameOverlay.gameObject.SetActive(false);
            return;
        }

        float outerW = barWidth + framePadding;
        float outerH = barHeight + framePadding;
        float halfW = outerW * 0.5f;
        float w = outerW * shieldFrac;

        _eliteShieldFrameOverlay.gameObject.SetActive(true);
        _eliteShieldFrameOverlay.transform.localScale = new Vector3(w, outerH, 1f);
        _eliteShieldFrameOverlay.transform.localPosition = new Vector3(-halfW + w * 0.5f, 0f, -0.002f);

        Color y = GameColors.EliteAccent;
        y.a = 1f;
        if (elite.IsShieldRegenerating)
            y = Color.Lerp(y, new Color(1f, 0.98f, 0.45f, 1f), 0.4f);
        _eliteShieldFrameOverlay.color = y;
    }
}
