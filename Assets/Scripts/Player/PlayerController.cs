using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 40f;

    [Header("Components")]
    [SerializeField] private Rigidbody2D rb;

    [Header("Camera")]
    [SerializeField] private Camera viewCamera;

    private Vector2 _moveInput;

    public Vector2 LastMoveDirection { get; private set; } = Vector2.right;

    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;

        if (!viewCamera) viewCamera = Camera.main;
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
}
