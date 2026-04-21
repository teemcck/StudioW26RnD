using UnityEngine;

public sealed class CardFloatAnimation : MonoBehaviour
{
    [SerializeField] private float scaleAmplitude = 0.025f;
    [SerializeField] private float rotationAmplitude = 1.6f;
    [SerializeField] private float scaleHz = 0.6f;
    [SerializeField] private float rotationHz = 0.43f;
    [SerializeField] private bool useUnscaledTime = true;

    private RectTransform _rt;
    private Vector3 _baseScale;
    private Quaternion _baseRotation;
    private float _phase;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _baseScale = _rt.localScale;
        _baseRotation = _rt.localRotation;
        _phase = Random.value * Mathf.PI * 2f;
    }

    private void OnEnable()
    {
        _baseScale = _rt.localScale;
        _baseRotation = _rt.localRotation;
    }

    private void OnDisable()
    {
        if (_rt == null) return;
        _rt.localScale = _baseScale;
        _rt.localRotation = _baseRotation;
    }

    private void Update()
    {
        if (_rt == null) return;

        float time = useUnscaledTime ? Time.unscaledTime : Time.time;
        float scaleWave = Mathf.Sin(_phase + time * scaleHz * Mathf.PI * 2f);
        float rotWave = Mathf.Sin(_phase * 0.73f + time * rotationHz * Mathf.PI * 2f);

        _rt.localScale = _baseScale * (1f + scaleAmplitude * scaleWave);
        _rt.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, rotationAmplitude * rotWave);
    }
}
