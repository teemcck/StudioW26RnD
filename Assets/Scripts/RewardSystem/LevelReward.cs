using UnityEngine;
using System.Collections.Generic;

public class LevelReward : MonoBehaviour
{
    private UpgradeManager _upgradeManager;
    private UpgradeUIHandler _upgradeUIHandler;

    private void Awake()
    {
        _upgradeManager = UpgradeManager.Instance;
        _upgradeUIHandler = UpgradeUIHandler.Instance;
    }

    public void ActivateUpgradeSelection(int numChoices)
    {
        List<UpgradeDisplaySO> upgrades = _upgradeManager.GetRandomUpgradeChoices(numChoices);
        _upgradeUIHandler.DisplayUpgrades(upgrades);
    }
}
