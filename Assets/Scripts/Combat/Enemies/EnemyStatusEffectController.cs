using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyBase))]
public class EnemyStatusEffectController : MonoBehaviour
{
    private readonly Dictionary<StatusEffectId, StatusEffect> _activeEffects = new();
    private readonly Dictionary<StatusEffectId, DamageContext> _effectContexts = new();

    private EnemyBase _enemy;
    private EnemyWorldVisuals _worldVisuals;
    private float _poisonTickTimer;

    private void Awake()
    {
        _enemy = GetComponent<EnemyBase>();
        _worldVisuals = GetComponent<EnemyWorldVisuals>();
    }

    public void Apply(StatusEffectId effectId, float duration, int maxStacks = 1, bool isPermanent = false, DamageContext context = default)
    {
        if (context.Source != null || context.Instigator != null)
            _effectContexts[effectId] = context;

        if (_activeEffects.TryGetValue(effectId, out StatusEffect existing))
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

        if (effectId == StatusEffectId.Frailty)
            TryExecuteFrailty();
    }

    public void ApplyTransferredEffect(StatusEffect effect, DamageContext context)
    {
        if (effect == null)
            return;

        StatusEffect copy = effect.Clone();
        _activeEffects[copy.id] = copy;

        if (context.Source != null || context.Instigator != null)
            _effectContexts[copy.id] = context;

        _worldVisuals?.NotifyStatusApplied(copy.id);

        if (copy.id == StatusEffectId.Frailty)
            TryExecuteFrailty();
    }

    public bool Has(StatusEffectId effectId) => _activeEffects.ContainsKey(effectId);

    public int GetStackCount(StatusEffectId effectId)
        => _activeEffects.TryGetValue(effectId, out StatusEffect effect) ? effect.currentStacks : 0;

    public float GetMoveSpeedMultiplier()
    {
        float confusionPenalty = Mathf.Min(2, GetStackCount(StatusEffectId.Confusion)) * 0.4f;
        return Mathf.Max(0.1f, 1f - confusionPenalty);
    }

    public float GetAttackSpeedMultiplier()
    {
        float confusionPenalty = Mathf.Min(2, GetStackCount(StatusEffectId.Confusion)) * 0.4f;
        return Mathf.Max(0.1f, 1f - confusionPenalty);
    }

    public IReadOnlyCollection<StatusEffect> GetNegativeEffects()
    {
        List<StatusEffect> results = new List<StatusEffect>();
        foreach (StatusEffect effect in _activeEffects.Values)
        {
            if (!effect.id.IsNegative())
                continue;

            results.Add(effect.Clone());
        }

        return results;
    }

    public void TransferNegativeEffectsTo(EnemyStatusEffectController target)
    {
        if (!target)
            return;

        foreach (StatusEffect effect in GetNegativeEffects())
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
        int poisonStacks = GetStackCount(StatusEffectId.Poison);
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
            _enemy.ApplyStatusDamage(damage, BuildStatusEffectContext(StatusEffectId.Poison));
            if (_enemy.IsDead)
                return;
        }
    }

    private void ExpireEffects()
    {
        List<StatusEffectId> expired = null;
        foreach (KeyValuePair<StatusEffectId, StatusEffect> pair in _activeEffects)
        {
            StatusEffect effect = pair.Value;
            if (effect.isPermanent)
                continue;

            effect.timeRemaining -= Time.deltaTime;
            if (!effect.IsExpired)
                continue;

            expired ??= new List<StatusEffectId>();
            expired.Add(pair.Key);
        }

        if (expired == null)
            return;

        foreach (StatusEffectId id in expired)
        {
            _activeEffects.Remove(id);
            _effectContexts.Remove(id);
        }
    }

    private void TryExecuteFrailty()
    {
        if (!Has(StatusEffectId.Frailty))
            return;

        float threshold = _enemy.MaxHealth * 0.15f;
        if (_enemy.CurrentHealth <= threshold)
            _enemy.ExecuteFrailty(BuildStatusEffectContext(StatusEffectId.Frailty));
    }

    private DamageContext BuildStatusEffectContext(StatusEffectId effectId)
    {
        DamageContext baseContext = GetEffectContext(effectId);
        return new DamageContext(
            baseContext.Source,
            baseContext.Instigator,
            AttackKind.StatusEffect,
            effectId.ToKey(),
            isStatusEffect: true);
    }

    private DamageContext GetEffectContext(StatusEffectId effectId)
    {
        return _effectContexts.TryGetValue(effectId, out DamageContext context) ? context : default;
    }
}
