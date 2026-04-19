using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles upgrade menu UI elements.
/// When the player clicks a card, raises UpgradeSelectedEvent and hides the menu.
/// </summary>
public class UpgradeUIHandler : MonoBehaviour
{
    public static UpgradeUIHandler Instance { get; private set; }

    [SerializeField] private GameObject upgradeContainer;
    [SerializeField] private GameObject upgradeDisplayPrefab;
    [SerializeField] private GameObject upgradeCanvas;

    private List<GameObject> _upgradeDisplays = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Shows the upgrade canvas and populates it with the given options.
    /// </summary>
    public void DisplayUpgrades(List<UpgradeDisplaySO> upgradeOptions)
    {
        if (!upgradeCanvas.activeSelf) upgradeCanvas.SetActive(true);
        PopulateUpgradeOptions(upgradeOptions);
    }

    /// <summary>
    /// Destroys all displayed upgrade cards and hides the canvas.
    /// </summary>
    public void HideUpgrades()
    {
        ClearUpgradeOptions();
        if (upgradeCanvas.activeSelf) upgradeCanvas.SetActive(false);
    }

    private void PopulateUpgradeOptions(List<UpgradeDisplaySO> upgradeOptions)
    {
        foreach (var option in upgradeOptions)
        {
            GameObject displayGO = Instantiate(upgradeDisplayPrefab, upgradeContainer.transform);
            UpgradeDisplay display = displayGO.GetComponent<UpgradeDisplay>();
            display.UpdateDisplay(option);

            // Capture for the lambda.
            UpgradeDisplaySO captured = option;
            display.OnClicked = () => OnUpgradeCardClicked(captured);

            _upgradeDisplays.Add(displayGO);
        }
    }

    private void OnUpgradeCardClicked(UpgradeDisplaySO selected)
    {
        int newStack = UpgradeManager.Instance.ApplyUpgradeFromDisplay(selected)
            ? UpgradeManager.Instance.GetStack(selected.upgradeID)
            : 0;

        AudioManager.Instance?.PlayUpgradeSelected();

        EventBus<UpgradeSelectedEvent>.Raise(new UpgradeSelectedEvent
        {
            UpgradeID     = selected.upgradeID,
            UpgradeName   = selected.upgradeName,
            NewStackCount = newStack
        });

        HideUpgrades();
    }

    private void ClearUpgradeOptions()
    {
        foreach (var display in _upgradeDisplays)
            Destroy(display);

        _upgradeDisplays.Clear();
    }
}
