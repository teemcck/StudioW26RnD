using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SlimeStrikeArcVisual : MonoBehaviour
{
    private LineRenderer _line;
    private float _baseWidth;
    private Color _rgb0;
    private Color _rgb1;

    public static void Spawn(
        Transform followParent,
        Vector2 worldForward,
        float arcRadiusLocal,
        float arcDegrees,
        float strikeDelaySeconds,
        int sortingLayerId,
        int sortingOrder)
    {
        if (followParent == null || arcRadiusLocal <= 0.01f)
            return;

        var go = new GameObject("SlimeStrikeArc");
        go.transform.SetParent(followParent, false);
        go.transform.localPosition = Vector3.zero;
        float ang = Mathf.Atan2(worldForward.y, worldForward.x) * Mathf.Rad2Deg;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, ang);

        var lr = go.AddComponent<LineRenderer>();
        lr.loop = false;
        lr.useWorldSpace = false;
        lr.widthCurve = AnimationCurve.Constant(0f, 1f, 0.055f);
        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null)
            sh = Shader.Find("Sprites/Default");
        if (sh != null)
            lr.sharedMaterial = new Material(sh);

        lr.sortingLayerID = sortingLayerId;
        lr.sortingOrder = sortingOrder;

        int n = 22;
        lr.positionCount = n;
        float half = arcDegrees * 0.5f * Mathf.Deg2Rad;
        for (int i = 0; i < n; i++)
        {
            float t = n > 1 ? i / (float)(n - 1) : 0f;
            float a = Mathf.Lerp(-half, half, t);
            lr.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * arcRadiusLocal);
        }

        var rgb0 = new Color(1f, 0.42f, 0.32f, 1f);
        var rgb1 = new Color(1f, 0.55f, 0.42f, 1f);
        lr.startColor = new Color(rgb0.r, rgb0.g, rgb0.b, 0.01f);
        lr.endColor = new Color(rgb1.r, rgb1.g, rgb1.b, 0.01f);

        var view = go.AddComponent<SlimeStrikeArcVisual>();
        view._line = lr;
        view._baseWidth = 0.055f;
        view._rgb0 = rgb0;
        view._rgb1 = rgb1;
        view.StartCoroutine(view.Run(strikeDelaySeconds));
    }

    private IEnumerator Run(float strikeDelaySeconds)
    {
        strikeDelaySeconds = Mathf.Max(0.02f, strikeDelaySeconds);
        float t = 0f;
        while (t < strikeDelaySeconds)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, t / strikeDelaySeconds);
            float alpha = Mathf.Lerp(0.01f, 0.95f, u);
            ApplyAlpha(alpha);
            float w = Mathf.Lerp(_baseWidth * 0.6f, _baseWidth * 1.12f, u);
            _line.startWidth = w;
            _line.endWidth = w;
            yield return null;
        }

        _line.startColor = new Color(1f, 0.95f, 0.55f, 1f);
        _line.endColor = new Color(1f, 0.45f, 0.25f, 1f);
        _rgb0 = new Color(1f, 0.95f, 0.55f, 1f);
        _rgb1 = new Color(1f, 0.45f, 0.25f, 1f);
        _line.startWidth = _baseWidth * 1.4f;
        _line.endWidth = _baseWidth * 1.4f;
        yield return new WaitForSeconds(0.05f);

        t = 0f;
        float fade = 0.16f;
        while (t < fade)
        {
            t += Time.deltaTime;
            float k = 1f - t / fade;
            ApplyAlpha(0.85f * k);
            yield return null;
        }
        Destroy(gameObject);
    }

    private void ApplyAlpha(float a)
    {
        _line.startColor = new Color(_rgb0.r, _rgb0.g, _rgb0.b, a * 0.88f);
        _line.endColor = new Color(_rgb1.r, _rgb1.g, _rgb1.b, a);
    }
}
