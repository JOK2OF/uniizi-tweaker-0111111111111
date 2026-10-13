namespace UniiziTweaker01.Core;

/// <summary>
/// Computes total CPU usage from two successive GetSystemTimes samples.
/// Note: the kernel time reported by Windows already includes idle time.
/// </summary>
public sealed class CpuUsageCalculator
{
    private ulong _prevIdle, _prevKernel, _prevUser;
    private bool _hasPrevious;

    /// <returns>Usage in percent (0-100), or null when there is no previous sample yet.</returns>
    public double? Update(ulong idle, ulong kernel, ulong user)
    {
        if (!_hasPrevious)
        {
            Store(idle, kernel, user);
            _hasPrevious = true;
            return null;
        }

        ulong idleDelta = idle - _prevIdle;
        ulong kernelDelta = kernel - _prevKernel;
        ulong userDelta = user - _prevUser;
        Store(idle, kernel, user);

        ulong total = kernelDelta + userDelta;
        if (total == 0 || idleDelta > total) return null;
        double busy = (double)(total - idleDelta) / total * 100.0;
        return Math.Clamp(busy, 0, 100);
    }

    private void Store(ulong idle, ulong kernel, ulong user)
    {
        _prevIdle = idle;
        _prevKernel = kernel;
        _prevUser = user;
    }
}
