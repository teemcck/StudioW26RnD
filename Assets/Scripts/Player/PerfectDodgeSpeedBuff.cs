using UnityEngine;

[DisallowMultipleComponent]
public sealed class PerfectDodgeSpeedBuff : MonoBehaviour
{
    [SerializeField] private float multiplierDelta = 0.30f;
    [SerializeField] private float duration = 1.5f;

    private PlayerStats _stats;
    private bool _applied;
    private float _expiresAt;

    public static void ApplyTo(GameObject target, float duration = 1.5f, float multiplierDelta = 0.30f)
    {
        if (target == null) return;

        var existing = target.GetComponent<PerfectDodgeSpeedBuff>();
        if (existing != null)
        {
            existing.Refresh(duration, multiplierDelta);
            return;
        }

        var buff = target.AddComponent<PerfectDodgeSpeedBuff>();
        buff.duration = duration;
        buff.multiplierDelta = multiplierDelta;
    }

    private void OnEnable()
    {
        _stats = GetComponent<PlayerStats>();
        if (_stats == null) { Destroy(this); return; }

        _stats.AddMultiplier(PlayerStatType.MoveSpeed, multiplierDelta);
        _applied = true;
        _expiresAt = Time.time + duration;
    }

    private void Update()
    {
        if (Time.time >= _expiresAt) Destroy(this);
    }

    private void OnDisable()
    {
        if (_applied && _stats != null)
        {
            _stats.AddMultiplier(PlayerStatType.MoveSpeed, -multiplierDelta);
            _applied = false;
        }
    }

    private void Refresh(float newDuration, float newMultiplierDelta)
    {
        float newExpiry = Time.time + newDuration;
        if (newExpiry > _expiresAt) _expiresAt = newExpiry;

        if (Mathf.Approximately(newMultiplierDelta, multiplierDelta)) return;

        if (_applied && _stats != null)
            _stats.AddMultiplier(PlayerStatType.MoveSpeed, newMultiplierDelta - multiplierDelta);

        multiplierDelta = newMultiplierDelta;
    }
}
