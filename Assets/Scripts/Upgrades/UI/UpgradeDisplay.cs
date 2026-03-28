using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Attached to UpgradeDisplay prefabs.
/// Displays card data and exposes an OnClicked callback
/// wired by UpgradeUIHandler when the card is instantiated.
/// </summary>
public class UpgradeDisplay : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image cardImage;

    private UpgradeDisplaySO _data;

    /// <summary>Set by UpgradeUIHandler after instantiation.</summary>
    public Action OnClicked { get; set; }

    public void UpdateDisplay(UpgradeDisplaySO display)
    {
        _data = display;
        cardImage.sprite = _data.cardImage;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClicked?.Invoke();
    }

    public UpgradeDisplaySO Data => _data;
}