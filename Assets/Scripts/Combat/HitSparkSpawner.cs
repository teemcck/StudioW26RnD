using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class HitSparkSpawner : MonoBehaviour
{
    public static HitSparkSpawner Instance { get; private set; }

    [SerializeField] private GameObject hitSparkPrefab;
    [SerializeField] private float sparkLifetime = 0.18f;
    [SerializeField] private float sparkStartScale = 0.35f;
    [SerializeField] private float sparkEndScale = 0.05f;

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

        Spawn(evt.Position, PickColor(evt), evt.Context.IsCrit);
        if (evt.Context.IsCrit && AudioManager.Instance != null)
            AudioManager.Instance.PlayCritHit();
    }

    private static Color PickColor(in EnemyDamagedEvent evt)
    {
        if (evt.Enemy != null && evt.Enemy.GetType().Name.Contains("Boss"))
            return GameColors.HitBossArmor;
        return evt.Context.IsCrit ? GameColors.HitCrit : GameColors.HitNormal;
    }

    public void Spawn(Vector2 position, Color color, bool emphasized = false)
    {
        if (hitSparkPrefab == null) return;

        var go = Instantiate(hitSparkPrefab, new Vector3(position.x, position.y, 0f), Quaternion.identity);
        var sr = go.GetComponent<SpriteRenderer>() ?? go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.color = color;

        float scale = emphasized ? sparkStartScale * 1.6f : sparkStartScale;
        float endScale = emphasized ? sparkEndScale * 1.3f : sparkEndScale;
        float life = emphasized ? sparkLifetime * 1.3f : sparkLifetime;

        go.AddComponent<HitSparkAnim>().Init(sr, scale, endScale, life);
    }
}

internal sealed class HitSparkAnim : MonoBehaviour
{
    private SpriteRenderer _sr;
    private float _startScale;
    private float _endScale;
    private float _lifetime;
    private float _age;

    public void Init(SpriteRenderer sr, float startScale, float endScale, float lifetime)
    {
        _sr = sr;
        _startScale = startScale;
        _endScale = endScale;
        _lifetime = Mathf.Max(0.02f, lifetime);
        transform.localScale = Vector3.one * _startScale;
    }

    private void Update()
    {
        _age += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_age / _lifetime);
        transform.localScale = Vector3.one * Mathf.Lerp(_startScale, _endScale, t);
        if (_sr != null)
        {
            var c = _sr.color;
            c.a = 1f - t;
            _sr.color = c;
        }
        if (_age >= _lifetime) Destroy(gameObject);
    }
}
