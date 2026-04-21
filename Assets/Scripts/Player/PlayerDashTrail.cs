using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class PlayerDashTrail : MonoBehaviour
{
    [SerializeField] private SpriteRenderer source;
    [SerializeField] private int ghostCount = 4;
    [SerializeField] private float ghostInterval = 0.03f;
    [SerializeField] private float ghostLifetime = 0.28f;
    [SerializeField] private float startAlpha = 0.55f;
    [SerializeField] private Color tint = default;

    private IEventBinding<PlayerDashedEvent> _binding;

    private void Awake()
    {
        if (source == null) source = GetComponent<SpriteRenderer>();
        if (tint == default) tint = GameColors.SafeDash;
    }

    private void OnEnable()
    {
        _binding = EventBus<PlayerDashedEvent>.Register(_ => StartCoroutine(SpawnGhosts()));
    }

    private void OnDisable()
    {
        if (_binding != null) EventBus<PlayerDashedEvent>.Unsubscribe(_binding);
        _binding = null;
    }

    private IEnumerator SpawnGhosts()
    {
        for (int i = 0; i < ghostCount; i++)
        {
            SpawnOne();
            yield return new WaitForSeconds(ghostInterval);
        }
    }

    private void SpawnOne()
    {
        if (source == null || source.sprite == null) return;

        var go = new GameObject("DashGhost");
        go.transform.position = source.transform.position;
        go.transform.rotation = source.transform.rotation;
        go.transform.localScale = source.transform.lossyScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = source.sprite;
        sr.flipX = source.flipX;
        sr.flipY = source.flipY;
        sr.sortingLayerID = source.sortingLayerID;
        sr.sortingOrder = source.sortingOrder - 1;

        Color c = tint;
        c.a = startAlpha;
        sr.color = c;

        go.AddComponent<DashGhostFade>().Init(sr, ghostLifetime, startAlpha);
    }
}

internal sealed class DashGhostFade : MonoBehaviour
{
    private SpriteRenderer _sr;
    private float _life;
    private float _age;
    private float _startAlpha;

    public void Init(SpriteRenderer sr, float lifetime, float startAlpha)
    {
        _sr = sr;
        _life = Mathf.Max(0.02f, lifetime);
        _startAlpha = startAlpha;
    }

    private void Update()
    {
        _age += Time.deltaTime;
        if (_sr != null)
        {
            float t = Mathf.Clamp01(_age / _life);
            var c = _sr.color;
            c.a = Mathf.Lerp(_startAlpha, 0f, t);
            _sr.color = c;
        }
        if (_age >= _life) Destroy(gameObject);
    }
}
