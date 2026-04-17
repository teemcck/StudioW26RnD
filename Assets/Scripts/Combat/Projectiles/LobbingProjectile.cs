using System.Collections;
using UnityEngine;

public class LobbingProjectile : AOEProjectile
{
    [Header("Ballistics")]
    [SerializeField] private float gravityScale = 0.24f;
    [SerializeField] private float minFlightTime = 0.55f;
    [SerializeField] private float maxFlightTime = 1.65f;

    [Header("Landing telegraph")]
    [SerializeField] private bool showLandingTelegraph = true;
    [SerializeField] private Sprite landingTelegraphSprite;
    [SerializeField] private Color landingTelegraphColor = new Color(1f, 0.4f, 0.12f, 0.38f);
    [SerializeField] private int landingTelegraphSortingOrder = 42;
    [SerializeField] private int landingTelegraphSortingLayerId;
    [SerializeField] private float telegraphPulseSpeed = 5f;

    [Header("Impact telegraph polish")]
    [SerializeField] private bool showImpactBurst = true;
    [SerializeField] private Sprite impactBurstSprite;
    [SerializeField] private Color impactBurstColor = new Color(1f, 0.72f, 0.25f, 0.92f);
    [SerializeField] private float impactBurstDuration = 0.2f;
    [SerializeField] private float impactBurstStartScaleMultiplier = 0.55f;
    [SerializeField] private float impactBurstEndScaleMultiplier = 1.85f;
    [SerializeField] private int impactBurstSortingOrder = 55;
    [SerializeField] private int impactBurstSortingLayerId;

    [Header("Visual Animation")]
    [SerializeField] private SpriteRenderer projectileSpriteRenderer;
    [SerializeField] private Sprite[] flightAnimationSprites;
    [SerializeField] private float flightAnimationFps = 10f;

    private bool _arcActive;
    private float _flightElapsed;
    private float _flightDuration;
    private Vector2 _arcStart;
    private Vector2 _landingPlanar;
    private float _vx;
    private float _vy;
    private float _g;
    private GameObject _landingTelegraphGo;
    private Collider2D _hitCollider;
    private int _animFrame;
    private float _animTimer;

    public Sprite LandingTelegraphSpriteResolved => landingTelegraphSprite ? landingTelegraphSprite : GetComponent<SpriteRenderer>()?.sprite;
    public Color LandingTelegraphColor => landingTelegraphColor;
    public int LandingTelegraphSortingOrder => landingTelegraphSortingOrder;
    public int LandingTelegraphSortingLayerId => landingTelegraphSortingLayerId;
    public float LandingTelegraphPulseSpeed => telegraphPulseSpeed;
    public float LandingTelegraphScale => ExplosionGroundRingWorldScale;

    protected override void Awake()
    {
        base.Awake();
        if (!projectileSpriteRenderer)
            projectileSpriteRenderer = GetComponent<SpriteRenderer>();
        _hitCollider = GetComponent<Collider2D>();
        RestrictColliderToPlayerOnly();
    }

    private void RestrictColliderToPlayerOnly()
    {
        var c = GetComponent<Collider2D>();
        if (!c) return;
        LayerMask playerOnly = LayerMask.GetMask("Player");
        c.includeLayers = playerOnly;
        c.callbackLayers = playerOnly;
    }

    protected override void FixedUpdate()
    {
        if (HasExploded || !_arcActive)
            return;

        float dt = Time.fixedDeltaTime;
        _flightElapsed += dt;
        float t = _flightElapsed;

        Vector2 pos = new Vector2(
            _arcStart.x + _vx * t,
            _arcStart.y + _vy * t + 0.5f * _g * t * t);

        RbProjectile.MovePosition(pos);

        Vector2 vel = new Vector2(_vx, _vy + _g * t);
        if (vel.sqrMagnitude > 0.0001f)
            SetFlightDirection(vel);

        UpdateFlightSpriteAnimation(dt);

        if (t >= _flightDuration)
        {
            _arcActive = false;
            Explode(_landingPlanar);
        }
    }

    protected override void OnAboutToExplode(Vector2 center)
    {
        _arcActive = false;
        if (_hitCollider)
            _hitCollider.enabled = true;
        DestroyLandingTelegraph();
        SpawnImpactBurst(center);
    }

    public void FireBallistic(Vector2 start, Vector2 targetWorld, float groundLobSpeed)
    {
        RestrictColliderToPlayerOnly();
        if (_hitCollider)
            _hitCollider.enabled = false;

        _arcStart = start;
        _landingPlanar = targetWorld;
        transform.position = start;

        Vector2 delta = targetWorld - start;
        float planarDist = delta.magnitude;
        if (planarDist < 0.02f)
        {
            delta = Vector2.right * 0.02f;
            planarDist = 0.02f;
        }

        float speed = Mathf.Max(0.15f, groundLobSpeed);
        float T = planarDist / speed;
        T = Mathf.Clamp(T, minFlightTime, maxFlightTime);
        _flightDuration = T;
        _flightElapsed = 0f;
        _animFrame = 0;
        _animTimer = 0f;
        ApplyFrame(0);

        RbProjectile.bodyType = RigidbodyType2D.Kinematic;
        RbProjectile.gravityScale = 0f;
        RbProjectile.linearVelocity = Vector2.zero;

        _g = Physics2D.gravity.y * gravityScale;
        _vx = delta.x / T;
        _vy = (delta.y - 0.5f * _g * T * T) / T;

        SetFlightDirection(new Vector2(_vx, _vy));
        _arcActive = true;
        ScheduleLifetime();

        if (showLandingTelegraph)
            SpawnLandingTelegraph(targetWorld, T);
    }

    private void UpdateFlightSpriteAnimation(float dt)
    {
        if (!projectileSpriteRenderer) return;
        if (flightAnimationSprites == null || flightAnimationSprites.Length < 2) return;

        float fps = Mathf.Max(1f, flightAnimationFps);
        _animTimer += dt;
        float frameDuration = 1f / fps;
        while (_animTimer >= frameDuration)
        {
            _animTimer -= frameDuration;
            _animFrame = (_animFrame + 1) % flightAnimationSprites.Length;
            ApplyFrame(_animFrame);
        }
    }

    private void ApplyFrame(int index)
    {
        if (!projectileSpriteRenderer) return;
        if (flightAnimationSprites == null || flightAnimationSprites.Length == 0) return;
        index = Mathf.Clamp(index, 0, flightAnimationSprites.Length - 1);
        if (flightAnimationSprites[index])
            projectileSpriteRenderer.sprite = flightAnimationSprites[index];
    }

    private void DestroyLandingTelegraph()
    {
        if (_landingTelegraphGo)
        {
            Destroy(_landingTelegraphGo);
            _landingTelegraphGo = null;
        }
    }

    private void SpawnLandingTelegraph(Vector2 worldCenter, float flightDuration)
    {
        DestroyLandingTelegraph();

        Sprite sprite = LandingTelegraphSpriteResolved;
        if (!sprite || flightDuration <= 0.01f)
            return;

        float ringScale = LandingTelegraphScale;

        var go = new GameObject("LobLandingTelegraph");
        go.transform.position = new Vector3(worldCenter.x, worldCenter.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = landingTelegraphSortingOrder;
        if (LandingTelegraphSortingLayerId != 0)
            sr.sortingLayerID = LandingTelegraphSortingLayerId;

        _landingTelegraphGo = go;

        var fx = go.AddComponent<LobLandingTelegraph>();
        fx.StartRun(sr, landingTelegraphColor, ringScale, flightDuration, telegraphPulseSpeed);
    }

    private void SpawnImpactBurst(Vector2 center)
    {
        if (!showImpactBurst) return;

        Sprite sprite = impactBurstSprite ? impactBurstSprite : LandingTelegraphSpriteResolved;
        if (!sprite) return;

        float baseScale = Mathf.Max(0.05f, LandingTelegraphScale);
        float startScale = baseScale * Mathf.Max(0.05f, impactBurstStartScaleMultiplier);
        float endScale = baseScale * Mathf.Max(impactBurstStartScaleMultiplier + 0.05f, impactBurstEndScaleMultiplier);
        float duration = Mathf.Max(0.05f, impactBurstDuration);

        var go = new GameObject("LobImpactBurst");
        go.transform.position = new Vector3(center.x, center.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = impactBurstColor;
        sr.sortingOrder = impactBurstSortingOrder;
        if (impactBurstSortingLayerId != 0)
            sr.sortingLayerID = impactBurstSortingLayerId;
        else if (LandingTelegraphSortingLayerId != 0)
            sr.sortingLayerID = LandingTelegraphSortingLayerId;
        go.transform.localScale = Vector3.one * startScale;

        go.AddComponent<LobImpactBurstFx>().StartRun(sr, impactBurstColor, startScale, endScale, duration);
    }

    protected override Vector2 ResolveExplosionPosition(Collider2D other)
    {
        if (other == null) return transform.position;
        return other.ClosestPoint(transform.position);
    }

    protected override Vector2 GetExpireExplosionPosition() => transform.position;
}

public sealed class LobLandingTelegraph : MonoBehaviour
{
    public void StartRun(SpriteRenderer sr, Color baseColor, float worldScale, float duration, float pulseSpeed)
    {
        StartCoroutine(Run(sr, baseColor, worldScale, duration, pulseSpeed));
    }

    private IEnumerator Run(SpriteRenderer sr, Color baseColor, float worldScale, float duration, float pulseSpeed)
    {
        float t = 0f;
        while (t < duration && sr)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / duration);
            float endRamp = progress >= 0.8f ? Mathf.InverseLerp(0.8f, 1f, progress) : 0f;
            float speed = Mathf.Lerp(pulseSpeed, pulseSpeed * 2.3f, endRamp);
            float pulse = 0.82f + 0.18f * Mathf.Sin(t * speed);
            var c = baseColor;
            c.a = baseColor.a * pulse * Mathf.Lerp(1f, 1.35f, endRamp);
            sr.color = c;
            float scale = worldScale * pulse * Mathf.Lerp(1f, 1.12f, endRamp);
            transform.localScale = Vector3.one * scale;
            yield return null;
        }

        Destroy(gameObject);
    }
}

public sealed class LobImpactBurstFx : MonoBehaviour
{
    public void StartRun(SpriteRenderer sr, Color color, float startScale, float endScale, float duration)
    {
        StartCoroutine(Run(sr, color, startScale, endScale, duration));
    }

    private IEnumerator Run(SpriteRenderer sr, Color color, float startScale, float endScale, float duration)
    {
        float t = 0f;
        while (t < duration && sr)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            Color c = color;
            c.a = Mathf.Lerp(color.a, 0f, u);
            sr.color = c;
            float eased = 1f - Mathf.Pow(1f - u, 3f);
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, eased);
            yield return null;
        }

        Destroy(gameObject);
    }
}
