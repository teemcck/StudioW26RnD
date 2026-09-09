using UnityEngine;

/// <summary>
/// Lazily built solid-white sprites for code-spawned feedback effects (hit pulses, muzzle flashes, rubble,
/// health bars). One shared instance per size so the same sprite is not re-created per class.
/// </summary>
public static class RuntimeSprites
{
    private static Sprite s_white;
    private static Sprite s_unitWhite;

    /// <summary>White sprite at 100 pixels-per-unit (Texture2D.whiteTexture is 4x4, so 0.04 world units). Scale via Transform.</summary>
    public static Sprite White
    {
        get
        {
            if (s_white == null)
                s_white = Create(100f);
            return s_white;
        }
    }

    /// <summary>White sprite that is exactly 1 world unit wide, convenient for bars sized through localScale.</summary>
    public static Sprite UnitWhite
    {
        get
        {
            if (s_unitWhite == null)
                s_unitWhite = Create(Texture2D.whiteTexture.width);
            return s_unitWhite;
        }
    }

    private static Sprite Create(float pixelsPerUnit)
    {
        Texture2D texture = Texture2D.whiteTexture;
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
    }
}
