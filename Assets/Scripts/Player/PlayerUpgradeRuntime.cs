using UnityEngine;

[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(StatusEffectManager))]
public class PlayerUpgradeRuntime : MonoBehaviour
{
    public enum Modifier
    {
        LifestealPercent,
        CrowdPleaserDamagePerEnemy,
        BallistaDamagePerTile,
        BulwarkReductionPerEnemy,
        BerserkerDamagePerMissingHealth,
        AssassinDamageMultiplier,
        AssassinDuration,
        OverclockCooldownReduction,
        WhirlwindEnabled,
        LockedInEnabled,
        PoisonOnHitDuration,
        ConfusionOnHitDuration,
        FrailtyOnHitDuration,
        SuperchargedAmmoMultiplier,
        SuperchargedAmmoDuration,
        SpiteDamageMultiplierPerStack,
        SpiteMaxStacks,
        UnassumingProcChance,
        RangerBoltCount,
        BurstAdditionalBolts,
        AvariceHealthPerQualifyingUpgrade,
        NegativeStatusImmunity,
        UnsteadyPoisonStacks,
        EpidemicEnabled,
        ContagionEnabled,
        DeusExDamagePerUpgrade,
        CombatSpecialistMeleeMultiplier,
        CombatSpecialistRangedMultiplier
    }

    private PlayerStats _stats;
    private PlayerHealth _health;
    private PlayerDashController _dashController;
    private PlayerWeaponController _weapons;
    private StatusEffectManager _statusEffects;

    private IEventBinding<PlayerDamagedEvent> _playerDamagedBinding;
    private IEventBinding<PlayerDashedEvent> _playerDashedBinding;
    private IEventBinding<EnemyDamagedEvent> _enemyDamagedBinding;
    private IEventBinding<EnemyKilledEvent> _enemyKilledBinding;

    private float _lifestealPercent;
    private float _crowdPleaserDamagePerEnemy;
    private float _ballistaDamagePerTile;
    private float _bulwarkReductionPerEnemy;
    private float _berserkerDamagePerMissingHealth;
    private float _assassinDamageMultiplier;
    private float _assassinDuration;
    private float _overclockReduction;
    private int _whirlwindCount;
    private int _lockedInCount;
    private float _poisonOnHitDuration;
    private float _confusionOnHitDuration;
    private float _frailtyOnHitDuration;
    private float _superchargedAmmoMultiplier;
    private float _superchargedAmmoDuration;
    private float _spiteDamageMultiplierPerStack;
    private int _spiteMaxStacks;
    private float _unassumingProcChance;
    private int _rangerBoltCount;
    private int _burstAdditionalBolts;
    private float _avariceHealthPerUpgrade;
    private int _negativeStatusImmunityCount;
    private int _unsteadyPoisonStacks;
    private int _epidemicCount;
    private int _contagionCount;
    private float _deusExDamagePerUpgrade;
    private float _combatSpecialistMeleeMultiplier;
    private float _combatSpecialistRangedMultiplier;

    private float _lastDashTime = -999f;
    private int _spiteStacks;
    private float _appliedDynamicMaxHealthBonus;

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        _health = GetComponent<PlayerHealth>();
        _dashController = GetComponent<PlayerDashController>();
        _weapons = GetComponent<PlayerWeaponController>();
        _statusEffects = GetComponent<StatusEffectManager>();
    }

    private void OnEnable()
    {
        _playerDamagedBinding = EventBus<PlayerDamagedEvent>.Register(OnPlayerDamaged);
        _playerDashedBinding = EventBus<PlayerDashedEvent>.Register(OnPlayerDashed);
        _enemyDamagedBinding = EventBus<EnemyDamagedEvent>.Register(OnEnemyDamaged);
        _enemyKilledBinding = EventBus<EnemyKilledEvent>.Register(OnEnemyKilled);
    }

    private void OnDisable()
    {
        EventBus<PlayerDamagedEvent>.Unsubscribe(_playerDamagedBinding);
        EventBus<PlayerDashedEvent>.Unsubscribe(_playerDashedBinding);
        EventBus<EnemyDamagedEvent>.Unsubscribe(_enemyDamagedBinding);
        EventBus<EnemyKilledEvent>.Unsubscribe(_enemyKilledBinding);
    }

    public bool IsImmuneToNegativeStatuses => _negativeStatusImmunityCount > 0;
    public bool ShouldInflictedStatusesBePermanent => _contagionCount > 0;

    public void AddModifier(Modifier modifier, float amount)
    {
        switch (modifier)
        {
            case Modifier.LifestealPercent: _lifestealPercent += amount; break;
            case Modifier.CrowdPleaserDamagePerEnemy: _crowdPleaserDamagePerEnemy += amount; break;
            case Modifier.BallistaDamagePerTile: _ballistaDamagePerTile += amount; break;
            case Modifier.BulwarkReductionPerEnemy: _bulwarkReductionPerEnemy += amount; break;
            case Modifier.BerserkerDamagePerMissingHealth: _berserkerDamagePerMissingHealth += amount; break;
            case Modifier.AssassinDamageMultiplier: _assassinDamageMultiplier += amount; break;
            case Modifier.AssassinDuration: _assassinDuration += amount; break;
            case Modifier.OverclockCooldownReduction: _overclockReduction += amount; break;
            case Modifier.WhirlwindEnabled: _whirlwindCount += Mathf.RoundToInt(amount); break;
            case Modifier.LockedInEnabled: _lockedInCount += Mathf.RoundToInt(amount); break;
            case Modifier.PoisonOnHitDuration: _poisonOnHitDuration += amount; break;
            case Modifier.ConfusionOnHitDuration: _confusionOnHitDuration += amount; break;
            case Modifier.FrailtyOnHitDuration: _frailtyOnHitDuration += amount; break;
            case Modifier.SuperchargedAmmoMultiplier: _superchargedAmmoMultiplier += amount; break;
            case Modifier.SuperchargedAmmoDuration: _superchargedAmmoDuration += amount; break;
            case Modifier.SpiteDamageMultiplierPerStack: _spiteDamageMultiplierPerStack += amount; break;
            case Modifier.SpiteMaxStacks: _spiteMaxStacks += Mathf.RoundToInt(amount); break;
            case Modifier.UnassumingProcChance: _unassumingProcChance += amount; break;
            case Modifier.RangerBoltCount: _rangerBoltCount += Mathf.RoundToInt(amount); break;
            case Modifier.BurstAdditionalBolts: _burstAdditionalBolts += Mathf.RoundToInt(amount); break;
            case Modifier.AvariceHealthPerQualifyingUpgrade: _avariceHealthPerUpgrade += amount; break;
            case Modifier.NegativeStatusImmunity: _negativeStatusImmunityCount += Mathf.RoundToInt(amount); break;
            case Modifier.UnsteadyPoisonStacks: _unsteadyPoisonStacks += Mathf.RoundToInt(amount); break;
            case Modifier.EpidemicEnabled: _epidemicCount += Mathf.RoundToInt(amount); break;
            case Modifier.ContagionEnabled: _contagionCount += Mathf.RoundToInt(amount); break;
            case Modifier.DeusExDamagePerUpgrade: _deusExDamagePerUpgrade += amount; break;
            case Modifier.CombatSpecialistMeleeMultiplier: _combatSpecialistMeleeMultiplier += amount; break;
            case Modifier.CombatSpecialistRangedMultiplier: _combatSpecialistRangedMultiplier += amount; break;
        }

        SyncSpecialStates();
    }

    public AttackDamageSnapshot BuildAttackSnapshot(AttackKind attackKind, Vector2 origin, EnemyBase target, int nearbyEnemyCount)
    {
        float flatBonus = 0f;
        float multiplierBonus = 0f;
        bool consumeSpite = false;

        if (attackKind == AttackKind.Melee)
            flatBonus += _crowdPleaserDamagePerEnemy * nearbyEnemyCount;

        if ((attackKind == AttackKind.Ranged || attackKind == AttackKind.EnergyBolt) && target != null)
        {
            float tilesBetween = Mathf.Max(0f, Mathf.Round(Vector2.Distance(origin, target.transform.position)));
            flatBonus += _ballistaDamagePerTile * tilesBetween;
        }

        float missingHealth = _stats.MaxHealth - _health.CurrentHealth;
        flatBonus += Mathf.Max(0f, missingHealth) * _berserkerDamagePerMissingHealth;

        if (_lastDashTime + Mathf.Max(0f, _assassinDuration) >= Time.time)
            multiplierBonus += _assassinDamageMultiplier;

        if ((attackKind == AttackKind.Ranged || attackKind == AttackKind.EnergyBolt) &&
            _lastDashTime + Mathf.Max(0f, _superchargedAmmoDuration) >= Time.time)
        {
            multiplierBonus += _superchargedAmmoMultiplier;
        }

        if (_spiteStacks > 0 && _spiteDamageMultiplierPerStack > 0f)
        {
            multiplierBonus += _spiteStacks * _spiteDamageMultiplierPerStack;
            consumeSpite = true;
        }

        var manager = UpgradeManager.Instance;
        if (manager != null)
        {
            int rangedUpgradeCount = manager.CountUpgradesWithTrait(UpgradeTrait.Ranged, excludeUpgradeId: null);
            if (rangedUpgradeCount <= 0)
            {
                if (attackKind == AttackKind.Melee)
                    multiplierBonus += _combatSpecialistMeleeMultiplier;
            }
            else if (attackKind == AttackKind.Ranged || attackKind == AttackKind.EnergyBolt)
            {
                multiplierBonus += _combatSpecialistRangedMultiplier;
            }

            if (_deusExDamagePerUpgrade > 0f && manager.AreOnlyOtherUpgradesCommon("deus_ex_machina"))
                multiplierBonus += manager.GetTotalUpgradeCount() * _deusExDamagePerUpgrade;
        }

        return new AttackDamageSnapshot(flatBonus, multiplierBonus, consumeSpite);
    }

    public void NotifyAttackPerformed(AttackKind attackKind, AttackDamageSnapshot snapshot)
    {
        if (_whirlwindCount > 0 && _statusEffects != null)
            _statusEffects.Apply(StatusEffectIds.Swiftness, 1f, 3);

        if (snapshot.ConsumeSpiteStacks)
            _spiteStacks = 0;
    }

    public float GetIncomingDamageMultiplier()
    {
        float reduction = 0f;

        if (_lockedInCount > 0 && _health.CurrentHealth <= _stats.MaxHealth * 0.3f)
            reduction += 0.4f;

        if (_bulwarkReductionPerEnemy > 0f)
        {
            int nearbyEnemies = CountEnemiesInRange(Mathf.Max(0.5f, _stats.AttackRange));
            reduction += nearbyEnemies * _bulwarkReductionPerEnemy;
        }

        reduction = Mathf.Clamp(reduction, 0f, 0.9f);
        return 1f - reduction;
    }

    public void RefreshDynamicModifiers(UpgradeManager manager)
    {
        float desiredBonus = 0f;
        if (manager != null && _avariceHealthPerUpgrade > 0f)
            desiredBonus += manager.CountUpgradesByMinimumRarity(UpgradeRarity.Rare) * _avariceHealthPerUpgrade;

        if (!Mathf.Approximately(desiredBonus, _appliedDynamicMaxHealthBonus))
        {
            float delta = desiredBonus - _appliedDynamicMaxHealthBonus;
            _stats.AddFlat(PlayerStatType.MaxHealth, delta);
            _appliedDynamicMaxHealthBonus = desiredBonus;
        }

        SyncSpecialStates();
    }

    public int GetEnergyBoltCount()
    {
        return Mathf.Max(0, 1 + _burstAdditionalBolts);
    }

    private void SyncSpecialStates()
    {
        if (_statusEffects == null)
            return;

        if (_unsteadyPoisonStacks > 0)
            _statusEffects.SetPermanentStacks(StatusEffectIds.Poison, _unsteadyPoisonStacks);
        else
            _statusEffects.Remove(StatusEffectIds.Poison);
    }

    private void OnPlayerDamaged(PlayerDamagedEvent evt)
    {
        if (_spiteMaxStacks <= 0)
            return;

        _spiteStacks = Mathf.Min(_spiteMaxStacks, _spiteStacks + 1);
    }

    private void OnPlayerDashed(PlayerDashedEvent evt)
    {
        _lastDashTime = Time.time;

        if (_rangerBoltCount <= 0 || _weapons == null)
            return;

        _weapons.FireEnergyBoltsAtRandomEnemies(_rangerBoltCount * GetEnergyBoltCount());
    }

    private void OnEnemyDamaged(EnemyDamagedEvent evt)
    {
        if (!evt.Context.WasCausedByPlayer)
            return;

        if (_lifestealPercent > 0f && _health != null && evt.DamageDealt > 0f)
            _health.Heal(evt.DamageDealt * _lifestealPercent);

        var enemyStatuses = evt.Enemy ? evt.Enemy.StatusEffects : null;
        if (enemyStatuses == null)
            return;

        bool makePermanent = ShouldInflictedStatusesBePermanent;
        if (_poisonOnHitDuration > 0f)
            enemyStatuses.Apply(StatusEffectIds.Poison, _poisonOnHitDuration, int.MaxValue, makePermanent, evt.Context);
        if (_confusionOnHitDuration > 0f)
            enemyStatuses.Apply(StatusEffectIds.Confusion, _confusionOnHitDuration, 3, makePermanent, evt.Context);
        if (_frailtyOnHitDuration > 0f)
            enemyStatuses.Apply(StatusEffectIds.Frailty, _frailtyOnHitDuration, 1, makePermanent, evt.Context);

        if (_unassumingProcChance > 0f &&
            evt.Context.TriggersOnHitEffects &&
            _weapons != null &&
            Random.value <= _unassumingProcChance)
        {
            _weapons.FireEnergyBoltsAtRandomEnemies(GetEnergyBoltCount());
        }
    }

    private void OnEnemyKilled(EnemyKilledEvent evt)
    {
        if (evt.Context.WasCausedByPlayer && _overclockReduction > 0f)
            _dashController?.ReduceRemainingCooldown(_overclockReduction);

        if (_epidemicCount > 0 && evt.Enemy != null && evt.Enemy.StatusEffects != null)
        {
            EnemyBase closest = FindClosestOtherEnemy(evt.Enemy);
            if (closest != null)
                evt.Enemy.StatusEffects.TransferNegativeEffectsTo(closest.StatusEffects);
        }
    }

    private int CountEnemiesInRange(float range)
    {
        var enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        int count = 0;
        float rangeSq = range * range;
        Vector3 origin = transform.position;

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead)
                continue;

            if ((enemy.transform.position - origin).sqrMagnitude <= rangeSq)
                count++;
        }

        return count;
    }

    private static EnemyBase FindClosestOtherEnemy(EnemyBase source)
    {
        var enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        EnemyBase best = null;
        float bestDistance = float.PositiveInfinity;

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy == source || enemy.IsDead)
                continue;

            float distance = (enemy.transform.position - source.transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = enemy;
            }
        }

        return best;
    }
}
