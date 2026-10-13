using System.Diagnostics;
using Microsoft.Win32;
using UniiziTweaker01.Infrastructure;

namespace UniiziTweaker01.Services;

/// <summary>Slow-changing system facts (Windows version, power plan, startup entry count).</summary>
public static class SystemInfoProvider
{
    public static string GetWindowsVersion()
    {
        var v = Environment.OSVersion.Version;
        string name = v.Build >= 22000 ? "Windows 11" : "Windows 10";
        string? display = null;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            display = key?.GetValue("DisplayVersion") as string;
        }
        catch (Exception ex)
        {
            AppLog.Error("Could not read Windows display version", ex);
        }
        return display is null ? $"{name} (build {v.Build})" : $"{name} {display} (build {v.Build})";
    }

    /// <summary>Runs the fixed command "powercfg /getactivescheme" (no user input involved).</summary>
    public static string? GetActivePowerPlan()
    {
        try
        {
            var psi = new ProcessStartInfo("powercfg.exe", "/getactivescheme")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi);
            if (process == null) return null;
            string output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(3000))
            {
                try { process.Kill(); } catch { }
                return null;
            }
            int open = output.IndexOf('(');
            int close = output.LastIndexOf(')');
            return open >= 0 && close > open ? output[(open + 1)..close].Trim() : null;
        }
        catch (Exception ex)
        {
            AppLog.Error("Could not read power plan", ex);
            return null;
        }
    }

    /// <summary>Counts values in the standard registry Run keys only (other startup mechanisms are not counted).</summary>
    public static int? GetStartupEntryCount()
    {
        var locations = new (RegistryKey Hive, string Path)[]
        {
            (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run"),
            (Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"),
        };
        int total = 0;
        bool any = false;
        foreach (var (hive, path) in locations)
        {
            try
            {
                using var key = hive.OpenSubKey(path);
                if (key == null) continue;
                total += key.ValueCount;
                any = true;
            }
            catch (Exception ex)
            {
                AppLog.Error("Could not read startup key " + path, ex);
            }
        }
        return any ? total : null;
    }
}
