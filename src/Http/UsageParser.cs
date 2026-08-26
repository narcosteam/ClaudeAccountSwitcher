using System.Globalization;
using System.Text.Json;

namespace ClaudeAccountSwitcher;

// five_hour/seven_day sit at the root (not under "rate_limits") and carry "utilization".
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
