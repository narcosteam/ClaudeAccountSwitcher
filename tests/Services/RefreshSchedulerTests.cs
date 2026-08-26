using ClaudeAccountSwitcher;
using Xunit;

namespace ClaudeAccountSwitcher.Tests;

public class RefreshSchedulerTests
{
    [Fact]
    public void DelayUntilNextMinuteBoundary_ReturnsRemainingSeconds_MidMinute()
    {
        var now = new DateTimeOffset(2026, 8, 24, 14, 32, 17, 500, TimeSpan.Zero);

        var delay = RefreshScheduler.DelayUntilNextMinuteBoundary(now);

        Assert.Equal(TimeSpan.FromSeconds(42.5), delay);
    }

    [Fact]
    public void DelayUntilNextMinuteBoundary_ReturnsFullMinute_WhenExactlyOnBoundary()
    {
        var now = new DateTimeOffset(2026, 8, 24, 14, 32, 0, 0, TimeSpan.Zero);

        var delay = RefreshScheduler.DelayUntilNextMinuteBoundary(now);

        Assert.Equal(TimeSpan.FromMinutes(1), delay);
    }

    [Fact]
    public void DelayUntilNextMinuteBoundary_ReturnsNearZero_JustBeforeBoundary()
    {
        var now = new DateTimeOffset(2026, 8, 24, 14, 32, 59, 999, TimeSpan.Zero);

        var delay = RefreshScheduler.DelayUntilNextMinuteBoundary(now);

        Assert.Equal(TimeSpan.FromMilliseconds(1), delay);
    }
}
