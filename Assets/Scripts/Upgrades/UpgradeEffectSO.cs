using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Contains an open-ended list of typed UpgradeEffect subclasses.
///
/// In the Inspector, right-click the Effects list to add any registered
/// UpgradeEffect subclass as a polymorphic entry. Each entry type
/// has its own serialized fields.
///
/// Example "Glass Cannon" card:
///   Effects:
///     [0] StatMultiplierEffect  { stat = AttackDamage, multiplier = 2.0 }
///     [1] SpawnMultiplierEffect { multiplier = 1.5 }
///
/// Both effects are applied atomically when the player picks the card.
/// </summary>
[CreateAssetMenu(fileName = "UpgradeEffect_", menuName = "Upgrades/Upgrade Effect")]
public class UpgradeEffectSO : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Primary key. Must match the paired UpgradeDisplaySO.")]
    public string upgradeID;

    [Header("Stacking")]
    [Tooltip("Max times this upgrade can be acquired per run. -1 = unlimited.")]
    public int maxStacks = 1;

    [Tooltip("If true, numeric values in effects are scaled by stack count on Apply.")]
    public bool scaleWithStacks = false;

    [Header("Effects")]
    [Tooltip("Open-ended list of effects. Right-click to add any UpgradeEffect subclass.")]
    [SerializeReference]
    public List<UpgradeEffect> effects = new();
    
    // Helpers used by UpgradeManager

    public void Apply(UpgradeContext ctx, int stackCount = 1)
    {
        foreach (var effect in effects)
        {
            if (effect == null) continue;
            // Stacking: if scaleWithStacks, caller should call Apply once per new stack.
            // Exposed the count here for effects that want to read it themselves.
            effect.Apply(ctx);
        }
    }

    public void Remove(UpgradeContext ctx)
    {
        foreach (var effect in effects)
            effect?.Remove(ctx);
    }

    /// <summary>True if any effect in this SO needs a per-frame Tick.</summary>
    public bool HasTickingEffects()
    {
        foreach (var e in effects)
            if (e != null && e.NeedsTick) return true;
        return false;
    }

    public void Tick(UpgradeContext ctx, float deltaTime)
    {
        foreach (var effect in effects)
            if (effect != null && effect.NeedsTick)
                effect.Tick(ctx, deltaTime);
    }

    /// <summary>
    /// Auto-generates a description by concatenating each effect's GetDescription().
    /// Useful if you want upgrade cards to be data-driven without manual copy.
    /// </summary>
    public string BuildAutoDescription()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var e in effects)
        {
            if (e == null) continue;
            string desc = e.GetDescription();
            if (!string.IsNullOrEmpty(desc))
                sb.AppendLine($"• {desc}");
        }
        return sb.ToString().TrimEnd();
    }
}
