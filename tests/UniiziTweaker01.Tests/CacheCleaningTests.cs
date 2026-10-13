#nullable enable
using System;
using System.IO;
using System.Linq;
using UniiziTweaker01.Core.CacheCleaning;
using Xunit;

namespace UniiziTweaker01.Tests;

public class CacheCleaningTests
{
    private static readonly DateTime Now = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
    private const string TempRoot = @"C:\Users\Test\AppData\Local\Temp";

    private static CleanupRules Rules(int minAgeDays = 3) =>
        new CleanupRules(new[] { TempRoot }, TimeSpan.FromDays(minAgeDays));

    private static FileCandidate Candidate(string path, double ageDays = 10, bool reparse = false, bool system = false, bool readOnly = false) =>
        new FileCandidate(path, 100, Now.AddDays(-ageDays), reparse, system, readOnly);

    [Fact]
    public void Decide_DeletesOldFileInsideAllowedRoot()
    {
        var d = CleanupPolicy.Decide(Candidate(TempRoot + @"\a.tmp"), Rules(), Now);
        Assert.True(d.ShouldDelete);
        Assert.Equal(SkipReason.None, d.Reason);
    }

    [Fact]
    public void Decide_DeletesInNestedFolderAndIsCaseInsensitive()
    {
        var d = CleanupPolicy.Decide(Candidate(@"c:\users\test\appdata\local\temp\sub\dir\a.tmp"), Rules(), Now);
        Assert.True(d.ShouldDelete);
    }

    [Fact]
    public void Decide_SkipsRecentFile()
    {
        var d = CleanupPolicy.Decide(Candidate(TempRoot + @"\a.tmp", ageDays: 1), Rules(), Now);
        Assert.False(d.ShouldDelete);
        Assert.Equal(SkipReason.TooRecent, d.Reason);
    }

    [Fact]
    public void Decide_FutureTimestamp_IsTooRecentWhenAgeRequired_ButAllowedWhenNoAge()
    {
        var future = Candidate(TempRoot + @"\a.tmp", ageDays: -1);
        Assert.Equal(SkipReason.TooRecent, CleanupPolicy.Decide(future, Rules(3), Now).Reason);
        Assert.True(CleanupPolicy.Decide(future, Rules(0), Now).ShouldDelete);
    }

    [Fact]
    public void Decide_SkipsReparsePoints()
    {
        var d = CleanupPolicy.Decide(Candidate(TempRoot + @"\link", reparse: true), Rules(), Now);
        Assert.Equal(SkipReason.ReparsePoint, d.Reason);
    }

    [Theory]
    [InlineData(@"C:\Users\Test\AppData\Local\Temp2\a.tmp")]
    [InlineData(@"C:\Windows\System32\kernel32.dll")]
    [InlineData(@"C:\Users\Test\AppData\Local\Temp\..\..\..\..\Windows\x.dll")]
    [InlineData(TempRoot)]
    public void Decide_SkipsEverythingOutsideRoots(string path)
    {
        var d = CleanupPolicy.Decide(Candidate(path), Rules(), Now);
        Assert.False(d.ShouldDelete);
        Assert.Equal(SkipReason.OutsideAllowedRoot, d.Reason);
    }

    [Theory]
    [InlineData("Cookies")]
    [InlineData("Login Data")]
    [InlineData("cookies.sqlite")]
    [InlineData("passwords.kdbx")]
    [InlineData("slot1.sav")]
    public void Decide_ProtectsSensitiveFiles(string fileName)
    {
        var d = CleanupPolicy.Decide(Candidate(TempRoot + @"\" + fileName), Rules(), Now);
        Assert.Equal(SkipReason.ProtectedPath, d.Reason);
    }

    [Theory]
    [InlineData(@"\Downloads\setup.exe")]
    [InlineData(@"\Saved Games\slot.dat")]
    [InlineData(@"\x\Documents\report.docx")]
    public void Decide_ProtectsSensitiveFolders(string relative)
    {
        var d = CleanupPolicy.Decide(Candidate(TempRoot + relative), Rules(), Now);
        Assert.Equal(SkipReason.ProtectedPath, d.Reason);
    }

    [Fact]
    public void Decide_SkipsSystemAndReadOnlyFiles()
    {
        Assert.Equal(SkipReason.SystemFile, CleanupPolicy.Decide(Candidate(TempRoot + @"\a", system: true), Rules(), Now).Reason);
        Assert.Equal(SkipReason.ReadOnly, CleanupPolicy.Decide(Candidate(TempRoot + @"\a", readOnly: true), Rules(), Now).Reason);
    }

    [Fact]
    public void IsRootAcceptable_RejectsDangerousRoots()
    {
        var forbidden = new[] { @"C:\Windows", @"C:\Program Files", @"C:\Users\Test\Documents" };
        var broad = new[] { @"C:\Users\Test", @"C:\Users\Test\AppData\Local" };

        Assert.True(CleanupPolicy.IsRootAcceptable(TempRoot, forbidden, broad));
        Assert.True(CleanupPolicy.IsRootAcceptable(@"C:\Temp", forbidden, broad));
        Assert.True(CleanupPolicy.IsRootAcceptable(@"C:\Windowsish\Temp", forbidden, broad));

        Assert.False(CleanupPolicy.IsRootAcceptable(@"C:\", forbidden, broad));
        Assert.False(CleanupPolicy.IsRootAcceptable(@"C:\Windows", forbidden, broad));
        Assert.False(CleanupPolicy.IsRootAcceptable(@"C:\Windows\Temp", forbidden, broad));
        Assert.False(CleanupPolicy.IsRootAcceptable(@"C:\Users\Test", forbidden, broad));
        Assert.False(CleanupPolicy.IsRootAcceptable(@"C:\Users\Test\AppData\Local", forbidden, broad));
        Assert.False(CleanupPolicy.IsRootAcceptable(@"C:\Users", forbidden, broad));
        Assert.False(CleanupPolicy.IsRootAcceptable(@"C:\Users\Test\Documents\cache", forbidden, broad));
        Assert.False(CleanupPolicy.IsRootAcceptable(@"relative\path", forbidden, broad));
        Assert.False(CleanupPolicy.IsRootAcceptable("", forbidden, broad));
    }

    [Fact]
    public void Catalog_IdsAreUnique_AndFindWorks()
    {
        var ids = CacheCategoryCatalog.All.Select(c => c.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.NotNull(CacheCategoryCatalog.Find("user-temp"));
        Assert.Null(CacheCategoryCatalog.Find("nope"));
    }

    [Fact]
    public void Catalog_RecycleBin_IsExplicitOnly()
    {
        var bins = CacheCategoryCatalog.All.Where(c => c.IsRecycleBin).ToList();
        var bin = Assert.Single(bins);
        Assert.Equal(CategoryRisk.NeedsExplicitConfirmation, bin.Risk);
        Assert.False(bin.SelectedByDefault);
        Assert.Empty(bin.Roots);
    }

    [Fact]
    public void Catalog_BrowsersAndShadersAreNotSelectedByDefault()
    {
        foreach (var c in CacheCategoryCatalog.All.Where(c => c.Id.StartsWith("browser-") || c.Id == "shader-cache"))
        {
            Assert.False(c.SelectedByDefault);
        }
    }

    [Fact]
    public void Catalog_PathsAreRelativeAndAvoidProtectedLocations()
    {
        string[] banned = { "Documents", "Downloads", "Cookies", "Login Data", "Local Storage", "Saved Games", ".." };
        foreach (var c in CacheCategoryCatalog.All.Where(c => !c.IsRecycleBin))
        {
            Assert.NotEmpty(c.Roots);
            Assert.True(c.MinAge >= TimeSpan.Zero);
            foreach (var r in c.Roots)
            {
                Assert.False(Path.IsPathRooted(r.RelativePath));
                foreach (var b in banned)
                {
                    Assert.DoesNotContain(b, r.RelativePath, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        Assert.True(CacheCategoryCatalog.Find("user-temp")!.MinAge >= TimeSpan.FromDays(1));
    }

    [Fact]
    public void Tally_CountsHandledAndSkipped()
    {
        var t = new CleanupTally();
        t.AddHandled(100);
        t.AddHandled(50);
        t.AddHandled(1000, 10);
        t.AddSkipped(SkipReason.Locked);
        t.AddSkipped(SkipReason.Locked);
        t.AddSkipped(SkipReason.AccessDenied, "x: denied");
        t.AddFolderRemoved();

        Assert.Equal(12, t.Files);
        Assert.Equal(1150, t.Bytes);
        Assert.Equal(3, t.SkippedItems);
        Assert.Equal(2, t.SkippedByReason[SkipReason.Locked]);
        Assert.Equal(1, t.FoldersRemoved);
        Assert.Single(t.Details);
    }

    [Fact]
    public void Tally_CapsDetails()
    {
        var t = new CleanupTally();
        for (var i = 0; i < 100; i++) t.AddSkipped(SkipReason.IoError, "e" + i);
        Assert.Equal(CleanupTally.MaxDetails, t.Details.Count);
        Assert.Equal(100, t.SkippedItems);
    }

    [Fact]
    public void Totals_SumAcrossCategories()
    {
        var a = new CleanupTally();
        a.AddHandled(10);
        a.AddSkipped(SkipReason.Locked);
        var b = new CleanupTally();
        b.AddHandled(20, 2);
        b.AddSkipped(SkipReason.Locked);
        b.AddSkipped(SkipReason.TooRecent);

        var totals = CleanupTotals.From(new[]
        {
            new CategoryResult("a", "A", a, ""),
            new CategoryResult("b", "B", b, ""),
        });

        Assert.Equal(3, totals.Files);
        Assert.Equal(30, totals.Bytes);
        Assert.Equal(3, totals.SkippedItems);
        Assert.Equal(2, totals.SkippedByReason[SkipReason.Locked]);
    }

    [Theory]
    [InlineData(-5, "0 B")]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1.0 MB")]
    public void SizeText_Formats(long bytes, string expected)
    {
        Assert.Equal(expected, SizeText.Format(bytes));
    }

    [Fact]
    public void History_RoundTripsAndTrims()
    {
        var entry = new CleanupHistoryEntry(Now, new[] { "A", "B" }, 5, 1000, 2, false);
        var restored = CleanupHistory.Deserialize(CleanupHistory.Serialize(new[] { entry }));
        var e = Assert.Single(restored);
        Assert.Equal(5, e.FilesDeleted);
        Assert.Equal(new[] { "A", "B" }, e.Categories);

        var list = new System.Collections.Generic.List<CleanupHistoryEntry>();
        for (var i = 0; i < CleanupHistory.MaxEntries + 10; i++)
        {
            list = CleanupHistory.Append(list, new CleanupHistoryEntry(Now.AddMinutes(i), new[] { "x" }, i, 0, 0, false));
        }
        Assert.Equal(CleanupHistory.MaxEntries, list.Count);
        Assert.Equal(CleanupHistory.MaxEntries + 9, list[0].FilesDeleted); // newest first
    }

    [Fact]
    public void History_InvalidJson_ReturnsEmpty()
    {
        Assert.Empty(CleanupHistory.Deserialize("not json"));
        Assert.Empty(CleanupHistory.Deserialize("null"));
    }
}
