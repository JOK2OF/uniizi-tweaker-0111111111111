#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UniiziTweaker01.Core.CacheCleaning;

namespace UniiziTweaker01.Services;

public sealed record CleanProgress(string CategoryName, long Items, long Bytes, string Phase);

/// <summary>
/// Windows side of the Cache Cleaner: resolves folders, walks them without following links,
/// asks CleanupPolicy (Core) for every decision, deletes, and reports exactly what happened.
/// Only one scan/clean can run at a time.
/// </summary>
public sealed class CacheCleanerService
{
    private const int ProgressEvery = 250;
    private static int _running;

    public static string HistoryPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UNIIZI", "TWEAKER01", "cache-cleaner-history.json");

    public Task<CleanupRun> ScanAsync(IReadOnlyList<CacheCategory> categories, IProgress<CleanProgress>? progress, CancellationToken ct) =>
        Task.Run(() => Execute(categories, false, progress, ct));

    public Task<CleanupRun> CleanAsync(IReadOnlyList<CacheCategory> categories, IProgress<CleanProgress>? progress, CancellationToken ct) =>
        Task.Run(() => Execute(categories, true, progress, ct));

    public IReadOnlyList<CleanupHistoryEntry> LoadHistory()
    {
        try
        {
            if (!File.Exists(HistoryPath)) return Array.Empty<CleanupHistoryEntry>();
            return CleanupHistory.Deserialize(File.ReadAllText(HistoryPath, Encoding.UTF8));
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            return Array.Empty<CleanupHistoryEntry>();
        }
    }

    public bool AppendHistory(CleanupHistoryEntry entry)
    {
        try
        {
            var dir = Path.GetDirectoryName(HistoryPath);
            if (string.IsNullOrEmpty(dir)) return false;
            Directory.CreateDirectory(dir);
            var updated = CleanupHistory.Append(LoadHistory(), entry);
            var tmp = HistoryPath + ".tmp";
            File.WriteAllText(tmp, CleanupHistory.Serialize(updated), Encoding.UTF8);
            File.Move(tmp, HistoryPath, true);
            return true;
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            return false;
        }
    }

    private static CleanupRun Execute(IReadOnlyList<CacheCategory> categories, bool delete, IProgress<CleanProgress>? progress, CancellationToken ct)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            throw new InvalidOperationException("Another cache cleaner operation is already running.");
        }

        try
        {
            var results = new List<CategoryResult>();
            var cancelled = false;
            var forbidden = BuildForbiddenTrees();
            var tooBroad = BuildTooBroadRoots();

            foreach (var cat in categories)
            {
                if (ct.IsCancellationRequested)
                {
                    cancelled = true;
                    break;
                }

                if (cat.IsRecycleBin)
                {
                    results.Add(RecycleBin(cat, delete));
                    continue;
                }

                var tally = new CleanupTally();
                var wasCancelled = ProcessCategory(cat, delete, forbidden, tooBroad, progress, ct, tally);
                results.Add(new CategoryResult(cat.Id, cat.Name, tally, wasCancelled ? "Cancelled - partial result." : string.Empty));
                if (wasCancelled)
                {
                    cancelled = true;
                    break;
                }
            }

            return new CleanupRun(results, cancelled);
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    // Returns true if cancelled.
    private static bool ProcessCategory(
        CacheCategory cat, bool delete, IReadOnlyList<string> forbidden, IReadOnlyList<string> tooBroad,
        IProgress<CleanProgress>? progress, CancellationToken ct, CleanupTally tally)
    {
        var roots = ResolveRoots(cat, forbidden, tooBroad, tally);
        if (roots.Count == 0) return false;

        var rules = new CleanupRules(roots, cat.MinAge);
        var nowUtc = DateTime.UtcNow;
        var phase = delete ? "Cleaning" : "Scanning";
        long counter = 0;
        var visitedDirs = new List<DirectoryInfo>();

        foreach (var root in roots)
        {
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                if (ct.IsCancellationRequested) return true;
                var dir = stack.Pop();

                foreach (var entry in ListEntries(dir, tally))
                {
                    if (ct.IsCancellationRequested) return true;
                    counter++;
                    if (counter % ProgressEvery == 0)
                    {
                        progress?.Report(new CleanProgress(cat.Name, counter, tally.Bytes, phase));
                    }

                    if (entry is DirectoryInfo di)
                    {
                        if (IsReparse(di))
                        {
                            tally.AddSkipped(SkipReason.ReparsePoint);
                        }
                        else
                        {
                            stack.Push(di.FullName);
                            visitedDirs.Add(di);
                        }
                    }
                    else if (entry is FileInfo fi)
                    {
                        HandleFile(fi, rules, delete, nowUtc, tally);
                    }
                }
            }
        }

        progress?.Report(new CleanProgress(cat.Name, counter, tally.Bytes, phase));

        if (delete) RemoveEmptyFolders(visitedDirs, cat.MinAge, nowUtc, tally, ct);
        return false;
    }

    private static List<FileSystemInfo> ListEntries(string dir, CleanupTally tally)
    {
        var list = new List<FileSystemInfo>();
        try
        {
            foreach (var e in new DirectoryInfo(dir).EnumerateFileSystemInfos())
            {
                list.Add(e);
            }
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            RecordFailure(ex, dir, tally);
        }
        return list;
    }

    private static void HandleFile(FileInfo fi, CleanupRules rules, bool delete, DateTime nowUtc, CleanupTally tally)
    {
        FileCandidate candidate;
        long length;
        try
        {
            var attrs = fi.Attributes;
            length = fi.Length;
            candidate = new FileCandidate(
                fi.FullName, length, fi.LastWriteTimeUtc,
                (attrs & FileAttributes.ReparsePoint) != 0,
                (attrs & FileAttributes.System) != 0,
                (attrs & FileAttributes.ReadOnly) != 0);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            RecordFailure(ex, fi.FullName, tally);
            return;
        }

        var decision = CleanupPolicy.Decide(candidate, rules, nowUtc);
        if (!decision.ShouldDelete)
        {
            tally.AddSkipped(decision.Reason);
            return;
        }

        if (!delete)
        {
            tally.AddHandled(length);
            return;
        }

        try
        {
            File.Delete(fi.FullName);
            tally.AddHandled(length);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            RecordFailure(ex, fi.FullName, tally);
        }
    }

    private static void RemoveEmptyFolders(List<DirectoryInfo> visited, TimeSpan minAge, DateTime nowUtc, CleanupTally tally, CancellationToken ct)
    {
        // Deepest first. LastWriteTimeUtc is the value cached at enumeration time (before we deleted children).
        foreach (var di in visited.OrderByDescending(d => d.FullName.Length))
        {
            if (ct.IsCancellationRequested) return;
            try
            {
                if ((di.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                if (minAge > TimeSpan.Zero && nowUtc - di.LastWriteTimeUtc < minAge) continue;
                Directory.Delete(di.FullName, false); // non-recursive: fails if not empty
                tally.AddFolderRemoved();
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                // not empty or in use: leave it alone
            }
        }
    }

    private static List<string> ResolveRoots(CacheCategory cat, IReadOnlyList<string> forbidden, IReadOnlyList<string> tooBroad, CleanupTally tally)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var spec in cat.Roots)
        {
            var baseDir = GetBase(spec.Root);
            if (string.IsNullOrEmpty(baseDir)) continue;

            foreach (var candidate in Expand(baseDir, spec.RelativePath))
            {
                var n = CleanupPolicy.Normalize(candidate);
                if (n.Length == 0 || !seen.Add(n)) continue;
                if (!Directory.Exists(n)) continue;

                if (!CleanupPolicy.IsRootAcceptable(n, forbidden, tooBroad))
                {
                    tally.AddDetail("Folder rejected for safety: " + n);
                    continue;
                }

                if (IsReparse(n))
                {
                    tally.AddSkipped(SkipReason.ReparsePoint);
                    continue;
                }

                result.Add(n);
            }
        }
        return result;
    }

    private static IEnumerable<string> Expand(string baseDir, string relative)
    {
        var current = new List<string> { baseDir };
        if (string.IsNullOrEmpty(relative)) return current;

        foreach (var segment in relative.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".." || segment == ".") return Array.Empty<string>();

            var next = new List<string>();
            foreach (var dir in current)
            {
                if (segment.IndexOfAny(new[] { '*', '?' }) >= 0)
                {
                    try
                    {
                        foreach (var sub in Directory.EnumerateDirectories(dir, segment))
                        {
                            if (!IsReparse(sub)) next.Add(sub);
                        }
                    }
                    catch (Exception ex) when (IsExpected(ex))
                    {
                        // folder missing or unreadable: nothing to expand
                    }
                }
                else
                {
                    next.Add(Path.Combine(dir, segment));
                }
            }
            current = next;
        }
        return current;
    }

    private static string GetBase(KnownRoot root) => root switch
    {
        KnownRoot.Temp => Path.GetTempPath(),
        KnownRoot.LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        KnownRoot.ProgramData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        _ => string.Empty,
    };

    private static List<string> BuildForbiddenTrees()
    {
        var list = new List<string>();
        void Add(string? p)
        {
            if (!string.IsNullOrWhiteSpace(p)) list.Add(p);
        }

        Add(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
        {
            Add(Path.Combine(profile, "Downloads"));
            Add(Path.Combine(profile, "Saved Games"));
            Add(Path.Combine(profile, "OneDrive"));
        }
        return list;
    }

    private static List<string> BuildTooBroadRoots()
    {
        var list = new List<string>();
        void Add(string? p)
        {
            if (!string.IsNullOrWhiteSpace(p)) list.Add(p);
        }

        Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
        return list;
    }

    private static bool IsReparse(DirectoryInfo di)
    {
        try
        {
            return (di.Attributes & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            return true; // unreadable: treat as unsafe
        }
    }

    private static bool IsReparse(string path) => IsReparse(new DirectoryInfo(path));

    private static bool IsExpected(Exception ex) =>
        ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException
        || ex is ArgumentException || ex is NotSupportedException;

    private static void RecordFailure(Exception ex, string path, CleanupTally tally)
    {
        switch (ex)
        {
            case FileNotFoundException:
            case DirectoryNotFoundException:
                tally.AddSkipped(SkipReason.Vanished);
                break;
            case UnauthorizedAccessException:
                tally.AddSkipped(SkipReason.AccessDenied, path + ": " + ex.Message);
                break;
            case IOException io when IsSharingViolation(io):
                tally.AddSkipped(SkipReason.Locked);
                break;
            default:
                tally.AddSkipped(SkipReason.IoError, path + ": " + ex.Message);
                break;
        }
    }

    private static bool IsSharingViolation(IOException ex)
    {
        var code = ex.HResult & 0xFFFF;
        return code == 32 || code == 33;
    }

    // ---- Recycle Bin (Windows shell API) ----

    private static CategoryResult RecycleBin(CacheCategory cat, bool empty)
    {
        var tally = new CleanupTally();
        try
        {
            var info = new Native.SHQUERYRBINFO { cbSize = Marshal.SizeOf<Native.SHQUERYRBINFO>() };
            var hr = Native.SHQueryRecycleBin(null, ref info);
            if (hr != 0)
            {
                tally.AddDetail("Recycle Bin query failed (HRESULT 0x" + hr.ToString("X8") + ").");
                return new CategoryResult(cat.Id, cat.Name, tally, "Could not read the Recycle Bin.");
            }

            if (info.i64NumItems == 0)
            {
                return new CategoryResult(cat.Id, cat.Name, tally, "The Recycle Bin is already empty.");
            }

            if (!empty)
            {
                tally.AddHandled(info.i64Size, info.i64NumItems);
                return new CategoryResult(cat.Id, cat.Name, tally,
                    "Holds " + info.i64NumItems + " item(s), " + SizeText.Format(info.i64Size) + ". Emptied only after you confirm.");
            }

            // SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND
            var rc = Native.SHEmptyRecycleBin(IntPtr.Zero, null, 0x7);
            if (rc != 0)
            {
                tally.AddDetail("Emptying the Recycle Bin failed (HRESULT 0x" + rc.ToString("X8") + ").");
                return new CategoryResult(cat.Id, cat.Name, tally, "The Recycle Bin could NOT be emptied.");
            }

            tally.AddHandled(info.i64Size, info.i64NumItems);
            return new CategoryResult(cat.Id, cat.Name, tally, "Emptied (size was measured just before emptying).");
        }
        catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException || ex is COMException || ex is SEHException)
        {
            tally.AddDetail("Recycle Bin API unavailable: " + ex.Message);
            return new CategoryResult(cat.Id, cat.Name, tally, "Recycle Bin API unavailable.");
        }
    }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("shell32.dll", EntryPoint = "SHQueryRecycleBinW", CharSet = CharSet.Unicode)]
        public static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [DllImport("shell32.dll", EntryPoint = "SHEmptyRecycleBinW", CharSet = CharSet.Unicode)]
        public static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);
    }
}
