using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Exit-reveal presentation operations. The caller owns the coroutine, camera proxy, and teleporter lifetime.
/// The routines use scaled time and restore authored colors before enabling exit colliders.
/// </summary>
internal static class BossTeleporterReveal
{
    /// <summary>
    /// Moves a camera target with smooth easing over scaled seconds, snapping when duration is effectively zero.
    /// </summary>
    public static IEnumerator PanProxyLerp(Transform proxy, Vector3 from, Vector3 to, float duration)
    {
        if (!proxy)
            yield break;
        if (duration <= 0.0001f)
        {
            proxy.position = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            proxy.position = Vector3.Lerp(from, to, u);
            yield return null;
        }
        proxy.position = to;
    }

    /// <summary>
    /// Pans and fades on independent durations, keeping teleporter collision disabled until both finish.
    /// </summary>
    public static IEnumerator PanAndFadeTeleporterReveal(Transform panProxy, Vector3 fromPos, Vector3 toPos,
        GameObject teleporterGo, float panDuration, float fadeDuration)
    {
        Tilemap[] tilemaps = teleporterGo.GetComponentsInChildren<Tilemap>(true);
        SpriteRenderer[] sprites = teleporterGo.GetComponentsInChildren<SpriteRenderer>(true);
        Collider2D[] cols = teleporterGo.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D c in cols)
            c.enabled = false;

        Color[] tileBase = new Color[tilemaps.Length];
        for (int i = 0; i < tilemaps.Length; i++)
        {
            tileBase[i] = tilemaps[i].color;
            Color tc = tileBase[i];
            tc.a = 0f;
            tilemaps[i].color = tc;
        }

        Color[] spriteBase = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            spriteBase[i] = sprites[i].color;
            Color sc = spriteBase[i];
            sc.a = 0f;
            sprites[i].color = sc;
        }

        float dur = Mathf.Max(0.01f, Mathf.Max(panDuration, fadeDuration));
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float uPan = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, panDuration));
            float uFade = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, fadeDuration));
            float smoothPan = Mathf.SmoothStep(0f, 1f, uPan);
            if (panProxy)
                panProxy.position = Vector3.Lerp(fromPos, toPos, smoothPan);
            for (int i = 0; i < tilemaps.Length; i++)
            {
                Color c = tileBase[i];
                c.a = tileBase[i].a * uFade;
                tilemaps[i].color = c;
            }

            for (int i = 0; i < sprites.Length; i++)
            {
                Color c = spriteBase[i];
                c.a = spriteBase[i].a * uFade;
                sprites[i].color = c;
            }

            yield return null;
        }

        if (panProxy)
            panProxy.position = toPos;
        for (int i = 0; i < tilemaps.Length; i++)
            tilemaps[i].color = tileBase[i];
        for (int i = 0; i < sprites.Length; i++)
            sprites[i].color = spriteBase[i];
        foreach (Collider2D c in cols)
            c.enabled = true;
    }

    /// <summary>
    /// Reveals teleporter tilemaps and sprites without a camera, restoring authored colors before enabling collision.
    /// </summary>
    public static IEnumerator FadeTeleporterInPlace(GameObject teleporterGo, float fadeDuration)
    {
        fadeDuration = Mathf.Max(0.01f, fadeDuration);
        Tilemap[] tilemaps = teleporterGo.GetComponentsInChildren<Tilemap>(true);
        SpriteRenderer[] sprites = teleporterGo.GetComponentsInChildren<SpriteRenderer>(true);
        Collider2D[] cols = teleporterGo.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D c in cols)
            c.enabled = false;

        Color[] tileBase = new Color[tilemaps.Length];
        for (int i = 0; i < tilemaps.Length; i++)
        {
            tileBase[i] = tilemaps[i].color;
            Color tc = tileBase[i];
            tc.a = 0f;
            tilemaps[i].color = tc;
        }

        Color[] spriteBase = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            spriteBase[i] = sprites[i].color;
            Color sc = spriteBase[i];
            sc.a = 0f;
            sprites[i].color = sc;
        }

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float uFade = Mathf.Clamp01(t / fadeDuration);
            for (int i = 0; i < tilemaps.Length; i++)
            {
                Color c = tileBase[i];
                c.a = tileBase[i].a * uFade;
                tilemaps[i].color = c;
            }

            for (int i = 0; i < sprites.Length; i++)
            {
                Color c = spriteBase[i];
                c.a = spriteBase[i].a * uFade;
                sprites[i].color = c;
            }

            yield return null;
        }

        for (int i = 0; i < tilemaps.Length; i++)
            tilemaps[i].color = tileBase[i];
        for (int i = 0; i < sprites.Length; i++)
            sprites[i].color = spriteBase[i];
        foreach (Collider2D c in cols)
            c.enabled = true;
    }
}
