using UnityEngine;

/// <summary>
/// Purely presentational upgrade data, what the card UI displays.
/// No game logic lives here.
///
/// Paired to an UpgradeEffectSO via matching upgradeID strings.
/// Lives in Assets/Upgrades/Display/.
/// </summary>
[CreateAssetMenu(fileName = "UpgradeDisplay_", menuName = "Upgrades/Upgrade Display")]
public class UpgradeDisplaySO : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Must exactly match the ID on the paired UpgradeEffectSO.")]
    public string upgradeID;

    [Header("Card Face")]
    public string upgradeName;

    [TextArea(2, 5)]
    [Tooltip("Leave blank to auto-generate from UpgradeEffectSO.BuildAutoDescription().")]
    public string upgradeDescription;

    [Tooltip("Art shown on the upgrade card.")]
    public Sprite cardImage;

    [Header("Upgrade Meta")]
    public UpgradeRarity rarity;
    public UpgradeCategory category;
}

public enum UpgradeRarity   { Common, Uncommon, Rare, Legendary }
public enum UpgradeCategory { Movement, Combat, Defence, Utility, Special }
