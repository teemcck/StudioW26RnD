using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerDustPuffer : MonoBehaviour
{
    [SerializeField] private GameObject dustPuffPrefab;
    [SerializeField] private GameObject dashSkidPuffPrefab;
    [SerializeField] private float runThreshold = 0.75f;
    [SerializeField] private float footstepInterval = 0.28f;
    [SerializeField] private Color dustColor = new Color(0.85f, 0.78f, 0.62f, 0.55f);
    [SerializeField] private float skidDuration = 0.35f;

    private Rigidbody2D _rb;
    private PlayerDashController _dash;
    private bool _wasRunning;
    private float _nextFootstepTime;
    private IEventBinding<PlayerDashedEvent> _dashedBinding;
    private bool _dashActive;
    private Vector2 _dashStoredVelocity;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _dash = GetComponent<PlayerDashController>();
    }

    private void OnEnable()
    {
        _dashedBinding = EventBus<PlayerDashedEvent>.Register(OnDashed);
    }

    private void OnDisable()
    {
        if (_dashedBinding != null) EventBus<PlayerDashedEvent>.Unsubscribe(_dashedBinding);
        _dashedBinding = null;
    }

    private void Update()
    {
        float speed = _rb.linearVelocity.magnitude;
        bool running = speed > runThreshold;

        if (running && !_wasRunning) SpawnPuff(0.55f);
        else if (!running && _wasRunning) SpawnPuff(0.35f);
        _wasRunning = running;

        if (running && Time.time >= _nextFootstepTime)
        {
            _nextFootstepTime = Time.time + footstepInterval;
            SpawnPuff(0.45f);
        }

        if (_dashActive && _rb.linearVelocity.magnitude < _dashStoredVelocity.magnitude * 0.3f)
        {
            SpawnSkid(_dashStoredVelocity.normalized);
            _dashActive = false;
        }
    }

    private void OnDashed(PlayerDashedEvent evt)
    {
        _dashActive = true;
        _dashStoredVelocity = _rb.linearVelocity;
        Invoke(nameof(ForceEndDashDetection), skidDuration);
    }

    private void ForceEndDashDetection()
    {
        if (_dashActive)
        {
            SpawnSkid(_dashStoredVelocity.sqrMagnitude > 0.01f ? _dashStoredVelocity.normalized : Vector2.zero);
            _dashActive = false;
        }
    }

    private void SpawnPuff(float alphaScale)
    {
        if (dustPuffPrefab == null) return;
        var go = Instantiate(dustPuffPrefab, new Vector3(transform.position.x, transform.position.y - 0.25f, 0f), Quaternion.identity);
        ApplyAlphaAndFade(go, alphaScale, Vector2.zero);
    }

    private void SpawnSkid(Vector2 direction)
    {
        var prefab = dashSkidPuffPrefab != null ? dashSkidPuffPrefab : dustPuffPrefab;
        if (prefab == null) return;

        for (int i = 0; i < 6; i++)
        {
            Vector2 jitter = Random.insideUnitCircle * 0.2f;
            Vector3 pos = transform.position + (Vector3)jitter;
            var go = Instantiate(prefab, new Vector3(pos.x, pos.y - 0.25f, 0f), Quaternion.identity);
            ApplyAlphaAndFade(go, 0.75f, -direction * Random.Range(0.5f, 1.2f));
        }
    }

    private void ApplyAlphaAndFade(GameObject go, float alphaScale, Vector2 drift)
    {
        var sr = go.GetComponent<SpriteRenderer>() ?? go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            Color c = dustColor;
            c.a *= alphaScale;
            sr.color = c;
        }

        var fade = go.GetComponent<DustPuffFade>() ?? go.AddComponent<DustPuffFade>();
        fade.Init(sr, 0.35f);
        if (drift.sqrMagnitude > 0.0001f) fade.AddDrift(drift);
    }
}

internal sealed class DustPuffFade : MonoBehaviour
{
    private SpriteRenderer _sr;
    private float _life;
    private float _age;
    private Vector2 _drift;
    private float _startAlpha;

    public void Init(SpriteRenderer sr, float lifetime)
    {
        _sr = sr;
        _life = Mathf.Max(0.05f, lifetime);
        _startAlpha = sr != null ? sr.color.a : 1f;
    }

    public void AddDrift(Vector2 drift) => _drift += drift;

    private void Update()
    {
        _age += Time.deltaTime;
        float t = Mathf.Clamp01(_age / _life);

        transform.position += (Vector3)(_drift * Time.deltaTime);
        transform.localScale *= 1f + Time.deltaTime * 0.8f;

        if (_sr != null)
        {
            var c = _sr.color;
            c.a = Mathf.Lerp(_startAlpha, 0f, t);
            _sr.color = c;
        }

        if (_age >= _life) Destroy(gameObject);
    }
}
