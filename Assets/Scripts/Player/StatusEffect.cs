using UnityEngine;

/// <summary>
/// Represents a temporary status effect (buff/debuff) applied to the player.
/// Examples: Swiftness, Poison, Freeze, Shield, etc.
/// </summary>
[System.Serializable]
public class StatusEffect
{
    public string id;
    public float duration;
    public int maxStacks = 1;
    
    [HideInInspector] public float timeRemaining;
    [HideInInspector] public int currentStacks;
    
    public StatusEffect(string id, float duration, int maxStacks = 1)
    {
        this.id = id;
        this.duration = duration;
        this.maxStacks = maxStacks;
        this.timeRemaining = duration;
        this.currentStacks = 1;
    }
    
    public void Refresh()
    {
        timeRemaining = duration;
    }
    
    public void AddStack()
    {
        if (currentStacks < maxStacks)
            currentStacks++;
        Refresh();
    }
    
    public bool IsExpired => timeRemaining <= 0f;
}
