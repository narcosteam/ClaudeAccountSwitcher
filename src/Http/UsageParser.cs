using System.Globalization;
using System.Text.Json;

namespace ClaudeAccountSwitcher;

// ponytail: real shape confirmed against the live /api/oauth/usage response —
// five_hour/seven_day sit at the ROOT (not under a "rate_limits" wrapper) and
// carry "utilization" (not "used_percentage"). The original guess based on
// statusline field names was wrong on both counts.
public static class UsageParser
{
    public static UsageInfo Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new UsageInfo(
            ParseWindow(root, "five_hour"),
            ParseWindow(root, "seven_day"));
    }

    private static RateLimitWindow? ParseWindow(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var window) || window.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!window.TryGetProperty("utilization", out var u) || u.ValueKind != JsonValueKind.Number)
        {
            return null;
        }
        var utilization = u.GetDouble();

        DateTimeOffset? resetsAt = window.TryGetProperty("resets_at", out var r) && r.ValueKind == JsonValueKind.String
            ? DateTimeOffset.Parse(r.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal)
            : null;

        return new RateLimitWindow(utilization, resetsAt);
    }
}
