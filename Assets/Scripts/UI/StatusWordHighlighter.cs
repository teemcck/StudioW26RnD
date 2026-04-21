using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public static class StatusWordHighlighter
{
    private static readonly Regex _keywordRegex = BuildRegex();

    private static Regex BuildRegex()
    {
        var sb = new StringBuilder("\\b(");
        for (int i = 0; i < StatusEffectCatalog.Keywords.Length; i++)
        {
            if (i > 0) sb.Append('|');
            sb.Append(Regex.Escape(StatusEffectCatalog.Keywords[i]));
        }
        sb.Append(")\\b");
        return new Regex(sb.ToString(), RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    public static string Highlight(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        return _keywordRegex.Replace(input, static match =>
        {
            string word = match.Value;
            string linkId = word.ToLowerInvariant();
            string tint = StatusEffectCatalog.TryGet(linkId, out var entry)
                ? ColorUtility.ToHtmlStringRGB(entry.Accent)
                : "FFFFFF";
            return $"<link=\"{linkId}\"><u><color=#{tint}>{word}</color></u></link>";
        });
    }
}
