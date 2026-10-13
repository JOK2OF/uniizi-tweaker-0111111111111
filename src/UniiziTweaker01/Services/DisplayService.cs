using UniiziTweaker01.Core;
using UniiziTweaker01.Infrastructure;

namespace UniiziTweaker01.Services;

public enum DisplayChangeResult { Success, RestartRequired, Failed, BadMode, NotUpdated, BadFlags, BadParam, Other }

public sealed record DisplayDeviceInfo(
    string DeviceName, string AdapterName, string MonitorName, bool IsPrimary,
    DisplayMode Current, IReadOnlyList<DisplayMode> Modes)
{
    public string Title => $"{DeviceName.Replace(@"\\.\", "")} · {MonitorName}" + (IsPrimary ? " (primary)" : "");
    public string Subtitle => Current.ToString();
}

/// <summary>Display enumeration and mode changes through the documented user32 display APIs.</summary>
public sealed class DisplayService
{
    public IReadOnlyList<DisplayDeviceInfo> GetDisplays()
    {
        var list = new List<DisplayDeviceInfo>();
        for (uint i = 0; ; i++)
        {
            var dd = NativeMethods.NewDisplayDevice();
            if (!NativeMethods.EnumDisplayDevices(null, i, ref dd, 0)) break;
            if ((dd.StateFlags & NativeMethods.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0) continue;
            if ((dd.StateFlags & NativeMethods.DISPLAY_DEVICE_MIRRORING_DRIVER) != 0) continue;

            var current = GetCurrent(dd.DeviceName);
            if (current == null) continue;
            list.Add(new DisplayDeviceInfo(
                dd.DeviceName, dd.DeviceString, GetMonitorName(dd.DeviceName),
                (dd.StateFlags & NativeMethods.DISPLAY_DEVICE_PRIMARY_DEVICE) != 0,
                current, EnumerateModes(dd.DeviceName)));
        }
        return list;
    }

    /// <summary>Lightweight summary used by the dashboard (no mode enumeration).</summary>
    public (DisplayMode? Primary, int Count) GetPrimarySummary()
    {
        DisplayMode? primary = null;
        int count = 0;
        for (uint i = 0; ; i++)
        {
            var dd = NativeMethods.NewDisplayDevice();
            if (!NativeMethods.EnumDisplayDevices(null, i, ref dd, 0)) break;
            if ((dd.StateFlags & NativeMethods.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0) continue;
            if ((dd.StateFlags & NativeMethods.DISPLAY_DEVICE_MIRRORING_DRIVER) != 0) continue;
            count++;
            if ((dd.StateFlags & NativeMethods.DISPLAY_DEVICE_PRIMARY_DEVICE) != 0)
                primary = GetCurrent(dd.DeviceName);
        }
        return (primary, count);
    }

    public DisplayMode? GetCurrent(string deviceName)
    {
        var dm = NativeMethods.NewDevMode();
        if (!NativeMethods.EnumDisplaySettingsEx(deviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref dm, 0)) return null;
        return new DisplayMode((int)dm.dmPelsWidth, (int)dm.dmPelsHeight, (int)dm.dmDisplayFrequency, (int)dm.dmBitsPerPel);
    }

    /// <summary>Asks the driver whether a mode is valid without applying it (CDS_TEST).</summary>
    public DisplayChangeResult TestMode(string deviceName, DisplayMode mode) => Change(deviceName, mode, NativeMethods.CDS_TEST);

    /// <summary>Applies a mode for the current session only (registry untouched).</summary>
    public DisplayChangeResult ApplyTemporary(string deviceName, DisplayMode mode) => Change(deviceName, mode, 0);

    /// <summary>Applies a mode and stores it as the user's Windows setting.</summary>
    public DisplayChangeResult ApplyPersistent(string deviceName, DisplayMode mode) =>
        Change(deviceName, mode, NativeMethods.CDS_UPDATEREGISTRY);

    private static List<DisplayMode> EnumerateModes(string deviceName)
    {
        var result = new List<DisplayMode>();
        var seen = new HashSet<(int, int, int, int)>();
        for (int i = 0; ; i++)
        {
            var dm = NativeMethods.NewDevMode();
            if (!NativeMethods.EnumDisplaySettingsEx(deviceName, i, ref dm, 0)) break;
            if (dm.dmBitsPerPel != 32 || dm.dmDisplayFrequency <= 1) continue;
            var mode = new DisplayMode((int)dm.dmPelsWidth, (int)dm.dmPelsHeight, (int)dm.dmDisplayFrequency, (int)dm.dmBitsPerPel);
            if (seen.Add((mode.Width, mode.Height, mode.RefreshRate, mode.BitsPerPixel))) result.Add(mode);
        }
        return result;
    }

    private static string GetMonitorName(string adapterDeviceName)
    {
        string? first = null;
        for (uint j = 0; ; j++)
        {
            var mon = NativeMethods.NewDisplayDevice();
            if (!NativeMethods.EnumDisplayDevices(adapterDeviceName, j, ref mon, 0)) break;
            first ??= mon.DeviceString;
            if ((mon.StateFlags & NativeMethods.DISPLAY_DEVICE_ACTIVE) != 0) return mon.DeviceString;
        }
        return first ?? "Unknown monitor";
    }

    private static DisplayChangeResult Change(string deviceName, DisplayMode mode, uint flags)
    {
        try
        {
            var dm = NativeMethods.NewDevMode();
            if (!NativeMethods.EnumDisplaySettingsEx(deviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref dm, 0))
                return DisplayChangeResult.Failed;

            dm.dmPelsWidth = (uint)mode.Width;
            dm.dmPelsHeight = (uint)mode.Height;
            dm.dmDisplayFrequency = (uint)mode.RefreshRate;
            dm.dmBitsPerPel = (uint)mode.BitsPerPixel;
            dm.dmFields |= NativeMethods.DM_PELSWIDTH | NativeMethods.DM_PELSHEIGHT |
                           NativeMethods.DM_DISPLAYFREQUENCY | NativeMethods.DM_BITSPERPEL;

            int code = NativeMethods.ChangeDisplaySettingsEx(deviceName, ref dm, IntPtr.Zero, flags, IntPtr.Zero);
            var result = code switch
            {
                0 => DisplayChangeResult.Success,
                1 => DisplayChangeResult.RestartRequired,
                -1 => DisplayChangeResult.Failed,
                -2 => DisplayChangeResult.BadMode,
                -3 => DisplayChangeResult.NotUpdated,
                -4 => DisplayChangeResult.BadFlags,
                -5 => DisplayChangeResult.BadParam,
                _ => DisplayChangeResult.Other,
            };
            AppLog.Info($"ChangeDisplaySettingsEx {deviceName} {mode} flags=0x{flags:X} -> {result} ({code})");
            return result;
        }
        catch (Exception ex)
        {
            AppLog.Error("ChangeDisplaySettingsEx failed", ex);
            return DisplayChangeResult.Failed;
        }
    }
}
