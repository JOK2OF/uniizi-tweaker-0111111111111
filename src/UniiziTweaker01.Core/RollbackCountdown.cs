namespace UniiziTweaker01.Core;

/// <summary>Countdown used by the display "Keep changes?" confirmation.</summary>
public sealed class RollbackCountdown
{
    public RollbackCountdown(int seconds)
    {
        if (seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        Remaining = seconds;
    }

    public int Remaining { get; private set; }
    public bool Expired => Remaining <= 0;

    public int Tick()
    {
        if (Remaining > 0) Remaining--;
        return Remaining;
    }
}
