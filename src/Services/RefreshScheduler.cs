namespace ClaudeAccountSwitcher;

public static class RefreshScheduler
{
    public static TimeSpan DelayUntilNextMinuteBoundary(DateTimeOffset now)
    {
        // ponytail: tick-based (not double-seconds) so exact-millisecond boundaries
        // like 59.999s don't round through floating point.
        var ticksIntoMinute = now.Ticks % TimeSpan.TicksPerMinute;
        return TimeSpan.FromTicks(TimeSpan.TicksPerMinute - ticksIntoMinute);
    }
}
