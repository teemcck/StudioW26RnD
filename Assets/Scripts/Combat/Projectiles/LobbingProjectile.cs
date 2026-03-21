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
    [SerializeField] private float telegraphPulseSpeed = 5f;

    private bool _arcActive;
    private float _flightElapsed;
    private float _flightDuration;
    private Vector2 _arcStart;
    private Vector2 _landingPlanar;
    private float _vx;
    private float _vy;
    private float _g;
    private GameObject _landingTelegraphGo;

    protected override void Awake()
    {
        base.Awake();
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

        if (t >= _flightDuration)
        {
            _arcActive = false;
            Explode(_landingPlanar);
        }
    }

    protected override void OnAboutToExplode(Vector2 center)
    {
        _arcActive = false;
        DestroyLandingTelegraph();
    }

    public void FireBallistic(Vector2 start, Vector2 targetWorld, float groundLobSpeed)
    {
        RestrictColliderToPlayerOnly();

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

        Sprite sprite = landingTelegraphSprite ? landingTelegraphSprite : GetComponent<SpriteRenderer>()?.sprite;
        if (!sprite || flightDuration <= 0.01f)
            return;

        float ringScale = ExplosionGroundRingWorldScale;

        var go = new GameObject("LobLandingTelegraph");
        go.transform.position = new Vector3(worldCenter.x, worldCenter.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = landingTelegraphSortingOrder;
        if (ExplosionGroundSortingLayerId != 0)
            sr.sortingLayerID = ExplosionGroundSortingLayerId;

        _landingTelegraphGo = go;

        var fx = go.AddComponent<LobLandingTelegraph>();
        fx.StartRun(sr, landingTelegraphColor, ringScale, flightDuration, telegraphPulseSpeed);
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
            float pulse = 0.82f + 0.18f * Mathf.Sin(t * pulseSpeed);
            var c = baseColor;
            c.a = baseColor.a * pulse;
            sr.color = c;
            transform.localScale = Vector3.one * (worldScale * pulse);
            yield return null;
        }

        Destroy(gameObject);
    }
}
