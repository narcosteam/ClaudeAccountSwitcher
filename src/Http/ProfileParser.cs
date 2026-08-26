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

        if (displayName is null || email is null || organizationUuid is null)
        {
            return null;
        }

        return new ProfileInfo(displayName, email, organizationUuid, AccountLabeler.IsTeamAccount(organizationType));
    }
}
