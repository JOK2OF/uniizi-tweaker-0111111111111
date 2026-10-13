#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace UniiziTweaker01.Core.CacheCleaning;

/// <summary>Counters for one category, used for both scans (what could be removed) and cleanups (what was removed).</summary>
public sealed class CleanupTally
{
    public const int MaxDetails = 20;

    private readonly Dictionary<SkipReason, long> _skipped = new Dictionary<SkipReason, long>();
    private readonly List<string> _details = new List<string>();

    public long Files { get; private set; }

    public long Bytes { get; private set; }

    public long FoldersRemoved { get; private set; }

    public long SkippedItems { get; private set; }

    public IReadOnlyDictionary<SkipReason, long> SkippedByReason => _skipped;

    public IReadOnlyList<string> Details => _details;

    public void AddHandled(long bytes, long files = 1)
    {
        Files += files;
        if (bytes > 0) Bytes += bytes;
    }

    public void AddFolderRemoved() => FoldersRemoved++;

    public void AddSkipped(SkipReason reason, string? detail = null)
    {
        SkippedItems++;
        _skipped.TryGetValue(reason, out var n);
        _skipped[reason] = n + 1;
        if (!string.IsNullOrEmpty(detail) && (reason == SkipReason.AccessDenied || reason == SkipReason.IoError))
        {
            AddDetail(detail!);
        }
    }

    public void AddDetail(string detail)
    {
        if (_details.Count < MaxDetails) _details.Add(detail);
    }
}

public sealed record CategoryResult(string CategoryId, string CategoryName, CleanupTally Tally, string Message);

public sealed record CleanupRun(IReadOnlyList<CategoryResult> Results, bool Cancelled);

public sealed record CleanupTotals(
    long Files,
    long Bytes,
    long FoldersRemoved,
    long SkippedItems,
    IReadOnlyDictionary<SkipReason, long> SkippedByReason)
{
    public static CleanupTotals From(IEnumerable<CategoryResult> results)
    {
        long files = 0, bytes = 0, folders = 0, skipped = 0;
        var byReason = new Dictionary<SkipReason, long>();
        foreach (var r in results)
        {
            files += r.Tally.Files;
            bytes += r.Tally.Bytes;
            folders += r.Tally.FoldersRemoved;
            skipped += r.Tally.SkippedItems;
            foreach (var kv in r.Tally.SkippedByReason)
            {
                byReason.TryGetValue(kv.Key, out var n);
                byReason[kv.Key] = n + kv.Value;
            }
        }
        return new CleanupTotals(files, bytes, folders, skipped, byReason);
    }
}

public static class SizeText
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public static string Format(long bytes)
    {
        if (bytes <= 0) return "0 B";
        double v = bytes;
        var i = 0;
        while (v >= 1024 && i < Units.Length - 1)
        {
            v /= 1024;
            i++;
        }
        return i == 0
            ? bytes.ToString(CultureInfo.InvariantCulture) + " B"
            : v.ToString("0.0", CultureInfo.InvariantCulture) + " " + Units[i];
    }
}

public sealed record CleanupHistoryEntry(
    DateTime TimestampUtc,
    string[] Categories,
    long FilesDeleted,
    long BytesFreed,
    long FilesSkipped,
    bool Cancelled);

public static class CleanupHistory
{
    public const int MaxEntries = 50;

    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { WriteIndented = true };

    public static string Serialize(IEnumerable<CleanupHistoryEntry> entries) =>
        JsonSerializer.Serialize(entries.ToList(), Options);

    public static List<CleanupHistoryEntry> Deserialize(string json)
    {
        try
        {
            var list = JsonSerializer.Deserialize<List<CleanupHistoryEntry>>(json);
            if (list == null) return new List<CleanupHistoryEntry>();
            return list.Where(e => e != null).ToList();
        }
        catch (JsonException)
        {
            return new List<CleanupHistoryEntry>();
        }
    }

    /// <summary>Newest first, trimmed to <see cref="MaxEntries"/>.</summary>
    public static List<CleanupHistoryEntry> Append(IEnumerable<CleanupHistoryEntry> existing, CleanupHistoryEntry entry)
    {
        var list = new List<CleanupHistoryEntry> { entry };
        list.AddRange(existing);
        if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);
        return list;
    }
}
