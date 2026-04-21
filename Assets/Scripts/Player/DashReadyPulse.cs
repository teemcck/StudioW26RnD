using UnityEngine;

public sealed class DashReadyPulse : MonoBehaviour
{
    [SerializeField] private SpriteRenderer target;
    [SerializeField] private float pulseAmplitude = 0.06f;
    [SerializeField] private float pulseHz = 1.1f;
    [SerializeField] private Color readyTint = default;

    private PlayerDashController _dash;
    private Vector3 _baseScale;
    private Color _baseColor;
    private bool _wasReady;

    private void Awake()
    {
        _dash = GetComponent<PlayerDashController>();
        if (target == null) target = GetComponentInChildren<SpriteRenderer>();
        if (target != null)
        {
            _baseScale = target.transform.localScale;
            _baseColor = target.color;
        }
        if (readyTint == default) readyTint = Color.Lerp(Color.white, GameColors.SafeDash, 0.2f);
    }

    private void Update()
    {
        if (_dash == null || target == null) return;

        bool ready = Time.time >= _dash.NextDashTime;
        if (!ready)
        {
            if (_wasReady)
            {
                target.transform.localScale = _baseScale;
                target.color = _baseColor;
            }
            _wasReady = false;
            return;
        }

        _wasReady = true;
        float wave = Mathf.Sin(Time.time * pulseHz * Mathf.PI * 2f);
        float scale = 1f + wave * pulseAmplitude * 0.5f;
        target.transform.localScale = _baseScale * scale;
        target.color = Color.Lerp(_baseColor, readyTint, (wave * 0.5f + 0.5f) * 0.35f);
    }
}
