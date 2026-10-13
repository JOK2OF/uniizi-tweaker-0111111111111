namespace UniiziTweaker01.Core;

/// <summary>Pure logic for filtering and grouping display modes. Never invents modes.</summary>
public static class DisplayModeCatalog
{
    public static readonly IReadOnlyList<Resolution> CommonPresets = new[]
    {
        new Resolution(800, 600), new Resolution(1024, 768), new Resolution(1280, 720),
        new Resolution(1280, 800), new Resolution(1366, 768), new Resolution(1440, 900),
        new Resolution(1600, 900), new Resolution(1680, 1050), new Resolution(1920, 1080),
        new Resolution(1920, 1200), new Resolution(2560, 1080), new Resolution(2560, 1440),
        new Resolution(2560, 1600), new Resolution(3440, 1440), new Resolution(3840, 2160),
    };

    public static IReadOnlyList<Resolution> DistinctResolutions(IEnumerable<DisplayMode> modes) =>
        modes.Select(m => m.Resolution).Distinct()
             .OrderByDescending(r => r.Width).ThenByDescending(r => r.Height).ToList();

    public static IReadOnlyList<int> RefreshRatesFor(IEnumerable<DisplayMode> modes, Resolution resolution) =>
        modes.Where(m => m.Resolution == resolution).Select(m => m.RefreshRate).Distinct()
             .OrderByDescending(r => r).ToList();

    /// <summary>Common presets that the display actually supports.</summary>
    public static IReadOnlyList<Resolution> SupportedPresets(IEnumerable<DisplayMode> modes)
    {
        var supported = DistinctResolutions(modes).ToHashSet();
        return CommonPresets.Where(supported.Contains).ToList();
    }

    public static IReadOnlyList<ResolutionOption> BuildOptions(IEnumerable<DisplayMode> modes)
    {
        var presets = CommonPresets.ToHashSet();
        return DistinctResolutions(modes).Select(r => new ResolutionOption(r, presets.Contains(r))).ToList();
    }

    public static bool IsSupported(IEnumerable<DisplayMode> modes, DisplayMode target) =>
        modes.Any(m => m.Width == target.Width && m.Height == target.Height && m.RefreshRate == target.RefreshRate);

    public static int? PickRefreshRate(IEnumerable<DisplayMode> modes, Resolution resolution, int? preferred)
    {
        var rates = RefreshRatesFor(modes, resolution);
        if (rates.Count == 0) return null;
        if (preferred is int p && rates.Contains(p)) return p;
        return rates[0];
    }
}
