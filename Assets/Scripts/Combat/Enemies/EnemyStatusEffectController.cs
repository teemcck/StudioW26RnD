using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyBase))]
public class EnemyStatusEffectController : MonoBehaviour
{
    private readonly Dictionary<string, StatusEffect> _activeEffects = new();
    private readonly Dictionary<string, DamageContext> _effectContexts = new();

    private EnemyBase _enemy;
    private EnemyWorldVisuals _worldVisuals;
    private float _poisonTickTimer;

    private void Awake()
    {
        _enemy = GetComponent<EnemyBase>();
        _worldVisuals = GetComponent<EnemyWorldVisuals>();
    }

    public void Apply(string effectId, float duration, int maxStacks = 1, bool isPermanent = false, DamageContext context = default)
    {
        if (string.IsNullOrWhiteSpace(effectId))
            return;

        if (context.Source != null || context.Instigator != null)
            _effectContexts[effectId] = context;

        if (_activeEffects.TryGetValue(effectId, out var existing))
        {
            existing.maxStacks = Mathf.Max(existing.maxStacks, maxStacks);
            existing.isPermanent |= isPermanent;
            existing.duration = isPermanent ? 0f : Mathf.Max(existing.duration, duration);
            existing.AddStack();
        }
        else
        {
            _activeEffects[effectId] = new StatusEffect(effectId, duration, maxStacks, isPermanent);
        }

        _worldVisuals?.NotifyStatusApplied(effectId);

        if (effectId == StatusEffectIds.Frailty)
            TryExecuteFrailty();
    }

    public void ApplyTransferredEffect(StatusEffect effect, DamageContext context)
    {
        if (effect == null || string.IsNullOrWhiteSpace(effect.id))
            return;

        var copy = effect.Clone();
        _activeEffects[copy.id] = copy;

        if (context.Source != null || context.Instigator != null)
            _effectContexts[copy.id] = context;

        _worldVisuals?.NotifyStatusApplied(copy.id);

        if (copy.id == StatusEffectIds.Frailty)
            TryExecuteFrailty();
    }

    public bool Has(string effectId) => _activeEffects.ContainsKey(effectId);

    public int GetStackCount(string effectId)
        => _activeEffects.TryGetValue(effectId, out var effect) ? effect.currentStacks : 0;

    public float GetMoveSpeedMultiplier()
    {
        float confusionPenalty = Mathf.Min(2, GetStackCount(StatusEffectIds.Confusion)) * 0.4f;
        return Mathf.Max(0.1f, 1f - confusionPenalty);
    }

    public float GetAttackSpeedMultiplier()
    {
        float confusionPenalty = Mathf.Min(2, GetStackCount(StatusEffectIds.Confusion)) * 0.4f;
        return Mathf.Max(0.1f, 1f - confusionPenalty);
    }

    public IReadOnlyCollection<StatusEffect> GetNegativeEffects()
    {
        var results = new List<StatusEffect>();
        foreach (var effect in _activeEffects.Values)
        {
            if (!StatusEffectIds.IsNegative(effect.id))
                continue;

            results.Add(effect.Clone());
        }

        return results;
    }

    public void TransferNegativeEffectsTo(EnemyStatusEffectController target)
    {
        if (!target)
            return;

        foreach (var effect in GetNegativeEffects())
            target.ApplyTransferredEffect(effect, GetEffectContext(effect.id));
    }

    private void Update()
    {
        if (_activeEffects.Count == 0 || _enemy == null || _enemy.IsDead)
            return;

        TickPoison();
        ExpireEffects();
        TryExecuteFrailty();
    }

    private void TickPoison()
    {
        int poisonStacks = GetStackCount(StatusEffectIds.Poison);
        if (poisonStacks <= 0)
        {
            _poisonTickTimer = 0f;
            return;
        }

        _poisonTickTimer += Time.deltaTime;
        while (_poisonTickTimer >= 1f)
        {
            _poisonTickTimer -= 1f;
            float damage = poisonStacks * 2f;
            _enemy.ApplyStatusDamage(damage, BuildStatusEffectContext(StatusEffectIds.Poison));
            if (_enemy.IsDead)
                return;
        }
    }

    private void ExpireEffects()
    {
        List<string> expired = null;
        foreach (var pair in _activeEffects)
        {
            var effect = pair.Value;
            if (effect.isPermanent)
                continue;

            effect.timeRemaining -= Time.deltaTime;
            if (!effect.IsExpired)
                continue;

            expired ??= new List<string>();
            expired.Add(pair.Key);
        }

        if (expired == null)
            return;

        foreach (var id in expired)
        {
            _activeEffects.Remove(id);
            _effectContexts.Remove(id);
        }
    }

    private void TryExecuteFrailty()
    {
        if (!Has(StatusEffectIds.Frailty))
            return;

        float threshold = _enemy.MaxHealth * 0.15f;
        if (_enemy.CurrentHealth <= threshold)
            _enemy.ExecuteFrailty(BuildStatusEffectContext(StatusEffectIds.Frailty));
    }

    private DamageContext BuildStatusEffectContext(string effectId)
    {
        DamageContext baseContext = GetEffectContext(effectId);
        return new DamageContext(
            baseContext.Source,
            baseContext.Instigator,
            AttackKind.StatusEffect,
            effectId,
            isStatusEffect: true);
    }

    private DamageContext GetEffectContext(string effectId)
    {
        return _effectContexts.TryGetValue(effectId, out var context) ? context : default;
    }
}
