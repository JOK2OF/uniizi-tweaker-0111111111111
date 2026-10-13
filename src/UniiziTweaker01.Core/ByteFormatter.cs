using System.Globalization;

namespace UniiziTweaker01.Core;

/// <summary>Formats byte counts for display (binary units, invariant culture).</summary>
public static class ByteFormatter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

    public static string Format(long bytes)
    {
        if (bytes <= 0) return "0 B";
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        string format = unit == 0 ? "0" : "0.0";
        return $"{value.ToString(format, CultureInfo.InvariantCulture)} {Units[unit]}";
    }
}
