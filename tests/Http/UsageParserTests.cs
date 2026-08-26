using ClaudeAccountSwitcher;
using Xunit;

namespace ClaudeAccountSwitcher.Tests;

public class UsageParserTests
{
    [Fact]
    public void Parse_ReadsFiveHourAndSevenDayWindows()
    {
        // Real shape: windows at the root, "utilization" rather than "used_percentage".
        const string json = """
        {"five_hour":{"utilization":42.5,"resets_at":"2026-08-23T22:00:00Z"},"seven_day":{"utilization":10,"resets_at":"2026-08-29T00:00:00Z"},"seven_day_opus":null,"extra_usage":{"is_enabled":false}}
        """;

        var usage = UsageParser.Parse(json);

        Assert.Equal(42.5, usage.FiveHour!.UsedPercentage);
        Assert.Equal(DateTimeOffset.Parse("2026-08-23T22:00:00Z"), usage.FiveHour.ResetsAt);
        Assert.Equal(10, usage.SevenDay!.UsedPercentage);
    }

    [Fact]
    public void Parse_ReturnsNullWindows_WhenWindowKeysMissing()
    {
        var usage = UsageParser.Parse("{}");

        Assert.Null(usage.FiveHour);
        Assert.Null(usage.SevenDay);
    }

    [Fact]
    public void Parse_ReturnsNullWindow_WhenWindowIsExplicitlyNull()
    {
        // The real response sends null for inactive model-specific windows.
        var usage = UsageParser.Parse("""{"five_hour":null,"seven_day":null}""");

        Assert.Null(usage.FiveHour);
        Assert.Null(usage.SevenDay);
    }

    [Fact]
    public void Parse_ReturnsNullResetsAt_WhenFieldMissing()
    {
        const string json = """{"five_hour":{"utilization":1}}""";

        var usage = UsageParser.Parse(json);

        Assert.Equal(1, usage.FiveHour!.UsedPercentage);
        Assert.Null(usage.FiveHour.ResetsAt);
    }

    [Fact]
    public void Parse_ReturnsNullWindow_WhenUtilizationMissing()
    {
        const string json = """{"five_hour":{}}""";

        var usage = UsageParser.Parse(json);

        Assert.Null(usage.FiveHour);
    }

    [Fact]
    public void Parse_TreatsOffsetlessResetsAt_AsUtc()
    {
        const string json = """{"five_hour":{"utilization":1,"resets_at":"2026-08-23T22:00:00"}}""";

        var usage = UsageParser.Parse(json);

        Assert.Equal(TimeSpan.Zero, usage.FiveHour!.ResetsAt!.Value.Offset);
    }
}
