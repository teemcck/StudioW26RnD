using UnityEngine;
using System.Collections.Generic;

public class LevelReward : MonoBehaviour
{
    private UpgradeManager _upgradeManager;
    private UpgradeUIHandler _upgradeUIHandler;

    private IEventBinding<UpgradeSelectedEvent> _upgradeSelectedBinding;

    // There is no performance variability implemented as of now.
    // In the future, the xp * xp multiplier will determine the level rewards.
    // Not yet implemented since there are concerns with this, some builds might
    // "prioritize" certain common cards, so doing better would be a decrement to the run
    // if xp meant higher chance for less common cards.

    private void Awake()
    {
        _upgradeManager    = UpgradeManager.Instance;
        _upgradeUIHandler  = UpgradeUIHandler.Instance;
    }

    private void OnEnable()
    {
        _upgradeSelectedBinding = EventBus<UpgradeSelectedEvent>.Register(OnUpgradeSelected);
    }

    private void OnDisable()
    {
        EventBus<UpgradeSelectedEvent>.Unsubscribe(_upgradeSelectedBinding);
    }

    public void ActivateUpgradeSelection(int numChoices)
    {
        List<UpgradeDisplaySO> upgrades = _upgradeManager.GetRandomUpgradeChoices(numChoices);
        _upgradeUIHandler.DisplayUpgrades(upgrades);
    }

    private void OnUpgradeSelected(UpgradeSelectedEvent evt)
    {
        Debug.Log($"Upgrade selected: {evt.UpgradeName} (x{evt.NewStackCount})");
        // Forward to UpgradeManager or apply directly once that API exists.
    }
}
