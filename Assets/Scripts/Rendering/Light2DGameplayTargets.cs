using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Pins local <see cref="Light2D"/> to explicit sorting layers. Default Unity behavior is “all layers”;
/// we set the same list explicitly so it stays predictable when layers are added later.
/// Includes <b>Map</b> so floor tilemaps pick up light; tune falloff/intensity on each light to avoid a milky wash.
/// </summary>
public static class Light2DGameplayTargets
{
    /// <summary> All project sorting layers (characters, floor Map tiles, VFX, etc.). </summary>
    public static void ApplyAllSortingLayers(Light2D light)
    {
        if (light == null)
            return;

        var ids = new List<int>(SortingLayer.layers.Length);
        foreach (var sl in SortingLayer.layers)
            ids.Add(sl.id);

        if (ids.Count > 0)
            light.targetSortingLayers = ids.ToArray();
    }

    /// <summary> Backwards-compatible name used by gameplay accent lights. </summary>
    public static void ApplyLocalAccentSortingLayers(Light2D light) => ApplyAllSortingLayers(light);

    /// <summary>
    /// Character / VFX accent lights: omits the <b>Map</b> sorting layer so floor and map-sized decoration
    /// are not washed by additive blobs; pillars and actors on other layers still read correctly.
    /// </summary>
    public static void ApplyLocalAccentWithoutMapLayer(Light2D light)
    {
        if (light == null)
            return;

        var ids = new List<int>(SortingLayer.layers.Length);
        foreach (var sl in SortingLayer.layers)
        {
            if (sl.name == "Map")
                continue;
            ids.Add(sl.id);
        }

        if (ids.Count > 0)
            light.targetSortingLayers = ids.ToArray();
    }

    /// <summary>
    /// Enables cast shadows on gameplay-created <see cref="Light2D"/> (additive accents, transients, etc.).
    /// Slightly softer than a typical global fill so several overlapping lights stay readable.
    /// </summary>
    public static void EnableAccentLightShadows(Light2D light, float shadowIntensity = 0.5f, float shadowSoftness = 0.28f)
    {
        if (light == null)
            return;

        light.shadowsEnabled = true;
        light.shadowIntensity = Mathf.Clamp01(shadowIntensity);
        light.shadowSoftness = shadowSoftness;
        light.shadowSoftnessFalloffIntensity = 0.45f;
    }
}
