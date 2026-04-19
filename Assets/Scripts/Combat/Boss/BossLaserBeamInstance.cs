using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossLaserBeamInstance : MonoBehaviour
{
    [SerializeField] private SpriteRenderer startCapRenderer;
    [SerializeField] private SpriteRenderer loopStripRenderer;
    [SerializeField] private SpriteRenderer endCapRenderer;

    [SerializeField] private Vector2 beamTipDirectionLocal = new Vector2(-2.2f, -1.3685f);
    [SerializeField] private float parentVisualOffsetDegrees;

    [SerializeField] private Vector2 loopFixedLocalPosition = new Vector2(-0.598f, -0.3761f);
    [SerializeField] private Vector2 startCapLocalPosition;
    [SerializeField] private Vector2 endCapLocalAnchor;

    [HideInInspector] [SerializeField] private float startExtentAlongBeamOverride = -1f;
    [HideInInspector] [SerializeField] private float endCapTipExtentAlongBeamOverride = -1f;
    [HideInInspector] [SerializeField] private float middleOverlapPadding = 0.02f;

    [SerializeField] private bool flipEndCapSpriteX;
    [SerializeField] private bool flipLoopSpriteX;
    [SerializeField] private int sortingOrderOffset = 100;

    private Color _baseColor = Color.white;
    private readonly List<SpriteRenderer> _allRenderers = new();

    private Quaternion _loopLocalRot;
    private float _loopSizeY;

    private void Awake()
    {
        CacheRendererList();
        if (_allRenderers.Count > 0)
            _baseColor = _allRenderers[0].color;

        if (loopStripRenderer)
        {
            _loopLocalRot = loopStripRenderer.transform.localRotation;
            _loopSizeY = loopStripRenderer.size.y;
            if (_loopSizeY < 0.001f && loopStripRenderer.sprite)
                _loopSizeY = Mathf.Max(0.01f, loopStripRenderer.sprite.bounds.size.y);
        }

        if (endCapRenderer) endCapRenderer.flipX = flipEndCapSpriteX;
        if (loopStripRenderer) loopStripRenderer.flipX = flipLoopSpriteX;
    }

    private void CacheRendererList()
    {
        _allRenderers.Clear();
        if (startCapRenderer) _allRenderers.Add(startCapRenderer);
        if (loopStripRenderer) _allRenderers.Add(loopStripRenderer);
        if (endCapRenderer) _allRenderers.Add(endCapRenderer);
    }

    public void CopySortingFrom(SpriteRenderer bossRenderer)
    {
        if (!bossRenderer) return;
        int order = bossRenderer.sortingOrder + sortingOrderOffset;
        foreach (var r in _allRenderers)
        {
            if (!r) continue;
            r.sortingLayerID = bossRenderer.sortingLayerID;
            r.sortingOrder = order;
        }
    }

    public void ApplyVisualTint(Color tint)
    {
        _baseColor = tint;
        foreach (var r in _allRenderers)
        {
            if (r) r.color = tint;
        }
    }

    public void ApplyBeam(Vector2 mouthWorld, float castAngleDegrees, float beamLengthWorld, float z)
    {
        Vector2 dirLocal = NormalizedBeamTipLocal();
        float localAngleDeg = Mathf.Atan2(dirLocal.y, dirLocal.x) * Mathf.Rad2Deg;
        float parentZ = castAngleDegrees - localAngleDeg + parentVisualOffsetDegrees;

        transform.SetPositionAndRotation(
            new Vector3(mouthWorld.x, mouthWorld.y, z),
            Quaternion.Euler(0f, 0f, parentZ));

        if (!startCapRenderer || !loopStripRenderer || !endCapRenderer)
            return;

        Sprite loopSprite = loopStripRenderer.sprite;
        if (!startCapRenderer.sprite || !loopSprite || !endCapRenderer.sprite)
            return;

        endCapRenderer.flipX = flipEndCapSpriteX;
        loopStripRenderer.flipX = flipLoopSpriteX;

        Vector3 beamAxisWorld = transform.TransformDirection(new Vector3(dirLocal.x, dirLocal.y, 0f)).normalized;

        LayoutAlongBeam(loopSprite, Mathf.Max(0f, beamLengthWorld), dirLocal, beamAxisWorld);
    }

    private Vector2 NormalizedBeamTipLocal()
    {
        Vector2 d = beamTipDirectionLocal;
        if (d.sqrMagnitude < 1e-6f)
            d = new Vector2(-2.2f, -1.3685f);
        return d.normalized;
    }

    private void LayoutAlongBeam(Sprite loopSprite, float beamLen, Vector2 dirLocal, Vector3 beamAxisWorld)
    {
        startCapRenderer.enabled = true;
        startCapRenderer.drawMode = SpriteDrawMode.Simple;
        startCapRenderer.transform.localPosition = new Vector3(startCapLocalPosition.x, startCapLocalPosition.y, 0f);
        startCapRenderer.transform.localRotation = Quaternion.identity;
        startCapRenderer.transform.localScale = Vector3.one;

        float startAlongBeam = StartExtentAlongBeam(beamAxisWorld);

        endCapRenderer.drawMode = SpriteDrawMode.Simple;
        endCapRenderer.transform.localPosition = new Vector3(endCapLocalAnchor.x, endCapLocalAnchor.y, 0f);
        endCapRenderer.transform.localRotation = Quaternion.identity;
        endCapRenderer.transform.localScale = Vector3.one;

        Vector3 endPivotWorld = endCapRenderer.transform.position;
        float tipExtent = EndTipExtentAlongBeam(beamAxisWorld, endPivotWorld);
        float spanAlongBeam = Mathf.Max(0f, beamLen - tipExtent);

        Vector2 endLocal = endCapLocalAnchor + dirLocal * spanAlongBeam;
        endCapRenderer.enabled = beamLen > 0.0001f;
        endCapRenderer.transform.localPosition = new Vector3(endLocal.x, endLocal.y, 0f);

        float middle = Mathf.Max(0f, spanAlongBeam - startAlongBeam - middleOverlapPadding);

        loopStripRenderer.enabled = middle > 1e-5f;
        if (!loopStripRenderer.enabled)
            return;

        loopStripRenderer.transform.localPosition = new Vector3(loopFixedLocalPosition.x, loopFixedLocalPosition.y, 0f);
        loopStripRenderer.transform.localRotation = _loopLocalRot;
        loopStripRenderer.transform.localScale = Vector3.one;

        loopStripRenderer.drawMode = SpriteDrawMode.Tiled;
        loopStripRenderer.sprite = loopSprite;

        float loopRightDotBeam = Mathf.Abs(Vector3.Dot(loopStripRenderer.transform.right, beamAxisWorld));
        float tiledLength = loopRightDotBeam > 0.001f ? middle / loopRightDotBeam : middle;

        float h = _loopSizeY > 0.001f ? _loopSizeY : Mathf.Max(0.01f, loopSprite.bounds.size.y);
        loopStripRenderer.size = new Vector2(tiledLength, h);
    }

    private float StartExtentAlongBeam(Vector3 beamAxisWorld)
    {
        if (startExtentAlongBeamOverride >= 0f)
            return startExtentAlongBeamOverride;

        return MaxProjectionAlongAxis(startCapRenderer, transform.position, beamAxisWorld);
    }

    private float EndTipExtentAlongBeam(Vector3 beamAxisWorld, Vector3 endPivotWorld)
    {
        if (endCapTipExtentAlongBeamOverride >= 0f)
            return endCapTipExtentAlongBeamOverride;

        return MaxProjectionAlongAxis(endCapRenderer, endPivotWorld, beamAxisWorld);
    }

    private static float MaxProjectionAlongAxis(SpriteRenderer sr, Vector3 originWorld, Vector3 beamAxisWorld)
    {
        if (!sr)
            return 0f;

        beamAxisWorld.Normalize();
        Bounds b = sr.bounds;
        float maxD = float.MinValue;
        Vector3 c = b.center;
        Vector3 e = b.extents;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c + new Vector3(
                (i & 1) != 0 ? e.x : -e.x,
                (i & 2) != 0 ? e.y : -e.y,
                (i & 4) != 0 ? e.z : -e.z);
            maxD = Mathf.Max(maxD, Vector3.Dot(corner - originWorld, beamAxisWorld));
        }

        return Mathf.Max(0f, maxD);
    }

    public void FadeAndDestroy(float duration)
    {
        StartCoroutine(FadeRoutine(duration));
    }

    private IEnumerator FadeRoutine(float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        float t = 0f;
        var alphas = new float[_allRenderers.Count];
        for (int i = 0; i < _allRenderers.Count; i++)
        {
            var r = _allRenderers[i];
            alphas[i] = r ? r.color.a : _baseColor.a;
        }

        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            for (int i = 0; i < _allRenderers.Count; i++)
            {
                var r = _allRenderers[i];
                if (!r) continue;
                Color c = r.color;
                c.a = Mathf.Lerp(alphas[i], 0f, u);
                r.color = c;
            }
            yield return null;
        }

        Destroy(gameObject);
    }
}
