using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Subtle player-facing 2D lights: a soft follow light for readability, and a brief dash burst that matches <see cref="GameColors.SafeDash"/>.
/// Uses the renderer's <b>Additive</b> light blend style so lights brighten; blend style 0 (Multiply) would darken at low intensity.
/// </summary>
public sealed class PlayerVisualLights2D : MonoBehaviour
{
    /// <summary> Default URP 2D Renderer: 0 = Multiply, 1 = Additive (see PC_Renderer2D). </summary>
    const int AdditiveBlendStyleIndex = 1;

    [Header("Presence (always on)")]
    [SerializeField] private bool enablePresenceLight = true;
    [SerializeField] [Range(0.02f, 0.45f)] private float presenceIntensity = 0.055f;
    [SerializeField] private float presenceOuterRadius = 1.05f;
    [SerializeField] private float presenceInnerRadius = 0.08f;
    [SerializeField] private Color presenceColor = new(0.78f, 0.9f, 1f, 1f);

    [Header("Dash burst")]
    [SerializeField] private bool enableDashBurst = true;
    [SerializeField] [Range(0.05f, 0.65f)] private float dashPeakIntensity = 0.22f;
    [SerializeField] private float dashOuterRadius = 0.95f;
    [SerializeField] private float dashInnerRadius = 0.05f;
    [SerializeField] private float dashDuration = 0.1f;

    private Light2D _presence;
    private IEventBinding<PlayerDashedEvent> _dashBinding;

    void Awake()
    {
        if (enablePresenceLight)
            CreatePresenceLight();
    }

    void OnEnable()
    {
        if (enableDashBurst)
            _dashBinding = EventBus<PlayerDashedEvent>.Register(OnPlayerDashed);
    }

    void OnDisable()
    {
        if (_dashBinding != null)
            EventBus<PlayerDashedEvent>.Unsubscribe(_dashBinding);
        _dashBinding = null;
    }

    void OnDestroy()
    {
        if (_presence != null)
            Destroy(_presence.gameObject);
    }

    void CreatePresenceLight()
    {
        var go = new GameObject("PlayerPresenceLight2D");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.15f, 0f);

        var light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.blendStyleIndex = AdditiveBlendStyleIndex;
        light.overlapOperation = Light2D.OverlapOperation.Additive;
        light.color = presenceColor;
        light.intensity = presenceIntensity;
        light.falloffIntensity = 0.9f;
        light.pointLightInnerRadius = presenceInnerRadius;
        light.pointLightOuterRadius = presenceOuterRadius;
        Light2DGameplayTargets.ApplyLocalAccentWithoutMapLayer(light);
        Light2DGameplayTargets.EnableAccentLightShadows(light, shadowIntensity: 0.48f, shadowSoftness: 0.26f);
        _presence = light;
    }

    void OnPlayerDashed(PlayerDashedEvent _)
    {
        if (!enableDashBurst)
            return;

        Color c = Color.Lerp(GameColors.SafeDash, Color.white, 0.35f);
        TransientPointLight2D.Spawn(
            transform.position + new Vector3(0f, 0.1f, 0f),
            c,
            dashPeakIntensity,
            dashInnerRadius,
            dashOuterRadius,
            dashDuration,
            useUnscaledTime: false);
    }
}
