using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SimpleProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 8f;
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] flightAnimationSprites;
    [SerializeField] private float animationFps = 16f;
    [SerializeField] private bool rotateToDirection = true;

    [Header("Hit")]
    [SerializeField] private float damage = 2f;
    [SerializeField] private float knockbackForce = 4f;
    [SerializeField] private LayerMask hitMask;

    private Rigidbody2D _rb;
    private Animator _animator;
    private Vector2 _dir;
    private float _animTimer;
    private int _animFrame;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (!spriteRenderer)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _animator = GetComponent<Animator>();
    }

    public void Fire(Vector2 direction)
    {
        _dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        _animTimer = 0f;
        _animFrame = 0;
        ApplyAnimationFrame(_animFrame);
        Destroy(gameObject, lifetime);
    }

    private void FixedUpdate()
    {
        _rb.linearVelocity = _dir * speed;

        if (rotateToDirection && _dir.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
            _rb.MoveRotation(angle);
        }

        AnimateFlight();
    }

    private void AnimateFlight()
    {
        if (_animator && _animator.runtimeAnimatorController != null)
            return;
        if (spriteRenderer == null || flightAnimationSprites == null || flightAnimationSprites.Length == 0)
            return;

        float fps = Mathf.Max(0.1f, animationFps);
        _animTimer += Time.fixedDeltaTime;
        float frameDuration = 1f / fps;
        while (_animTimer >= frameDuration)
        {
            _animTimer -= frameDuration;
            _animFrame = (_animFrame + 1) % flightAnimationSprites.Length;
            ApplyAnimationFrame(_animFrame);
        }
    }

    private void ApplyAnimationFrame(int frame)
    {
        if (spriteRenderer == null || flightAnimationSprites == null || flightAnimationSprites.Length == 0)
            return;
        spriteRenderer.sprite = flightAnimationSprites[Mathf.Clamp(frame, 0, flightAnimationSprites.Length - 1)];
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & hitMask.value) == 0)
            return;

        var dmg = other.GetComponentInParent<IDamageable>();
        if (dmg != null)
            dmg.TakeHit(damage, _dir, knockbackForce);
        
        Destroy(gameObject);
    }
}
