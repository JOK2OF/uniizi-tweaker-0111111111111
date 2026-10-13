#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace UniiziTweaker01.Core.CacheCleaning;

public enum SkipReason
{
    None = 0,
    ReparsePoint,
    OutsideAllowedRoot,
    ProtectedPath,
    SystemFile,
    ReadOnly,
    TooRecent,
    Locked,
    AccessDenied,
    IoError,
    Vanished,
}

public static class SkipReasonText
{
    public static string Describe(SkipReason reason) => reason switch
    {
        SkipReason.ReparsePoint => "Symbolic link or junction (never followed)",
        SkipReason.OutsideAllowedRoot => "Outside the allowed folders",
        SkipReason.ProtectedPath => "Protected file or folder (cookies, passwords, saves, documents...)",
        SkipReason.SystemFile => "System-flagged file",
        SkipReason.ReadOnly => "Read-only file",
        SkipReason.TooRecent => "Too recent (modified within the minimum age)",
        SkipReason.Locked => "In use by another program",
        SkipReason.AccessDenied => "Access denied",
        SkipReason.IoError => "Other read/delete error",
        SkipReason.Vanished => "No longer existed",
        _ => reason.ToString(),
    };
}

public sealed record FileCandidate(
    string FullPath,
    long Length,
    DateTime LastWriteUtc,
    bool IsReparsePoint,
    bool IsSystem,
    bool IsReadOnly);

public sealed record CleanupDecision(bool ShouldDelete, SkipReason Reason)
{
    public static CleanupDecision Delete { get; } = new CleanupDecision(true, SkipReason.None);

    public static CleanupDecision Skip(SkipReason reason) => new CleanupDecision(false, reason);
}

public sealed class CleanupRules
{
    public CleanupRules(IEnumerable<string> allowedRoots, TimeSpan minAge)
    {
        var roots = new List<string>();
        foreach (var r in allowedRoots)
        {
            var n = CleanupPolicy.Normalize(r);
            if (n.Length > 0) roots.Add(n);
        }
        AllowedRoots = roots;
        MinAge = minAge;
    }

    public IReadOnlyList<string> AllowedRoots { get; }

    public TimeSpan MinAge { get; }
}

/// <summary>Pure decision logic: what may be deleted. No file system access here.</summary>
public static class CleanupPolicy
{
    private static readonly string[] ProtectedSegments =
    {
        "Documents", "Downloads", "Desktop", "Saved Games", "Pictures", "Videos", "Music", "OneDrive",
        "Cookies", "Local Storage", "IndexedDB", "Session Storage", "Sessions",
    };

    private static readonly string[] ProtectedFileNames =
    {
        "Cookies", "Cookies-journal", "Login Data", "Login Data-journal", "Login Data For Account",
        "Web Data", "History", "Bookmarks", "Preferences", "Secure Preferences", "Local State",
        "cookies.sqlite", "logins.json", "key4.db", "places.sqlite", "formhistory.sqlite",
    };

    private static readonly string[] ProtectedExtensions =
    {
        ".kdbx", ".pfx", ".pem", ".key", ".sav", ".save", ".wallet",
    };

    /// <summary>Full path, ".." resolved, no trailing separator (except for a drive root). Empty string if invalid.</summary>
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is IOException)
        {
            return string.Empty;
        }

        var root = Path.GetPathRoot(full) ?? string.Empty;
        if (full.Length > root.Length)
        {
            full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        return full;
    }

    /// <summary>True if path equals root or lies inside it. Both must be normalized. Respects folder boundaries.</summary>
    public static bool IsUnderOrEqual(string path, string root)
    {
        if (root.Length == 0 || path.Length < root.Length) return false;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return false;
        if (path.Length == root.Length) return true;
        var last = root[root.Length - 1];
        if (last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar) return true;
        var next = path[root.Length];
        return next == Path.DirectorySeparatorChar || next == Path.AltDirectorySeparatorChar;
    }

    /// <summary>
    /// A folder may be used as a cleaning root only if it is fully qualified, not a drive root,
    /// not one of the "too broad" folders, not inside a forbidden tree and not an ancestor of one.
    /// </summary>
    public static bool IsRootAcceptable(string root, IEnumerable<string> forbiddenTrees, IEnumerable<string> tooBroadExact)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)) return false;
        var n = Normalize(root);
        if (n.Length == 0) return false;

        var driveRoot = Path.GetPathRoot(n);
        if (string.IsNullOrEmpty(driveRoot)) return false;
        if (string.Equals(n, driveRoot, StringComparison.OrdinalIgnoreCase)) return false;

        foreach (var broad in tooBroadExact)
        {
            var nb = Normalize(broad);
            if (nb.Length > 0 && string.Equals(n, nb, StringComparison.OrdinalIgnoreCase)) return false;
        }

        foreach (var forbidden in forbiddenTrees)
        {
            var nf = Normalize(forbidden);
            if (nf.Length == 0) continue;
            if (IsUnderOrEqual(n, nf) || IsUnderOrEqual(nf, n)) return false;
        }

        return true;
    }

    public static CleanupDecision Decide(FileCandidate file, CleanupRules rules, DateTime nowUtc)
    {
        if (file.IsReparsePoint) return CleanupDecision.Skip(SkipReason.ReparsePoint);

        var path = Normalize(file.FullPath);
        if (path.Length == 0) return CleanupDecision.Skip(SkipReason.OutsideAllowedRoot);

        string? matchedRoot = null;
        foreach (var root in rules.AllowedRoots)
        {
            if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase)) continue;
            if (!IsUnderOrEqual(path, root)) continue;
            if (matchedRoot == null || root.Length > matchedRoot.Length) matchedRoot = root;
        }
        if (matchedRoot == null) return CleanupDecision.Skip(SkipReason.OutsideAllowedRoot);

        var relative = path.Substring(matchedRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (IsProtected(relative)) return CleanupDecision.Skip(SkipReason.ProtectedPath);

        if (file.IsSystem) return CleanupDecision.Skip(SkipReason.SystemFile);
        if (file.IsReadOnly) return CleanupDecision.Skip(SkipReason.ReadOnly);

        if (rules.MinAge > TimeSpan.Zero && nowUtc - file.LastWriteUtc < rules.MinAge)
        {
            return CleanupDecision.Skip(SkipReason.TooRecent);
        }

        return CleanupDecision.Delete;
    }

    private static bool IsProtected(string relativePath)
    {
        var parts = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return true;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            foreach (var seg in ProtectedSegments)
            {
                if (string.Equals(parts[i], seg, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }

        var fileName = parts[parts.Length - 1];
        foreach (var name in ProtectedFileNames)
        {
            if (string.Equals(fileName, name, StringComparison.OrdinalIgnoreCase)) return true;
        }

        var ext = Path.GetExtension(fileName);
        foreach (var e in ProtectedExtensions)
        {
            if (string.Equals(ext, e, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }
}
