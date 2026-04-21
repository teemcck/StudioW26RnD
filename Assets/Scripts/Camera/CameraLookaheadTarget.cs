using UnityEngine;

[DisallowMultipleComponent]
public sealed class CameraLookaheadTarget : MonoBehaviour
{
    [SerializeField] private float lookaheadDistance = 1.6f;
    [SerializeField] private float smoothTime = 0.22f;
    [SerializeField] private float deadzoneSpeed = 0.2f;
    [SerializeField] private string anchorChildName = "FollowAnchor";

    private Transform _anchor;
    private Rigidbody2D _rb;
    private Vector2 _currentOffset;
    private Vector2 _offsetVelocity;
    private Vector2 _smoothedVelocity;

    public Transform Anchor => _anchor;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();

        var existing = transform.Find(anchorChildName);
        if (existing != null)
        {
            _anchor = existing;
        }
        else
        {
            var go = new GameObject(anchorChildName);
            go.transform.SetParent(transform, worldPositionStays: false);
            _anchor = go.transform;
        }

        _anchor.localPosition = Vector3.zero;
    }

    private void LateUpdate()
    {
        Vector2 rawVel = _rb != null ? _rb.linearVelocity : Vector2.zero;
        _smoothedVelocity = Vector2.Lerp(_smoothedVelocity, rawVel, Mathf.Clamp01(Time.deltaTime * 6f));

        Vector2 targetOffset;
        if (_smoothedVelocity.magnitude < deadzoneSpeed)
        {
            targetOffset = Vector2.zero;
        }
        else
        {
            Vector2 dir = _smoothedVelocity.normalized;
            float mag = Mathf.Clamp(_smoothedVelocity.magnitude * 0.2f, 0f, lookaheadDistance);
            targetOffset = dir * mag;
        }

        _currentOffset = Vector2.SmoothDamp(_currentOffset, targetOffset, ref _offsetVelocity, smoothTime);
        _anchor.localPosition = new Vector3(_currentOffset.x, _currentOffset.y, 0f);
    }
}
