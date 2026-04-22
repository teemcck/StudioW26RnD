using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerDashController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private PlayerController playerController;

    [Header("Dodge")]
    [Tooltip("How long after a dash starts the player is invulnerable (perfect-dodge window).")]
    [SerializeField] private float dashInvulnWindow = 0.18f;

    private PlayerStats _playerStats;
    private float _nextDashTime;
    private float _dodgeEnds = -999f;

    public bool IsDodgeInvulnerable => Time.time < _dodgeEnds;
    public float NextDashTime => _nextDashTime;

    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!playerController) playerController = GetComponent<PlayerController>();

        _playerStats = GetComponent<PlayerStats>();
    }

    public void OnDash(InputValue value)
    {
        if (!value.isPressed) return;
        if (playerController && playerController.IsControlLocked) return;
        if (Time.time < _nextDashTime) return;

        PerformDash();
    }

    private void PerformDash()
    {
        Vector2 dashDirection = playerController.LastMoveDirection;
        if (dashDirection.sqrMagnitude < 0.0001f)
            dashDirection = Vector2.right;

        float dashSpeed = _playerStats.DashSpeed;
        float dashDistance = _playerStats.DashDistance;
        float dashCooldown = _playerStats.DashCooldown;

        Vector2 dashVelocity = dashDirection * dashSpeed;
        rb.linearVelocity = dashVelocity;

        float dashDuration = dashSpeed > 0.0001f ? (dashDistance / dashSpeed) : 0.1f;
        if (playerController)
            playerController.PlayDashAnimation(dashDirection, dashDuration);

        _nextDashTime = Time.time + dashCooldown;
        _dodgeEnds = Time.time + Mathf.Max(0f, dashInvulnWindow);

        var cam = Object.FindFirstObjectByType<CameraController>();
        if (cam != null)
            cam.BreathIn();

        EventBus<PlayerDashedEvent>.Raise(new PlayerDashedEvent
        {
            Position = transform.position
        });
    }

    public void ReduceRemainingCooldown(float seconds)
    {
        if (seconds <= 0f)
            return;

        _nextDashTime = Mathf.Max(Time.time, _nextDashTime - seconds);
    }
}
