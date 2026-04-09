using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerDashController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private PlayerController playerController;

    private PlayerStats _playerStats;
    private float _nextDashTime;

    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!playerController) playerController = GetComponent<PlayerController>();

        _playerStats = GetComponent<PlayerStats>();
    }

    public void OnDash(InputValue value)
    {
        if (!value.isPressed) return;
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

        _nextDashTime = Time.time + dashCooldown;

        EventBus<PlayerDashedEvent>.Raise(new PlayerDashedEvent
        {
            Position = transform.position
        });

        Debug.Log($"Player dashed. Direction: {dashDirection}, Speed: {dashSpeed}, Distance: {dashDistance}");
    }
}
