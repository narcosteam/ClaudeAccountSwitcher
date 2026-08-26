using System.Text.Json;

namespace ClaudeAccountSwitcher;

// ponytail: shape confirmed against a live /api/oauth/profile response —
// see ProfileParserTests for the captured example.
public static class ProfileParser
{
    public static ProfileInfo? Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("account", out var account) || !root.TryGetProperty("organization", out var organization))
        {
            return null;
        }

        var displayName = account.TryGetProperty("display_name", out var dn) ? dn.GetString() : null;
        var email = account.TryGetProperty("email", out var em) ? em.GetString() : null;
        var organizationUuid = organization.TryGetProperty("uuid", out var ou) ? ou.GetString() : null;
        var organizationType = organization.TryGetProperty("organization_type", out var ot) ? ot.GetString() : null;
        var rateLimitTier = organization.TryGetProperty("rate_limit_tier", out var rlt) ? rlt.GetString() : null;

        if (displayName is null || email is null || organizationUuid is null)
        {
            return null;
        }

        return new ProfileInfo(displayName, email, organizationUuid, AccountLabeler.IsTeamAccount(organizationType),
            SubscriptionType(organizationType), rateLimitTier);
    }

    // ponytail: only the "claude_" prefix strip is live-confirmed (claude_pro
    // -> pro, matching a real .credentials.json). Passes through unstripped
    // rather than guessing further if a future organization_type doesn't
    // have that prefix.
    private static string? SubscriptionType(string? organizationType) =>
        organizationType?.StartsWith("claude_", StringComparison.OrdinalIgnoreCase) == true
            ? organizationType["claude_".Length..]
            : organizationType;
}
