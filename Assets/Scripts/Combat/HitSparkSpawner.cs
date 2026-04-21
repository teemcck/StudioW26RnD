using UnityEngine;

/// <summary> Hit feedback tier — drives scale, light peak, and optional future VFX variants. </summary>
public enum HitSparkCategory
{
    Normal,
    Crit,
    BossArmor,
    Shield
}

[DefaultExecutionOrder(-100)]
public sealed class HitSparkSpawner : MonoBehaviour
{
    public static HitSparkSpawner Instance { get; private set; }

    [SerializeField] private GameObject hitSparkPrefab;
    [SerializeField] private float sparkLifetime = 0.22f;
    [SerializeField] private float sparkStartScale = 0.52f;
    [SerializeField] private float sparkEndScale = 0.08f;

    [Header("Per-category scale (game feel)")]
    [SerializeField] private float scaleMulNormal = 1f;
    [SerializeField] private float scaleMulCrit = 1.42f;
    [SerializeField] private float scaleMulBossArmor = 1.38f;
    [SerializeField] private float scaleMulShield = 1.55f;

    [Header("Light flicker (additive; non-Map layers via TransientPointLight2D)")]
    [SerializeField] private bool enableSparkLight = true;
    [SerializeField] [Range(0.05f, 1.2f)] private float sparkLightPeak = 0.22f;
    [SerializeField] [Range(0.05f, 1.2f)] private float sparkLightPeakCrit = 0.38f;
    [SerializeField] [Range(0.05f, 1.2f)] private float sparkLightPeakBoss = 0.34f;
    [SerializeField] [Range(0.05f, 1.2f)] private float sparkLightPeakShield = 0.3f;
    [SerializeField] private float sparkLightOuterRadius = 0.62f;
    [SerializeField] private float sparkLightInnerRadius = 0.04f;
    [SerializeField] private float sparkLightDuration = 0.09f;

    [Header("Sorting (must draw above map tiles)")]
    [SerializeField] private string vfxSortingLayerName = "VFX";
    [SerializeField] private int vfxSortingOrder = 420;

    private IEventBinding<EnemyDamagedEvent> _binding;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        _binding = EventBus<EnemyDamagedEvent>.Register(OnEnemyDamaged);
    }

    private void OnDisable()
    {
        if (_binding != null) EventBus<EnemyDamagedEvent>.Unsubscribe(_binding);
        _binding = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnemyDamaged(EnemyDamagedEvent evt)
    {
        if (evt.Context.IsStatusEffect) return;

        var category = Classify(evt);
        Spawn(evt.Position, PickColor(category), category);
        if (evt.Context.IsCrit && AudioManager.Instance != null)
            AudioManager.Instance.PlayCritHit();
    }

    private static HitSparkCategory Classify(in EnemyDamagedEvent evt)
    {
        if (evt.Enemy != null && evt.Enemy.GetType().Name.Contains("Boss"))
            return HitSparkCategory.BossArmor;
        if (evt.Context.IsCrit)
            return HitSparkCategory.Crit;
        return HitSparkCategory.Normal;
    }

    private static Color PickColor(HitSparkCategory category)
    {
        switch (category)
        {
            case HitSparkCategory.BossArmor:
                return GameColors.HitBossArmor;
            case HitSparkCategory.Crit:
                return GameColors.HitCrit;
            case HitSparkCategory.Shield:
                return GameColors.HitShield;
            default:
                return GameColors.HitNormal;
        }
    }

    /// <summary> Shield chip / block (elite shield) — call from <see cref="EnemyBase"/> when damage is fully absorbed. </summary>
    public void Spawn(Vector2 position, Color color, HitSparkCategory category)
    {
        if (hitSparkPrefab == null) return;

        var go = Instantiate(hitSparkPrefab, new Vector3(position.x, position.y, 0f), Quaternion.identity);
        var sr = go.GetComponent<SpriteRenderer>() ?? go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = color;
            sr.sortingLayerID = SortingLayer.NameToID(vfxSortingLayerName);
            sr.sortingOrder = vfxSortingOrder;
        }

        float catMul = CategoryScaleMul(category);
        float startScale = sparkStartScale * catMul;
        float endScale = sparkEndScale * Mathf.Lerp(1f, 1.15f, catMul - 1f);
        float life = sparkLifetime * (category == HitSparkCategory.Shield ? 1.35f : category == HitSparkCategory.BossArmor ? 1.2f : 1f);

        var anim = go.AddComponent<HitSparkAnim>();
        anim.Init(sr, startScale, endScale, life);

        if (enableSparkLight)
        {
            float peak = PeakForCategory(category);
            TransientPointLight2D.Spawn(
                go.transform.position,
                color,
                peak,
                sparkLightInnerRadius,
                sparkLightOuterRadius,
                sparkLightDuration,
                useUnscaledTime: true);
        }
    }

    float CategoryScaleMul(HitSparkCategory category)
    {
        return category switch
        {
            HitSparkCategory.Crit => scaleMulCrit,
            HitSparkCategory.BossArmor => scaleMulBossArmor,
            HitSparkCategory.Shield => scaleMulShield,
            _ => scaleMulNormal
        };
    }

    float PeakForCategory(HitSparkCategory category)
    {
        return category switch
        {
            HitSparkCategory.Crit => sparkLightPeakCrit,
            HitSparkCategory.BossArmor => sparkLightPeakBoss,
            HitSparkCategory.Shield => sparkLightPeakShield,
            _ => sparkLightPeak
        };
    }
}

internal sealed class HitSparkAnim : MonoBehaviour
{
    private SpriteRenderer _sr;
    private float _startScale;
    private float _endScale;
    private float _lifetime;
    private float _age;
    private float _spin;

    public void Init(SpriteRenderer sr, float startScale, float endScale, float lifetime)
    {
        _sr = sr;
        _startScale = startScale;
        _endScale = endScale;
        _lifetime = Mathf.Max(0.02f, lifetime);
        transform.localScale = Vector3.one * _startScale;
        _spin = Random.Range(-220f, 220f);
    }

    private void Update()
    {
        _age += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_age / _lifetime);
        transform.localScale = Vector3.one * Mathf.Lerp(_startScale, _endScale, t);
        if (Mathf.Abs(_spin) > 1f)
            transform.Rotate(0f, 0f, _spin * Time.unscaledDeltaTime);

        if (_sr != null)
        {
            var c = _sr.color;
            c.a = 1f - t;
            _sr.color = c;
        }

        if (_age >= _lifetime) Destroy(gameObject);
    }
}
