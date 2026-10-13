namespace UniiziTweaker01.Core;

public readonly record struct Resolution(int Width, int Height)
{
    public override string ToString() => $"{Width} × {Height}";
}

/// <summary>A display mode as reported by Windows / the display driver.</summary>
public sealed record DisplayMode(int Width, int Height, int RefreshRate, int BitsPerPixel)
{
    public Resolution Resolution => new(Width, Height);

    public override string ToString() => $"{Width} × {Height} @ {RefreshRate} Hz";
}

/// <summary>A selectable resolution; IsCommonPreset marks the well-known presets supported by the display.</summary>
public sealed record ResolutionOption(Resolution Resolution, bool IsCommonPreset)
{
    public string Label => IsCommonPreset ? $"{Resolution} ★" : Resolution.ToString();
}
