using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeAccountSwitcher;

// Shared camelCase options — matches the field names Claude Code itself uses.
internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions CamelCase = new(JsonSerializerDefaults.Web);
}

public sealed class StoredAccount
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }

    // Unix milliseconds, matching the real .credentials.json.
    public required long ExpiresAt { get; init; }

    // Pass-through only — omitting these breaks the CLI's statusline.
    public string? SubscriptionType { get; init; }
    public string? RateLimitTier { get; init; }

    // No typed Scopes property: the real .credentials.json stores "scopes" as
    // an array, this app's own OAuth response as a string. ExtraFields
    // catches it (and anything else unmodeled) regardless of shape.
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; set; }
}

public sealed record AccountIndexEntry(
    string Id,
    string Label,
    DateTimeOffset AddedAt,
    string? Email = null,
    string? OrganizationUuid = null,
    bool IsTeamAccount = false);

// Maps to /api/oauth/profile: account.display_name/email, organization.uuid,
// IsTeamAccount via AccountLabeler, RateLimitTier verbatim from
// organization.rate_limit_tier, SubscriptionType from organization_type (see ProfileParser).
public sealed record ProfileInfo(string DisplayName, string Email, string OrganizationUuid, bool IsTeamAccount, string? SubscriptionType, string? RateLimitTier);

public sealed record RateLimitWindow(double UsedPercentage, DateTimeOffset? ResetsAt);

public sealed record UsageInfo(RateLimitWindow? FiveHour, RateLimitWindow? SevenDay);
