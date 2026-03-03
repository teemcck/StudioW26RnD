using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles upgrade menu UI elements.
///
/// Public API:
/// - Populate card upgrade displays via PopulateUpgradeOptions.
/// - Destory card upgrade displays via ClearUpgradeOptions
/// </summary>
public class UpgradeUIHandler : MonoBehaviour
{
    public static UpgradeUIHandler Instance { get; private set; }
    
    [SerializeField] private GameObject upgradeContainer;
    [SerializeField] private GameObject upgradeDisplayPrefab;
    
    // Stored UI elements pertaining to each upgrade option.
    
    [SerializeField] private List<GameObject> upgradeDisplays = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    /// <summary>
    /// Instantiates each option in upgradeOptions as a UI object.
    /// Stores data to associated upgrades.
    /// </summary>
    /// <param name="upgradeOptions"></param>
    public void PopulateUpgradeOptions(List<UpgradeDisplaySO> upgradeOptions)
    {
        int numOptions = upgradeOptions.Count;

        for (int i = 0; i < numOptions; i++)
        {
            // Instantiate upgrade display, feed it data.
            GameObject display = Instantiate(upgradeDisplayPrefab, upgradeContainer.transform);
            display.GetComponent<UpgradeDisplay>().UpdateDisplay(upgradeOptions[i]);
            upgradeDisplays.Add(display);
        }
    }
    
    /// <summary>
    /// Destroys all UI upgrade options in upgradeContainer.
    /// </summary>
    public void ClearUpgradeOptions()
    {
        foreach (var display in upgradeDisplays)
        {
            Destroy(display);
        }
    }
}
