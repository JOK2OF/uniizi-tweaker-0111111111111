namespace UniiziTweaker01.Core;

/// <summary>One real measurement of the machine. Null means "not measurable / unavailable".</summary>
public sealed record SystemSnapshot(
    DateTime Timestamp,
    double? CpuPercent,
    long TotalRamBytes,
    long AvailableRamBytes,
    long? SystemDriveTotalBytes,
    long? SystemDriveFreeBytes,
    int ProcessCount,
    TimeSpan Uptime)
{
    public long UsedRamBytes => Math.Max(0, TotalRamBytes - AvailableRamBytes);

    public double RamPercent => TotalRamBytes > 0 ? UsedRamBytes * 100.0 / TotalRamBytes : 0;

    public double? DiskUsedPercent =>
        SystemDriveTotalBytes is long total && total > 0 && SystemDriveFreeBytes is long free
            ? (total - free) * 100.0 / total
            : null;
}
