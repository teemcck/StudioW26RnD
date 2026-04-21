using System.Collections.Generic;

public static class StartupUpgradeDebugState
{
    public static bool InfiniteHealthEnabled { get; set; }

    public readonly struct ConfiguredUpgrade
    {
        public ConfiguredUpgrade(string upgradeId, int stackCount)
        {
            UpgradeId = upgradeId;
            StackCount = stackCount;
        }

        public string UpgradeId { get; }
        public int StackCount { get; }
    }

    private static readonly Dictionary<string, int> CountsByUpgradeId = new();

    public static int GetCount(string upgradeId)
    {
        return !string.IsNullOrEmpty(upgradeId) && CountsByUpgradeId.TryGetValue(upgradeId, out int count) ? count : 0;
    }

    public static void SetCount(string upgradeId, int count)
    {
        if (string.IsNullOrEmpty(upgradeId))
            return;

        if (count <= 0)
            CountsByUpgradeId.Remove(upgradeId);
        else
            CountsByUpgradeId[upgradeId] = count;
    }

    public static void Clear()
    {
        CountsByUpgradeId.Clear();
        InfiniteHealthEnabled = false;
    }

    public static List<ConfiguredUpgrade> GetConfiguredUpgrades()
    {
        var configured = new List<ConfiguredUpgrade>(CountsByUpgradeId.Count);
        foreach (var pair in CountsByUpgradeId)
            configured.Add(new ConfiguredUpgrade(pair.Key, pair.Value));
        return configured;
    }

    public static void AlignWithUpgradeManager(UpgradeManager manager)
    {
        if (manager == null)
            return;

        foreach (var display in manager.GetAllUpgradeDisplays())
        {
            if (display == null || string.IsNullOrEmpty(display.upgradeID))
                continue;
            SetCount(display.upgradeID, manager.GetStack(display.upgradeID));
        }
    }
}
