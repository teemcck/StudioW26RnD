using UnityEngine;

public static class GameColors
{
    public static readonly Color DangerAoE = HexRGB(0xFF4A4A);
    public static readonly Color SafeDash = HexRGB(0x4AF0FF);
    public static readonly Color Reward = HexRGB(0xFFDA66);
    public static readonly Color EliteAccent = HexRGB(0xFFCC33);
    public static readonly Color HitNormal = HexRGB(0xFFFFFF);
    public static readonly Color HitCrit = HexRGB(0xFFBA3A);
    public static readonly Color HitShield = HexRGB(0x9BC5D4);
    public static readonly Color HitBossArmor = HexRGB(0xFF8032);
    public static readonly Color PerfectDodge = HexRGB(0x8BF3FF);
    public static readonly Color LowHpVignette = new(0.8f, 0.12f, 0.12f, 1f);

    public static Color HexRGB(int rgb)
    {
        float r = ((rgb >> 16) & 0xFF) / 255f;
        float g = ((rgb >> 8) & 0xFF) / 255f;
        float b = (rgb & 0xFF) / 255f;
        return new Color(r, g, b, 1f);
    }

    public static Color WithAlpha(this Color c, float a)
    {
        c.a = a;
        return c;
    }
}
