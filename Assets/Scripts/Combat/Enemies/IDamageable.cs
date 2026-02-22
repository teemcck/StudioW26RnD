using UnityEngine;

public interface IDamageable
{
    void TakeHit(float damage, Vector2 knockbackDirection, float knockbackForce);
}
