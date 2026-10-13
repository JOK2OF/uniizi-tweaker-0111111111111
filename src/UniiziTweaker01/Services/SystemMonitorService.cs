using System.Diagnostics;
using System.Runtime.InteropServices;
using UniiziTweaker01.Core;
using UniiziTweaker01.Infrastructure;

namespace UniiziTweaker01.Services;

/// <summary>Takes real measurements from Windows. Anything it cannot read is reported as null, never invented.</summary>
public sealed class SystemMonitorService
{
    private readonly CpuUsageCalculator _cpu = new();

    public SystemSnapshot Sample()
    {
        double? cpu = null;
        if (NativeMethods.GetSystemTimes(out ulong idle, out ulong kernel, out ulong user))
            cpu = _cpu.Update(idle, kernel, user);

        long total = 0, available = 0;
        var ms = new NativeMethods.MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>() };
        if (NativeMethods.GlobalMemoryStatusEx(ref ms))
        {
            total = (long)ms.ullTotalPhys;
            available = (long)ms.ullAvailPhys;
        }

        long? driveTotal = null, driveFree = null;
        try
        {
            string? root = Path.GetPathRoot(Environment.SystemDirectory);
            if (root != null)
            {
                var drive = new DriveInfo(root);
                if (drive.IsReady)
                {
                    driveTotal = drive.TotalSize;
                    driveFree = drive.AvailableFreeSpace;
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Could not read system drive", ex);
        }

        return new SystemSnapshot(
            DateTime.Now, cpu, total, available, driveTotal, driveFree,
            CountProcesses(), TimeSpan.FromMilliseconds(Environment.TickCount64));
    }

    private static int CountProcesses()
    {
        var processes = Process.GetProcesses();
        try { return processes.Length; }
        finally { foreach (var p in processes) p.Dispose(); }
    }
}
