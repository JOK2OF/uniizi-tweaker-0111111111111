using UniiziTweaker01.Core;
using Xunit;

namespace UniiziTweaker01.Tests;

public class ByteFormatterTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(-5, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1073741824, "1.0 GB")]
    public void Formats_values(long bytes, string expected) =>
        Assert.Equal(expected, ByteFormatter.Format(bytes));
}

public class UptimeFormatterTests
{
    [Fact] public void Days() => Assert.Equal("3d 4h 12m", UptimeFormatter.Format(new TimeSpan(3, 4, 12, 0)));
    [Fact] public void Hours() => Assert.Equal("4h 12m", UptimeFormatter.Format(new TimeSpan(4, 12, 0)));
    [Fact] public void Minutes() => Assert.Equal("1m 30s", UptimeFormatter.Format(TimeSpan.FromSeconds(90)));
    [Fact] public void Negative_is_zero() => Assert.Equal("0m 0s", UptimeFormatter.Format(TimeSpan.FromSeconds(-4)));
}

public class CpuUsageCalculatorTests
{
    [Fact]
    public void First_sample_has_no_result()
    {
        var calc = new CpuUsageCalculator();
        Assert.Null(calc.Update(100, 200, 100));
    }

    [Fact]
    public void Computes_busy_percentage_from_deltas()
    {
        var calc = new CpuUsageCalculator();
        calc.Update(100, 200, 100);
        // idle +50, kernel(+idle) +80, user +20 => total 100, busy 50
        double? result = calc.Update(150, 280, 120);
        Assert.NotNull(result);
        Assert.Equal(50.0, result!.Value, 3);
    }

    [Fact]
    public void No_elapsed_time_returns_null()
    {
        var calc = new CpuUsageCalculator();
        calc.Update(10, 20, 10);
        Assert.Null(calc.Update(10, 20, 10));
    }
}

public class SampleHistoryTests
{
    [Fact]
    public void Keeps_only_capacity_latest_values()
    {
        var h = new SampleHistory(3);
        for (int i = 1; i <= 5; i++) h.Add(i);
        Assert.Equal(new double[] { 3, 4, 5 }, h.ToArray());
    }

    [Fact]
    public void Rejects_invalid_capacity() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new SampleHistory(0));
}

public class RollbackCountdownTests
{
    [Fact]
    public void Expires_after_requested_ticks()
    {
        var c = new RollbackCountdown(2);
        Assert.False(c.Expired);
        c.Tick();
        Assert.False(c.Expired);
        c.Tick();
        Assert.True(c.Expired);
        c.Tick();
        Assert.Equal(0, c.Remaining);
    }
}

public class DisplayModeCatalogTests
{
    private static readonly DisplayMode[] Modes =
    {
        new(1920, 1080, 60, 32), new(1920, 1080, 144, 32), new(1280, 720, 60, 32),
        new(1000, 700, 60, 32), new(3840, 2160, 30, 32),
    };

    [Fact]
    public void Distinct_resolutions_are_sorted_descending()
    {
        var r = DisplayModeCatalog.DistinctResolutions(Modes);
        Assert.Equal(new[] { new Resolution(3840, 2160), new Resolution(1920, 1080), new Resolution(1280, 720), new Resolution(1000, 700) }, r);
    }

    [Fact]
    public void Refresh_rates_are_distinct_and_highest_first() =>
        Assert.Equal(new[] { 144, 60 }, DisplayModeCatalog.RefreshRatesFor(Modes, new Resolution(1920, 1080)));

    [Fact]
    public void Presets_are_filtered_against_real_modes()
    {
        var presets = DisplayModeCatalog.SupportedPresets(Modes);
        Assert.Contains(new Resolution(1920, 1080), presets);
        Assert.DoesNotContain(new Resolution(2560, 1440), presets);
        Assert.DoesNotContain(new Resolution(1000, 700), presets);
    }

    [Fact]
    public void Options_flag_common_presets_only()
    {
        var options = DisplayModeCatalog.BuildOptions(Modes);
        Assert.True(options.Single(o => o.Resolution == new Resolution(1920, 1080)).IsCommonPreset);
        Assert.False(options.Single(o => o.Resolution == new Resolution(1000, 700)).IsCommonPreset);
    }

    [Fact]
    public void Unsupported_modes_are_rejected()
    {
        Assert.True(DisplayModeCatalog.IsSupported(Modes, new DisplayMode(1920, 1080, 144, 32)));
        Assert.False(DisplayModeCatalog.IsSupported(Modes, new DisplayMode(1920, 1080, 240, 32)));
        Assert.False(DisplayModeCatalog.IsSupported(Modes, new DisplayMode(2560, 1440, 60, 32)));
    }

    [Fact]
    public void Pick_refresh_rate_prefers_requested_else_highest()
    {
        var res = new Resolution(1920, 1080);
        Assert.Equal(60, DisplayModeCatalog.PickRefreshRate(Modes, res, 60));
        Assert.Equal(144, DisplayModeCatalog.PickRefreshRate(Modes, res, 75));
        Assert.Null(DisplayModeCatalog.PickRefreshRate(Modes, new Resolution(2560, 1440), 60));
    }

    [Fact]
    public void Snapshot_percentages_are_computed()
    {
        var s = new SystemSnapshot(DateTime.Now, 10, 1000, 250, 2000, 500, 5, TimeSpan.Zero);
        Assert.Equal(75.0, s.RamPercent, 3);
        Assert.Equal(75.0, s.DiskUsedPercent!.Value, 3);
    }
}
