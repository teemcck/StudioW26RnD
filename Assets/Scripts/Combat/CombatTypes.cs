using UnityEngine;

public enum AttackKind
{
    Unknown,
    Melee,
    Ranged,
    EnergyBolt,
    StatusEffect,
    Contact
}

public readonly struct DamageContext
{
    public static readonly DamageContext Empty = new(null, null, AttackKind.Unknown, null, false, false);

    public readonly GameObject Source;
    public readonly GameObject Instigator;
    public readonly AttackKind AttackKind;
    public readonly string SourceId;
    public readonly bool IsStatusEffect;
    public readonly bool TriggersOnHitEffects;

    public DamageContext(
        GameObject source,
        GameObject instigator,
        AttackKind attackKind,
        string sourceId = null,
        bool isStatusEffect = false,
        bool triggersOnHitEffects = false)
    {
        Source = source;
        Instigator = instigator;
        AttackKind = attackKind;
        SourceId = sourceId;
        IsStatusEffect = isStatusEffect;
        TriggersOnHitEffects = triggersOnHitEffects;
    }

    public bool WasCausedByPlayer =>
        Instigator != null && Instigator.GetComponent<PlayerController>() != null;
}

public readonly struct AttackDamageSnapshot
{
    public readonly float FlatBonus;
    public readonly float MultiplierBonus;
    public readonly bool ConsumeSpiteStacks;

    public AttackDamageSnapshot(float flatBonus, float multiplierBonus, bool consumeSpiteStacks)
    {
        FlatBonus = flatBonus;
        MultiplierBonus = multiplierBonus;
        ConsumeSpiteStacks = consumeSpiteStacks;
    }

    public float ApplyTo(float baseDamage)
    {
        return Mathf.Max(0f, (baseDamage + FlatBonus) * (1f + MultiplierBonus));
    }
}
