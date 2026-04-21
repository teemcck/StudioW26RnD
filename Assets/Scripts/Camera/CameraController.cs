using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private CinemachineCamera cineCamera;
    [SerializeField] private CinemachineImpulseSource impulseSource;
    [SerializeField] private Transform playerTransform;

    [Header("Context Zoom")]
    [Tooltip("Enemy layer used by crowd-zoom polling.")]
    [SerializeField] private LayerMask crowdZoomEnemyLayer = ~0;
    [Tooltip("Radius of the crowd detection circle, in world units.")]
    [SerializeField] private float crowdZoomRadius = 5.5f;
    [Tooltip("Ortho offset applied when the crowd is at its max size.")]
    [SerializeField] private float crowdZoomMaxOffset = 0.6f;
    [Tooltip("Rate at which the ortho offset lerps toward its target.")]
    [SerializeField] private float crowdZoomLerpSpeed = 2.5f;

    private CinemachineConfiner2D _confiner;
    private float _orthoBaseline;
    private Coroutine _orthoRoutine;
    private Transform _savedFollowBeforePhase;
    private float _breathOffset;
    private Coroutine _breathRoutine;
    private float _crowdOffsetTarget;
    private float _crowdOffset;
    private float _nextCrowdPollTime;

    private void Awake()
    {
        if (cineCamera == null)
            cineCamera = GetComponent<CinemachineCamera>();
        if (cineCamera == null)
            cineCamera = GetComponentInChildren<CinemachineCamera>(true);

        if (impulseSource == null)
            impulseSource = GetComponent<CinemachineImpulseSource>();
        if (impulseSource == null && cineCamera != null)
            impulseSource = cineCamera.GetComponent<CinemachineImpulseSource>();
        if (impulseSource == null)
            impulseSource = GetComponentInChildren<CinemachineImpulseSource>(true);

        EnsureImpulseListenerOnVirtualCamera();
        ApplyImpulseDefinitionDefaults();

        if (playerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) playerTransform = p.transform;
        }

        if (!TryGetComponent<CinemachineConfiner2D>(out _confiner))
            TryGetComponent(out _confiner);

        if (cineCamera != null)
            _orthoBaseline = cineCamera.Lens.OrthographicSize;
    }

    private void ApplyImpulseDefinitionDefaults()
    {
        if (impulseSource == null || impulseSource.ImpulseDefinition == null)
            return;
        var def = impulseSource.ImpulseDefinition;
        def.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
        def.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
        def.ImpulseDuration = 0.11f;
        if (def.ImpulseChannel == 0)
            def.ImpulseChannel = 1;
    }

    private void EnsureImpulseListenerOnVirtualCamera()
    {
        if (cineCamera == null)
            return;
        var go = cineCamera.gameObject;
        var listener = go.GetComponent<CinemachineImpulseListener>();
        if (listener == null)
            listener = go.AddComponent<CinemachineImpulseListener>();

        listener.Use2DDistance = true;
        listener.Gain = 1f;
        listener.UseCameraSpace = true;
        listener.ChannelMask = -1;
        listener.ApplyAfter = CinemachineCore.Stage.Finalize;
    }

    public void LockToTransform(Transform target)
    {
        if (cineCamera != null)
            cineCamera.Follow = target;
    }

    public void LockToPlayer()
    {
        if (cineCamera != null && playerTransform != null)
            cineCamera.Follow = playerTransform;
    }

    private void LateUpdate()
    {
        if (cineCamera == null)
            return;

        if (Time.unscaledTime >= _nextCrowdPollTime)
        {
            _nextCrowdPollTime = Time.unscaledTime + 0.25f;
            PollCrowdZoom();
        }

        _crowdOffset = Mathf.Lerp(_crowdOffset, _crowdOffsetTarget, Mathf.Clamp01(Time.unscaledDeltaTime * crowdZoomLerpSpeed));

        if (_orthoRoutine == null)
        {
            float target = Mathf.Max(0.1f, _orthoBaseline + _crowdOffset + _breathOffset);
            cineCamera.Lens.OrthographicSize = target;
        }
    }

    private void PollCrowdZoom()
    {
        if (playerTransform == null)
        {
            _crowdOffsetTarget = 0f;
            return;
        }

        var hits = Physics2D.OverlapCircleAll(playerTransform.position, crowdZoomRadius, crowdZoomEnemyLayer);
        int enemyCount = 0;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;
            if (hits[i].GetComponentInParent<EnemyBase>() != null)
                enemyCount++;
        }

        CrowdZoom(enemyCount);
    }

    /// <summary>Driven from an external poller or <see cref="PollCrowdZoom"/>.</summary>
    public void CrowdZoom(int nearbyEnemyCount)
    {
        float t = Mathf.Clamp01(nearbyEnemyCount / 5f);
        _crowdOffsetTarget = t * crowdZoomMaxOffset;
    }

    /// <summary>Short ortho "breath" — dip by <paramref name="amount"/> for <paramref name="duration"/>, then restore.</summary>
    public void BreathIn(float amount = 0.25f, float duration = 0.25f)
    {
        if (_breathRoutine != null)
            StopCoroutine(_breathRoutine);
        _breathRoutine = StartCoroutine(BreathRoutine(amount, duration));
    }

    private System.Collections.IEnumerator BreathRoutine(float amount, float duration)
    {
        float half = duration * 0.5f;
        float u = 0f;
        while (u < 1f)
        {
            u += Time.unscaledDeltaTime / Mathf.Max(0.01f, half);
            _breathOffset = Mathf.Lerp(0f, -Mathf.Abs(amount), Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u)));
            yield return null;
        }

        u = 0f;
        while (u < 1f)
        {
            u += Time.unscaledDeltaTime / Mathf.Max(0.01f, half);
            _breathOffset = Mathf.Lerp(-Mathf.Abs(amount), 0f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u)));
            yield return null;
        }

        _breathOffset = 0f;
        _breathRoutine = null;
    }

    public void Shake(float intensity = 0.2f)
    {
        if (impulseSource == null)
            return;
        float t = Mathf.Clamp(intensity, 0.02f, 2f);
        Vector2 dir = Random.insideUnitCircle;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.right;
        dir.Normalize();
        float mag = 0.32f + t * 0.95f;
        impulseSource.GenerateImpulseWithVelocity(new Vector3(dir.x * mag, dir.y * mag, 0f));
    }

    public void ShakeShieldBreak()
    {
        Shake(0.16f);
    }

    public void ShakeTap() => ShakeWithShape(CinemachineImpulseDefinition.ImpulseShapes.Bump, 0.08f, 0.25f);

    public void ShakeMedium(Vector2 direction = default) => ShakeWithShape(CinemachineImpulseDefinition.ImpulseShapes.Recoil, 0.18f, 0.9f, direction);

    public void ShakeFatality() => ShakeWithShape(CinemachineImpulseDefinition.ImpulseShapes.Rumble, 0.55f, 1.4f);

    private void ShakeWithShape(CinemachineImpulseDefinition.ImpulseShapes shape, float duration, float magnitude, Vector2 direction = default)
    {
        if (impulseSource == null || impulseSource.ImpulseDefinition == null)
            return;

        var def = impulseSource.ImpulseDefinition;
        float prevDur = def.ImpulseDuration;
        var prevShape = def.ImpulseShape;
        var prevType = def.ImpulseType;

        def.ImpulseShape = shape;
        def.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
        def.ImpulseDuration = duration;

        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Random.insideUnitCircle;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.right;
        dir.Normalize();

        impulseSource.GenerateImpulseWithVelocity(new Vector3(dir.x * magnitude, dir.y * magnitude, 0f));

        def.ImpulseDuration = prevDur;
        def.ImpulseShape = prevShape;
        def.ImpulseType = prevType;
    }

    public void ShakeRumble(float intensity = 0.2f)
    {
        if (impulseSource == null || impulseSource.ImpulseDefinition == null)
            return;
        var def = impulseSource.ImpulseDefinition;
        float prevDur = def.ImpulseDuration;
        var prevShape = def.ImpulseShape;
        var prevType = def.ImpulseType;

        float t = Mathf.Clamp(intensity, 0.02f, 2f);
        def.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Rumble;
        def.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
        def.ImpulseDuration = Mathf.Lerp(0.38f, 0.68f, Mathf.Clamp01(t / 0.35f));

        Vector2 dir = Random.insideUnitCircle;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.right;
        dir.Normalize();
        float mag = 0.05f + t * 0.2f;
        impulseSource.GenerateImpulseWithVelocity(new Vector3(dir.x * mag, dir.y * mag, 0f));

        def.ImpulseDuration = prevDur;
        def.ImpulseShape = prevShape;
        def.ImpulseType = prevType;
    }

    public void PhaseTransitionZoomIn(Transform focusOn)
    {
        if (cineCamera == null)
            return;
        StopOrthoRoutine();
        _savedFollowBeforePhase = cineCamera.Follow;
        Transform target = focusOn ? focusOn : playerTransform;
        if (target)
            cineCamera.Follow = target;
        _orthoBaseline = cineCamera.Lens.OrthographicSize;
        float zoomed = Mathf.Max(3.2f, _orthoBaseline * 0.74f);
        _orthoRoutine = StartCoroutine(OrthoLerpTo(zoomed, 0.52f));
    }

    public void PhaseTransitionZoomRestore()
    {
        if (cineCamera == null)
            return;
        StopOrthoRoutine();
        if (playerTransform)
            cineCamera.Follow = playerTransform;
        else if (_savedFollowBeforePhase)
            cineCamera.Follow = _savedFollowBeforePhase;
        _savedFollowBeforePhase = null;
        _orthoRoutine = StartCoroutine(OrthoLerpTo(_orthoBaseline, 0.48f));
    }

    private void StopOrthoRoutine()
    {
        if (_orthoRoutine != null)
        {
            StopCoroutine(_orthoRoutine);
            _orthoRoutine = null;
        }
    }

    private IEnumerator OrthoLerpTo(float targetOrtho, float duration)
    {
        if (cineCamera == null)
            yield break;
        float start = cineCamera.Lens.OrthographicSize;
        duration = Mathf.Max(0.02f, duration);
        float u = 0f;
        while (u < 1f)
        {
            u += Time.deltaTime / duration;
            float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u));
            cineCamera.Lens.OrthographicSize = Mathf.Lerp(start, targetOrtho, s);
            yield return null;
        }
        cineCamera.Lens.OrthographicSize = targetOrtho;
        _orthoRoutine = null;
    }

    public IEnumerator PlayDeathKillImpactRoutine()
    {
        if (cineCamera == null)
            yield break;
        float ortho0 = cineCamera.Lens.OrthographicSize;
        float scale0 = Time.timeScale;
        float orthoTarget = ortho0 - 0.28f;
        float scaleTarget = 0.72f;
        float inDur = 0.2f;
        float u = 0f;
        while (u < 1f)
        {
            u += Time.unscaledDeltaTime / inDur;
            float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u));
            Time.timeScale = Mathf.Lerp(scale0, scaleTarget, s);
            cineCamera.Lens.OrthographicSize = Mathf.Lerp(ortho0, orthoTarget, s);
            yield return null;
        }

        float hold = 0f;
        while (hold < 0.07f)
        {
            hold += Time.unscaledDeltaTime;
            yield return null;
        }

        float outDur = 0.18f;
        float o1 = cineCamera.Lens.OrthographicSize;
        float s1 = Time.timeScale;
        u = 0f;
        while (u < 1f)
        {
            u += Time.unscaledDeltaTime / outDur;
            float sm = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u));
            Time.timeScale = Mathf.Lerp(s1, 1f, sm);
            cineCamera.Lens.OrthographicSize = Mathf.Lerp(o1, ortho0, sm);
            yield return null;
        }

        Time.timeScale = 1f;
        cineCamera.Lens.OrthographicSize = ortho0;
    }

    public void UpdateConfinerCollider(Collider2D collider)
    {
        if (_confiner != null)
        {
            _confiner.BoundingShape2D = collider;
            _confiner.InvalidateBoundingShapeCache();
        }
    }
}
