using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attached to UpgradeDisplay prefabs.
/// 
/// Provides interface for updating UI display as well as data
/// provided to the in-game tooltip.
/// </summary>
public class UpgradeDisplay : MonoBehaviour
{
    [SerializeField] private Image cardImage;
    private UpgradeDisplaySO _data;
    
    /// <summary>
    /// Stores input SO in _data, updates upgrade UI.
    /// </summary>
    /// <param name="display"></param>
   public void UpdateDisplay(UpgradeDisplaySO display)
   {
       // Store display data from SO.
       _data = display;
       cardImage.sprite = _data.cardImage;
   }
   
   // Public API, Lookup
   public UpgradeDisplaySO Data => _data;
}
