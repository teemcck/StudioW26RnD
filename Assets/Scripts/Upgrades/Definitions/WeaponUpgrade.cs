using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Weapon Upgrade", fileName = "WeaponUpgrade")]
public class WeaponUpgrade : ScriptableObject
{
    [Min(0.01f)] public float damageMultiplier = 1.1f;
    [Min(0.01f)] public float knockbackMultiplier = 1.0f;
    [Min(0.01f)] public float cooldownMultiplier = 1.0f;
}
