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
    public bool isPermanent;
    
    [HideInInspector] public float timeRemaining;
    [HideInInspector] public int currentStacks;
    
    public StatusEffect(string id, float duration, int maxStacks = 1, bool isPermanent = false)
    {
        this.id = id;
        this.duration = duration;
        this.maxStacks = maxStacks;
        this.isPermanent = isPermanent;
        this.timeRemaining = isPermanent ? float.PositiveInfinity : duration;
        this.currentStacks = 1;
    }
    
    public void Refresh()
    {
        timeRemaining = isPermanent ? float.PositiveInfinity : duration;
    }
    
    public void AddStack()
    {
        if (currentStacks < maxStacks)
            currentStacks++;
        Refresh();
    }
    
    public bool IsExpired => timeRemaining <= 0f;

    public StatusEffect Clone()
    {
        var copy = new StatusEffect(id, duration, maxStacks, isPermanent)
        {
            currentStacks = currentStacks,
            timeRemaining = timeRemaining
        };
        return copy;
    }
}
