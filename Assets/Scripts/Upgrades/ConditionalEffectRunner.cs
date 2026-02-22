using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to the player. Evaluates ConditionalEffects every frame
/// and switches between their whenTrue / whenFalse effect sets
/// as the condition changes state.
///
/// UpgradeManager does not need to know about this component;
/// ConditionalEffect.Apply() registers directly here.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class ConditionalEffectRunner : MonoBehaviour
{
    private struct ConditionalRecord
    {
        public ConditionalEffect Effect;
        public UpgradeContext Ctx;
        public bool LastState;
    }

    private List<ConditionalRecord> _records = new();
    private PlayerController _playerController;

    private void Awake() => _playerController = GetComponent<PlayerController>();

    public void Register(ConditionalEffect effect, UpgradeContext ctx)
    {
        bool initialState = Evaluate(effect);
        _records.Add(new ConditionalRecord
        {
            Effect    = effect,
            Ctx       = ctx,
            LastState = initialState
        });

        // Apply the correct branch immediately
        ApplyBranch(effect, ctx, initialState);
    }

    public void Unregister(ConditionalEffect effect, UpgradeContext ctx)
    {
        for (int i = _records.Count - 1; i >= 0; i--)
        {
            if (_records[i].Effect != effect) continue;
            // Remove whichever branch is currently active
            RemoveBranch(effect, ctx, _records[i].LastState);
            _records.RemoveAt(i);
            return;
        }
    }

    private void Update()
    {
        for (int i = 0; i < _records.Count; i++)
        {
            var rec      = _records[i];
            bool current = Evaluate(rec.Effect);
            if (current == rec.LastState) continue;

            // State flipped, swap branches
            RemoveBranch(rec.Effect, rec.Ctx, rec.LastState);
            ApplyBranch(rec.Effect, rec.Ctx, current);

            // Struct copy-back
            _records[i] = new ConditionalRecord
            {
                Effect    = rec.Effect,
                Ctx       = rec.Ctx,
                LastState = current
            };
        }
    }

    private bool Evaluate(ConditionalEffect effect)
    {
        return effect.condition switch
        {
            // Fill in later after more control variables in player controller.
            // ConditionalType.HealthBelow => PlayerController.Health.CurrentHP < effect.threshold,
            // ConditionalType.HealthAbove => PlayerController.Health.CurrentHP > effect.threshold,
            ConditionalType.FloorBelow  => GameRules.Instance.Get(GameRuleType.RoomCount) < effect.threshold,
            ConditionalType.FloorAbove  => GameRules.Instance.Get(GameRuleType.RoomCount) > effect.threshold,
            ConditionalType.Custom      => EvaluateCustom(effect),
            _                           => false
        };
    }

    /// <summary>Hook for game-specific conditions not covered by the enum.</summary>
    protected virtual bool EvaluateCustom(ConditionalEffect effect) => false;
    
    // Branch application helpers

    private static void ApplyBranch(ConditionalEffect effect, UpgradeContext ctx, bool state)
    {
        var branch = state ? effect.whenTrue : effect.whenFalse;
        foreach (var e in branch) e?.Apply(ctx);
    }

    private static void RemoveBranch(ConditionalEffect effect, UpgradeContext ctx, bool state)
    {
        var branch = state ? effect.whenTrue : effect.whenFalse;
        foreach (var e in branch) e?.Remove(ctx);
    }
}
