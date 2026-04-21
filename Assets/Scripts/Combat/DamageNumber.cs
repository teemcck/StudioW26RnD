using TMPro;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class DamageNumberSpawner : MonoBehaviour
{
    public static DamageNumberSpawner Instance { get; private set; }

    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] private float damageThreshold = 8f;
    [SerializeField] private float normalFontSize = 16f;
    [SerializeField] private float critFontSize = 30f;
    [SerializeField] private float riseDistance = 0.6f;
    [SerializeField] private float critRiseDistance = 1.1f;
    [SerializeField] private float lifetime = 0.6f;
    [SerializeField] private float critLifetime = 0.95f;

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
        if (!evt.Context.IsCrit && evt.DamageDealt < damageThreshold) return;
        Spawn(evt.Position, evt.DamageDealt, evt.Context.IsCrit);
    }

    public void Spawn(Vector2 worldPos, float damage, bool isCrit)
    {
        if (damageNumberPrefab == null) return;

        Vector3 spawnPos = new Vector3(worldPos.x + Random.Range(-0.15f, 0.15f), worldPos.y + 0.4f, 0f);
        var go = Instantiate(damageNumberPrefab, spawnPos, Quaternion.identity);
        var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp == null) return;

        int rounded = Mathf.RoundToInt(damage);
        tmp.text = isCrit ? $"{rounded}!" : rounded.ToString();
        tmp.fontSize = isCrit ? critFontSize : normalFontSize;
        tmp.fontStyle = isCrit ? FontStyles.Italic : FontStyles.Normal;
        tmp.color = isCrit ? GameColors.HitCrit : Color.white;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        tmp.outlineWidth = isCrit ? 0.26f : 0.18f;

        if (isCrit)
        {
            tmp.enableVertexGradient = true;
            tmp.colorGradient = new VertexGradient(
                new Color(1f, 0.95f, 0.55f),
                new Color(1f, 0.95f, 0.55f),
                new Color(1f, 0.55f, 0.1f),
                new Color(1f, 0.55f, 0.1f));

            if (Hitstop.Instance != null)
                Hitstop.Instance.Freeze(0.07f, priority: 2);
            var cam = Object.FindFirstObjectByType<CameraController>();
            if (cam != null) cam.ShakeTap();
        }
        else
        {
            tmp.enableVertexGradient = false;
        }

        var anim = go.AddComponent<DamageNumberAnim>();
        anim.Init(go.transform, tmp, isCrit ? critRiseDistance : riseDistance, isCrit ? critLifetime : lifetime, isCrit);
    }
}

internal sealed class DamageNumberAnim : MonoBehaviour
{
    private Transform _root;
    private TextMeshProUGUI _tmp;
    private Vector3 _startPos;
    private Vector3 _startScale;
    private float _rise;
    private float _lifetime;
    private float _age;
    private bool _isCrit;

    public void Init(Transform root, TextMeshProUGUI tmp, float rise, float lifetime, bool isCrit)
    {
        _root = root;
        _tmp = tmp;
        _startPos = root.position;
        _startScale = root.localScale;
        _rise = rise;
        _lifetime = Mathf.Max(0.05f, lifetime);
        _isCrit = isCrit;
    }

    private void Update()
    {
        _age += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_age / _lifetime);

        if (_root != null)
            _root.position = _startPos + new Vector3(0f, _rise * Mathf.SmoothStep(0f, 1f, t), 0f);

        if (_tmp != null)
        {
            var c = _tmp.color;
            c.a = 1f - t;
            _tmp.color = c;

            if (_isCrit && _root != null)
            {
                float punchT = Mathf.Clamp01(_age / 0.22f);
                float punch = 1f + (1f - punchT) * 0.3f;
                _root.localScale = _startScale * punch;
            }
            else if (_root != null)
            {
                _root.localScale = _startScale;
            }
        }

        if (_age >= _lifetime)
            Destroy(_root != null ? _root.gameObject : gameObject);
    }
}
