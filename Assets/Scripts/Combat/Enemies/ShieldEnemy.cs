using System.Collections;
using UnityEngine;

public class ShieldEnemy : MeleeEnemy
{
    [Header("Shield combat")]
    [SerializeField] private float shieldMaxHealth = 15f;
    [SerializeField] private float shieldBlockHalfAngle = 100f;
    [SerializeField] private float shieldTurnSpeedDegrees = 85f;

    [Header("Shield visual")]
    [SerializeField] private Transform shieldPivot;
    [SerializeField] private SpriteRenderer shieldSprite;
    [SerializeField] private float shieldOrbitRadius = 0.52f;
    [SerializeField] private Color shieldColor = new Color(0.45f, 0.75f, 1f, 0.95f);
    [SerializeField] private Color shieldFlashColor = Color.white;
    [SerializeField] private float shieldFlashDuration = 0.1f;
    [SerializeField] private float shieldVisualThickness = 0.11f;
    [SerializeField] private float shieldVisualHeight = 1.05f;

    private float _shieldHp;
    private Color _shieldBaseColor;
    private Coroutine _shieldFlashCo;
    private float _shieldPivotZDeg;

    protected override void Awake()
    {
        base.Awake();
        _shieldHp = shieldMaxHealth;

        if (!shieldPivot)
        {
            var found = transform.Find("ShieldPivot");
            if (found) shieldPivot = found;
        }

        if (!shieldSprite && shieldPivot)
            shieldSprite = shieldPivot.GetComponentInChildren<SpriteRenderer>();

        if (shieldSprite)
        {
            _shieldBaseColor = shieldColor;
            shieldSprite.color = _shieldBaseColor;
        }

        if (shieldPivot)
            _shieldPivotZDeg = shieldPivot.eulerAngles.z;

        ApplyShieldOrbitOffset();
    }

    protected override void FixedUpdate()
    {
        AimShieldTowardPlayer();
        base.FixedUpdate();
    }

    private void ApplyShieldOrbitOffset()
    {
        if (!shieldSprite) return;
        var visual = shieldSprite.transform;
        visual.localPosition = new Vector3(shieldOrbitRadius, 0f, 0f);
        visual.localScale = new Vector3(shieldVisualThickness, shieldVisualHeight, 1f);
    }

    private void OnValidate()
    {
        if (shieldPivot && shieldSprite && Application.isPlaying == false)
            ApplyShieldOrbitOffset();
    }

    private void AimShieldTowardPlayer()
    {
        if (!Player || !shieldPivot) return;
        Vector2 to = (Vector2)(Player.position - transform.position);
        if (to.sqrMagnitude < 0.0001f) return;

        float targetZ = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
        _shieldPivotZDeg = Mathf.MoveTowardsAngle(_shieldPivotZDeg, targetZ, shieldTurnSpeedDegrees * Time.fixedDeltaTime);
        shieldPivot.rotation = Quaternion.Euler(0f, 0f, _shieldPivotZDeg);
        ApplyShieldOrbitOffset();
    }

    private Vector2 GetShieldFacing()
    {
        if (!shieldPivot) return Vector2.right;
        Vector2 f = shieldPivot.right;
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector2.right;
    }

    public override void TakeHit(float damage, Vector2 knockbackDirection, float knockbackForce, DamageContext context = default)
    {
        if (damage <= 0f)
            return;

        if (_shieldHp <= 0f)
        {
            base.TakeHit(damage, knockbackDirection, knockbackForce, context);
            return;
        }

        Vector2 towardPlayer = (Vector2)(Player.position - transform.position);
        if (towardPlayer.sqrMagnitude < 0.0001f)
        {
            base.TakeHit(damage, knockbackDirection, knockbackForce, context);
            return;
        }
        towardPlayer.Normalize();

        Vector2 incoming = knockbackDirection.sqrMagnitude > 0.0001f
            ? knockbackDirection.normalized
            : ((Vector2)transform.position - (Vector2)Player.position).normalized;

        Vector2 shieldForward = GetShieldFacing();
        float cosHalf = Mathf.Cos((shieldBlockHalfAngle * 0.5f) * Mathf.Deg2Rad);
        float align = Vector2.Dot(incoming, -shieldForward);
        bool blocked = align >= cosHalf;

        if (!blocked)
        {
            base.TakeHit(damage, knockbackDirection, knockbackForce, context);
            return;
        }

        _shieldHp -= damage;
        PlayShieldFlash();

        if (_shieldHp <= 0f)
        {
            _shieldHp = 0f;
            OnShieldBroken();
        }
    }

    private void PlayShieldFlash()
    {
        if (!shieldSprite) return;
        if (_shieldFlashCo != null)
            StopCoroutine(_shieldFlashCo);
        _shieldFlashCo = StartCoroutine(ShieldFlashRoutine());
    }

    private IEnumerator ShieldFlashRoutine()
    {
        shieldSprite.color = shieldFlashColor;
        yield return new WaitForSeconds(shieldFlashDuration);
        if (shieldSprite && shieldSprite.enabled)
            shieldSprite.color = _shieldHp > 0f ? _shieldBaseColor : new Color(0.5f, 0.5f, 0.55f, 0.35f);
        _shieldFlashCo = null;
    }

    private void OnShieldBroken()
    {
        if (shieldPivot)
            shieldPivot.gameObject.SetActive(false);
        if (shieldSprite)
        {
            shieldSprite.enabled = false;
            shieldSprite.color = new Color(0.4f, 0.4f, 0.45f, 0.25f);
        }
    }
}
