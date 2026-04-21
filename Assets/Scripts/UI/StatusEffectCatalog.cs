using System.Collections.Generic;
using UnityEngine;

public static class StatusEffectCatalog
{
    public readonly struct Entry
    {
        public readonly string Title;
        public readonly string Body;
        public readonly Color Accent;

        public Entry(string title, string body, Color accent)
        {
            Title = title;
            Body = body;
            Accent = accent;
        }
    }

    private static readonly Dictionary<string, Entry> _entries = new()
    {
        ["poison"] = new Entry(
            "Poison",
            "Take 2 damage every second for each stack of poison (unlimited stacks).",
            new Color(0.50f, 0.87f, 0.33f, 1f)),
        ["confusion"] = new Entry(
            "Confusion",
            "Attack and movement speed is decreased by 15% (max of 3 stacks).",
            new Color(0.70f, 0.45f, 1.00f, 1f)),
        ["frailty"] = new Entry(
            "Frailty",
            "Instantly dies when health falls under 15% (max of 1 stack).",
            new Color(1.00f, 0.55f, 0.35f, 1f)),
    };

    public static readonly string[] Keywords = { "poison", "confusion", "frailty" };

    public static bool TryGet(string key, out Entry entry)
    {
        if (string.IsNullOrEmpty(key)) { entry = default; return false; }
        return _entries.TryGetValue(key.ToLowerInvariant(), out entry);
    }

    public static IEnumerable<string> Keys => _entries.Keys;
}
