using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private enum ActionState
    {
        Locomotion,
        Dash,
        Melee
    }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 40f;
    [SerializeField] private float runThreshold = 0.05f;

    [Header("Components")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Camera")]
    [SerializeField] private Camera viewCamera;

    private Vector2 _moveInput;
    private string _currentStateName;
    private Vector2 _lastVelocity;
    private ActionState _actionState = ActionState.Locomotion;
    private float _actionTimer;

    public Vector2 LastMoveDirection { get; private set; } = Vector2.right;

    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!animator)
        {
            animator = GetComponent<Animator>();
            if (!animator)
            {
                foreach (var candidate in GetComponentsInChildren<Animator>(true))
                {
                    if (candidate.runtimeAnimatorController != null &&
                        candidate.runtimeAnimatorController.name == "Player")
                    {
                        animator = candidate;
                        break;
                    }
                }
            }
        }
        if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        rb.freezeRotation = true;

        if (!viewCamera) viewCamera = Camera.main;
    }

    private void Update()
    {
        if (_actionState == ActionState.Locomotion)
            return;

        _actionTimer -= Time.deltaTime;
        if (_actionTimer <= 0f)
        {
            _actionState = ActionState.Locomotion;
            ApplyLocomotion(_lastVelocity, LastMoveDirection, forceRestart: false);
        }
    }

    public void OnMove(InputValue value)
    {
        _moveInput = value.Get<Vector2>();
    }

    private void FixedUpdate()
    {
        Vector2 camMoveDir = InputToCameraRelativeDirection(_moveInput);
        camMoveDir = SnapTo8Directions(camMoveDir);

        if (camMoveDir.sqrMagnitude > 0.0001f)
            LastMoveDirection = camMoveDir;

        Vector2 desiredVelocity = camMoveDir * moveSpeed;

        Vector2 current = rb.linearVelocity;
        Vector2 next = Vector2.MoveTowards(current, desiredVelocity, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = next;
        _lastVelocity = next;

        if (_actionState == ActionState.Locomotion)
            ApplyLocomotion(next, LastMoveDirection, forceRestart: false);
    }

    private Vector2 InputToCameraRelativeDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.0001f) return Vector2.zero;

        if (!viewCamera) return input.normalized;

        Vector3 right3 = viewCamera.transform.right;
        Vector3 up3 = viewCamera.transform.up;

        Vector2 right = new Vector2(right3.x, right3.y).normalized;
        Vector2 up = new Vector2(up3.x, up3.y).normalized;

        Vector2 dir = right * input.x + up * input.y;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.zero;
    }

    private static Vector2 SnapTo8Directions(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return Vector2.zero;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        angle = Mathf.Round(angle / 45f) * 45f;

        float rad = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
    }

    public void PlayDashAnimation(Vector2 dashDirection, float durationSeconds)
    {
        PlayAction("Dash", dashDirection, durationSeconds, ActionState.Dash);
    }

    public void PlayMeleeAnimation(Vector2 attackDirection, float durationSeconds)
    {
        PlayAction("Melee", attackDirection, durationSeconds, ActionState.Melee);
    }

    private void PlayAction(string prefix, Vector2 direction, float durationSeconds, ActionState actionState)
    {
        if (direction.sqrMagnitude > 0.0001f)
            LastMoveDirection = direction.normalized;

        _actionState = actionState;
        _actionTimer = Mathf.Max(0.01f, durationSeconds);

        string stateName = BuildStateName(prefix, LastMoveDirection, out bool flipX);
        SetFlip(flipX);
        PlayState(stateName, forceRestart: true);
    }

    private void ApplyLocomotion(Vector2 velocity, Vector2 facingDirection, bool forceRestart)
    {
        string prefix = velocity.sqrMagnitude > runThreshold * runThreshold ? "Run" : "Idle";
        string stateName = BuildStateName(prefix, facingDirection, out bool flipX);

        SetFlip(flipX);
        PlayState(stateName, forceRestart);
    }

    private string BuildStateName(string prefix, Vector2 rawDirection, out bool flipX)
    {
        Vector2 dir = rawDirection.sqrMagnitude > 0.0001f ? rawDirection.normalized : LastMoveDirection;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.right;

        flipX = dir.x < -0.0001f;
        if (flipX) dir.x = -dir.x;

        string suffix;
        if (dir.y >= 0.35f && dir.x >= 0.35f)
            suffix = "UR";
        else if (dir.y <= -0.35f && dir.x >= 0.35f)
            suffix = "DR";
        else if (Mathf.Abs(dir.y) > 0.8f)
            suffix = dir.y > 0f ? "U" : "D";
        else if (Mathf.Abs(dir.y) > 0.25f && dir.x < 0.35f)
            suffix = dir.y > 0f ? "U" : "D";
        else
            suffix = "R";

        return $"{prefix}_{suffix}";
    }

    private void PlayState(string stateName, bool forceRestart)
    {
        if (!animator) return;
        if (!forceRestart && _currentStateName == stateName) return;

        _currentStateName = stateName;
        animator.Play(stateName, 0, 0f);
    }

    private void SetFlip(bool flipX)
    {
        if (spriteRenderer)
            spriteRenderer.flipX = flipX;
    }
}
