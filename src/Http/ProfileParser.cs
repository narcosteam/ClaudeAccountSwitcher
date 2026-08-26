using System.Text.Json;

namespace ClaudeAccountSwitcher;

// Shape confirmed against a live /api/oauth/profile response — see ProfileParserTests.
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

    // Only the "claude_" prefix strip is confirmed (claude_pro -> pro); passes through otherwise.
    private static string? SubscriptionType(string? organizationType) =>
        organizationType?.StartsWith("claude_", StringComparison.OrdinalIgnoreCase) == true
            ? organizationType["claude_".Length..]
            : organizationType;
}
