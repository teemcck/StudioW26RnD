using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Short-lived point <see cref="Light2D"/> that eases out and self-destructs. Used for impacts and bursts without cluttering the scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class TransientPointLight2D : MonoBehaviour
{
    /// <summary> Default URP 2D Renderer: 0 = Multiply, 1 = Additive. </summary>
    public const int AdditiveBlendStyleIndex = 1;

    Light2D _light;
    float _peakIntensity;
    float _duration;
    float _elapsed;
    bool _useUnscaledTime;

    /// <summary> Creates a world-space transient light and returns the component (on a new GameObject). </summary>
    public static TransientPointLight2D Spawn(
        Vector3 worldPosition,
        Color color,
        float peakIntensity,
        float innerRadius,
        float outerRadius,
        float duration,
        bool useUnscaledTime = true)
    {
        var go = new GameObject("TransientPointLight2D");
        go.transform.position = worldPosition;

        var light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.blendStyleIndex = AdditiveBlendStyleIndex;
        light.overlapOperation = Light2D.OverlapOperation.Additive;
        light.color = color;
        light.intensity = peakIntensity;
        light.falloffIntensity = 0.78f;
        light.pointLightInnerRadius = innerRadius;
        light.pointLightOuterRadius = outerRadius;
        Light2DGameplayTargets.ApplyLocalAccentWithoutMapLayer(light);
        Light2DGameplayTargets.EnableAccentLightShadows(light, shadowIntensity: 0.42f, shadowSoftness: 0.24f);

        var t = go.AddComponent<TransientPointLight2D>();
        t._light = light;
        t._peakIntensity = peakIntensity;
        t._duration = Mathf.Max(0.02f, duration);
        t._useUnscaledTime = useUnscaledTime;
        return t;
    }

    void Update()
    {
        if (_light == null)
        {
            Destroy(gameObject);
            return;
        }

        float dt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _elapsed += dt;
        float u = Mathf.Clamp01(_elapsed / _duration);
        // Smooth ease-out so the pop reads as a spark, not a lingering lamp.
        float fade = (1f - u) * (1f - u);
        _light.intensity = _peakIntensity * fade;

        if (_elapsed >= _duration)
            Destroy(gameObject);
    }
}
