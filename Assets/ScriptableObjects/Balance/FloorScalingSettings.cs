using UnityEngine;

/// <summary>
/// Optional designer override for the enemy floor-scaling curve. Drop an instance
/// at Resources/FloorScaling.asset and it will be auto-loaded by
/// <see cref="FloorScalingCurve"/>. If no asset exists the formulas in
/// FloorScalingCurve are used directly.
/// </summary>
[CreateAssetMenu(fileName = "FloorScaling", menuName = "Balance/Floor Scaling", order = 10)]
public sealed class FloorScalingSettings : ScriptableObject
{
    [Tooltip("Horizontal axis = floor index (0-based). Vertical axis = HP multiplier.")]
    public AnimationCurve healthMultiplier = AnimationCurve.Linear(0f, 1f, 10f, 2f);

    [Tooltip("Horizontal axis = floor index (0-based). Vertical axis = damage multiplier.")]
    public AnimationCurve damageMultiplier = AnimationCurve.Linear(0f, 1f, 10f, 1.4f);

    public float EvaluateHealth(int floorIndex)
    {
        return healthMultiplier.Evaluate(Mathf.Max(0, floorIndex));
    }

    public float EvaluateDamage(int floorIndex)
    {
        return damageMultiplier.Evaluate(Mathf.Max(0, floorIndex));
    }
}
