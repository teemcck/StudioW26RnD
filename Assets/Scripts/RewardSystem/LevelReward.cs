using System;
using System.Collections.Generic;
using UnityEngine;

public class LevelReward  : MonoBehaviour
{
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private UpgradeUIHandler upgradeUIHandler;
    
    // There is no performance variability implemented as of now.
    // In the future, the xp * xp multiplier will determine the level rewards.
    // Not yet implemented since there are concerns with this, some builds might
    // "prioritize" certain common cards, so doing better would be a decrement to the run
    // if xp meant higher chance for less common cards.

    private void Start()
    {
        upgradeManager = UpgradeManager.Instance;
        upgradeUIHandler = UpgradeUIHandler.Instance;
        ActivateUpgradeSelection(3); // Testing.
    }

    private void ActivateUpgradeSelection(int numChoices)
    {
        List<UpgradeDisplaySO> upgrades = upgradeManager.GetRandomUpgradeChoices(numChoices);
        upgradeUIHandler.DisplayUpgrades(upgrades);
    }
}
