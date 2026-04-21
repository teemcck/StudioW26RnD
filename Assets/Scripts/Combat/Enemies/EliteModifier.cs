using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyBase))]
[DefaultExecutionOrder(20)]
public sealed class EliteModifier : MonoBehaviour
{
    [SerializeField] private float scaleMultiplier = 1.2f;
    [SerializeField] private float healthMultiplier = 2.2f;
    [SerializeField] private float damageMultiplier = 2.5f;
    [SerializeField] private float shieldReserve = 18f;
    [Tooltip("If the elite takes no damage for this long, shield begins refilling.")]
    [SerializeField] private float shieldRegenAfterNoHitsSeconds = 2f;
    [Tooltip("Seconds to refill shield from empty to full once regen starts (smooth bar + outline).")]
    [SerializeField] private float shieldRegenFillDuration = 1.15f;

    [Header("Body look (sprite tint — matches elite UI accent)")]
    [Tooltip("How strongly the warm gold tint is applied (one-shot; does not stack per frame).")]
    [SerializeField] [Range(0f, 0.35f)] private float bodyTintBlend = 0.18f;
    [Tooltip("Warm multiplier before the accent nudge: slightly lifts red/green, cools blue for an amber read.")]
    [SerializeField] private Color bodyTintMultiplier = new Color(1.1f, 1.04f, 0.88f, 1f);
    [Tooltip("How much to blend toward GameColors.EliteAccent after the multiply (keeps cohesion with shield frame).")]
    [SerializeField] [Range(0f, 0.2f)] private float eliteAccentMix = 0.085f;

    private EnemyBase _enemy;
    private float _currentShield;
    private float _lastHitTime = -999f;
    private readonly List<SpriteRenderer> _bodySprites = new List<SpriteRenderer>(8);

    public bool IsElite => true;
    public float ShieldNormalized => shieldReserve > 0f ? Mathf.Clamp01(_currentShield / shieldReserve) : 0f;
    public bool HasShield => _currentShield > 0f;
    public float OutgoingDamageMultiplier => damageMultiplier;

    /// <summary>True while shield is refilling after the idle delay (drives UI pulse on the yellow frame).</summary>
    public bool IsShieldRegenerating =>
        shieldReserve > 0.001f &&
        _currentShield < shieldReserve - 0.001f &&
        Time.time - _lastHitTime >= Mathf.Max(0.05f, shieldRegenAfterNoHitsSeconds);

    private void Awake()
    {
        _enemy = GetComponent<EnemyBase>();
    }

    private void Start()
    {
        _currentShield = shieldReserve;

        if (_enemy != null)
        {
            _enemy.ApplyRuntimeScaling(healthMultiplier, scaleMultiplier, damageMultiplier);
            _enemy.NotifyRuntimeScalingApplied();
        }

        if (TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.simulated = true;
            rb.WakeUp();
        }

        Physics2D.SyncTransforms();

        CacheBodySprites();
        ApplyStaticBodyTint();

        if (TryGetComponent<EnemyWorldVisuals>(out var visuals))
            visuals.NotifyEliteAttached();

        AudioManager.Instance?.PlayEliteSpawn();
    }

    private void Update()
    {
        TickShieldRegenFromOutOfCombat();
    }

    /// <summary>Call when this elite takes any hit (shield or HP). Resets shield regen timer.</summary>
    public void NotifyHitReceived()
    {
        _lastHitTime = Time.time;
    }

    private void TickShieldRegenFromOutOfCombat()
    {
        if (shieldReserve <= 0f)
            return;
        if (_currentShield >= shieldReserve - 0.001f)
            return;
        float delay = Mathf.Max(0.05f, shieldRegenAfterNoHitsSeconds);
        if (Time.time - _lastHitTime < delay)
            return;

        _currentShield = shieldReserve;
    }

    private void CacheBodySprites()
    {
        _bodySprites.Clear();
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null || sr.sprite == null)
                continue;
            if (ShouldExcludeFromEliteBody(sr))
                continue;
            _bodySprites.Add(sr);
        }
    }

    /// <summary>
    /// One-shot warm nudge so we do not stack tint every frame (which would blow out colors).
    /// </summary>
    private void ApplyStaticBodyTint()
    {
        float t = Mathf.Clamp01(bodyTintBlend);
        if (t <= 0f)
            return;

        float accent = Mathf.Clamp01(eliteAccentMix);
        Color elite = GameColors.EliteAccent;

        for (int i = 0; i < _bodySprites.Count; i++)
        {
            var sr = _bodySprites[i];
            if (sr == null)
                continue;

            Color c = sr.color;
            Color multiplied = new Color(
                Mathf.Min(1.45f, c.r * bodyTintMultiplier.r),
                Mathf.Min(1.45f, c.g * bodyTintMultiplier.g),
                Mathf.Min(1.45f, c.b * bodyTintMultiplier.b),
                c.a);
            Color towardUi = Color.Lerp(multiplied, elite, accent);
            towardUi.a = c.a;
            sr.color = Color.Lerp(c, towardUi, t);
        }
    }

    private static bool ShouldExcludeFromEliteBody(SpriteRenderer sr)
    {
        string n = sr.gameObject.name;
        if (n.Contains("WorldVisuals") || n.Contains("HealthBar") || n.StartsWith("EliteGlow"))
            return true;
        string lower = n.ToLowerInvariant();
        return lower.Contains("vfx") || lower.Contains("fx") || lower.Contains("slash") || lower.Contains("muzzle");
    }

    public float AbsorbDamage(float incoming)
    {
        if (incoming <= 0f || _currentShield <= 0f)
            return incoming;

        float absorbed = Mathf.Min(_currentShield, incoming);
        _currentShield -= absorbed;
        return incoming - absorbed;
    }
}
