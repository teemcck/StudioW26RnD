using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SimpleProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 8f;
    [SerializeField] private float lifetime = 3f;

    [Header("Hit")]
    [SerializeField] private float damage = 2f;
    [SerializeField] private float knockbackForce = 4f;
    [SerializeField] private LayerMask hitMask;

    private Rigidbody2D _rb;
    private Vector2 _dir;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    public void Fire(Vector2 direction)
    {
        _dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        Destroy(gameObject, lifetime);
    }

    private void FixedUpdate()
    {
        _rb.linearVelocity = _dir * speed;
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
