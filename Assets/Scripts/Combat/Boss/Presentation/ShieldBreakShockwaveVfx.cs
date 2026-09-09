using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Short-lived shader shockwave spawned when a boss shield breaks.
/// Instances share geometry and material while keeping wave progress in a per-renderer property block.
/// </summary>
public sealed class ShieldBreakShockwaveVfx : MonoBehaviour
{
    private static Material _sharedMaterial;
    private static Mesh _quadMesh;

    private MaterialPropertyBlock _mpb;
    private MeshRenderer _renderer;

    private static readonly int IdWave = Shader.PropertyToID("_WaveProgress");
    private const float Duration = 0.58f;

    private float _t;

    /// <summary>
    /// Lazily creates the shared shockwave material; an unavailable shader suppresses spawning.
    /// </summary>
    private static Material SharedMaterial
    {
        get
        {
            if (_sharedMaterial == null)
            {
                Shader s = Shader.Find("Boss/StunShockwaveDistort");
                if (s != null)
                    _sharedMaterial = new Material(s);
            }
            return _sharedMaterial;
        }
    }

    /// <summary>
    /// Builds one reusable centered quad for all shield-break shockwave instances.
    /// </summary>
    private static Mesh QuadMesh()
    {
        if (_quadMesh != null)
            return _quadMesh;
        _quadMesh = new Mesh { name = "ShieldBreakShockwaveQuad" };
        _quadMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };
        _quadMesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        _quadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        _quadMesh.RecalculateNormals();
        _quadMesh.RecalculateBounds();
        return _quadMesh;
    }

    /// <summary>
    /// Attaches a shockwave to the boss and sorts it above the supplied sprite without cloning the shared material.
    /// </summary>
    public static void Spawn(Transform parent, SpriteRenderer sortRef)
    {
        if (SharedMaterial == null)
            return;

        GameObject go = new GameObject("ShieldBreakShockwave");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.48f, -0.06f);
        go.transform.localScale = new Vector3(40f, 40f, 1f);

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = QuadMesh();

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = SharedMaterial;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        if (sortRef)
        {
            mr.sortingLayerID = sortRef.sortingLayerID;
            mr.sortingOrder = sortRef.sortingOrder + 28;
        }

        ShieldBreakShockwaveVfx v = go.AddComponent<ShieldBreakShockwaveVfx>();
        v._renderer = mr;
        v._mpb = new MaterialPropertyBlock();
        v._mpb.SetFloat(IdWave, 0f);
        mr.SetPropertyBlock(v._mpb);
    }

    /// <summary>
    /// Advances distortion on unscaled time so the shockwave stays animated during hitstop, then destroys it.
    /// </summary>
    private void Update()
    {
        if (_renderer == null)
        {
            Destroy(gameObject);
            return;
        }

        _t += Time.unscaledDeltaTime;
        float u = Duration > 1e-4f ? Mathf.Clamp01(_t / Duration) : 1f;
        float wave = Mathf.SmoothStep(0f, 1.18f, Mathf.Pow(u, 0.38f));
        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(IdWave, wave);
        _renderer.SetPropertyBlock(_mpb);

        if (u >= 1f)
            Destroy(gameObject);
    }
}
