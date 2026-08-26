namespace ClaudeAccountSwitcher;

public static class RefreshScheduler
{
    public static TimeSpan DelayUntilNextMinuteBoundary(DateTimeOffset now)
    {
        // Tick-based so exact-millisecond boundaries don't round through floating point.
        var ticksIntoMinute = now.Ticks % TimeSpan.TicksPerMinute;
        return TimeSpan.FromTicks(TimeSpan.TicksPerMinute - ticksIntoMinute);
    }
}
