using UnityEngine;

[System.Serializable]
public class PlayerUpgradeModifierEffect : UpgradeEffect
{
    public PlayerUpgradeRuntime.Modifier modifier;
    public float amount = 1f;

    public override void Apply(UpgradeContext ctx)
    {
        ctx.Runtime?.AddModifier(modifier, amount);
    }

    public override void Remove(UpgradeContext ctx)
    {
        ctx.Runtime?.AddModifier(modifier, -amount);
    }

    public override string GetDescription()
    {
        return $"{modifier}: {(amount >= 0f ? "+" : "")}{amount}";
    }
}
