#nullable enable
using System;
using System.Collections.Generic;

namespace UniiziTweaker01.Core.CacheCleaning;

/// <summary>The fixed list of things the Cache Cleaner is allowed to touch. Nothing else can be added at runtime.</summary>
public static class CacheCategoryCatalog
{
    public static IReadOnlyList<CacheCategory> All { get; } = Build();

    public static CacheCategory? Find(string id)
    {
        foreach (var c in All)
        {
            if (string.Equals(c.Id, id, StringComparison.Ordinal)) return c;
        }
        return null;
    }

    private static List<CacheCategory> Build()
    {
        var list = new List<CacheCategory>
        {
            new CacheCategory(
                "user-temp", "User temporary files",
                "Files in your temp folder that have not been modified for at least 3 days.",
                CategoryRisk.Safe, true, TimeSpan.FromDays(3),
                new[] { new RootSpec(KnownRoot.Temp, "") }),

            Chromium("browser-chrome", "Google Chrome", @"Google\Chrome\User Data"),
            Chromium("browser-edge", "Microsoft Edge", @"Microsoft\Edge\User Data"),
            Chromium("browser-brave", "Brave", @"BraveSoftware\Brave-Browser\User Data"),

            new CacheCategory(
                "browser-firefox", "Mozilla Firefox cache",
                "Disk cache of Firefox profiles. Close Firefox first; files in use are skipped. Cookies, passwords, history and profiles are never touched.",
                CategoryRisk.Safe, false, TimeSpan.Zero,
                new[] { new RootSpec(KnownRoot.LocalAppData, @"Mozilla\Firefox\Profiles\*\cache2") }),

            new CacheCategory(
                "launcher-steam", "Steam web cache",
                "Steam's embedded browser cache (store/community pages). Game files and saves are not touched.",
                CategoryRisk.Safe, false, TimeSpan.Zero,
                new[] { new RootSpec(KnownRoot.LocalAppData, @"Steam\htmlcache") }),

            new CacheCategory(
                "launcher-epic", "Epic Games Launcher web cache",
                "Web cache of the Epic Games Launcher. Game files and saves are not touched.",
                CategoryRisk.Safe, false, TimeSpan.Zero,
                new[] { new RootSpec(KnownRoot.LocalAppData, @"EpicGamesLauncher\Saved\webcache*") }),

            new CacheCategory(
                "shader-cache", "GPU shader caches",
                "DirectX, NVIDIA and AMD shader caches. They are rebuilt automatically.",
                CategoryRisk.Caution, false, TimeSpan.Zero,
                new[]
                {
                    new RootSpec(KnownRoot.LocalAppData, "D3DSCache"),
                    new RootSpec(KnownRoot.LocalAppData, @"NVIDIA\DXCache"),
                    new RootSpec(KnownRoot.LocalAppData, @"NVIDIA\GLCache"),
                    new RootSpec(KnownRoot.LocalAppData, @"AMD\DxCache"),
                    new RootSpec(KnownRoot.LocalAppData, @"AMD\GLCache"),
                    new RootSpec(KnownRoot.LocalAppData, @"AMD\VkCache"),
                },
                false,
                "Games recompile their shaders afterwards, so expect brief stutter the first time you launch each game. Deleting shader caches does not raise FPS."),

            new CacheCategory(
                "crash-dumps", "Application crash dumps",
                "Crash dump files in your user profile that are at least 7 days old.",
                CategoryRisk.Safe, true, TimeSpan.FromDays(7),
                new[] { new RootSpec(KnownRoot.LocalAppData, "CrashDumps") }),

            new CacheCategory(
                "wer-reports", "Old Windows error reports",
                "Windows Error Reporting archives and queues that are at least 30 days old.",
                CategoryRisk.Safe, false, TimeSpan.FromDays(30),
                new[]
                {
                    new RootSpec(KnownRoot.LocalAppData, @"Microsoft\Windows\WER\ReportArchive"),
                    new RootSpec(KnownRoot.LocalAppData, @"Microsoft\Windows\WER\ReportQueue"),
                    new RootSpec(KnownRoot.ProgramData, @"Microsoft\Windows\WER\ReportArchive"),
                    new RootSpec(KnownRoot.ProgramData, @"Microsoft\Windows\WER\ReportQueue"),
                },
                false,
                "The system-wide folder usually needs administrator rights; files that cannot be deleted are reported as skipped."),

            new CacheCategory(
                "recycle-bin", "Recycle Bin",
                "Permanently empties the Recycle Bin of all drives. Files cannot be recovered afterwards.",
                CategoryRisk.NeedsExplicitConfirmation, false, TimeSpan.Zero,
                Array.Empty<RootSpec>(), true),
        };
        return list;
    }

    private static CacheCategory Chromium(string id, string browser, string userDataRelative)
    {
        var roots = new List<RootSpec>();
        foreach (var profile in new[] { "Default", "Profile *" })
        {
            foreach (var sub in new[] { "Cache", "Code Cache", "GPUCache" })
            {
                roots.Add(new RootSpec(KnownRoot.LocalAppData, userDataRelative + "\\" + profile + "\\" + sub));
            }
        }

        return new CacheCategory(
            id, browser + " cache",
            "Cache, code cache and GPU cache of " + browser + ". Close the browser first; files in use are skipped. Cookies, passwords, history and profiles are never touched.",
            CategoryRisk.Safe, false, TimeSpan.Zero, roots.ToArray());
    }
}
