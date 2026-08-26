using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeAccountSwitcher;

// ponytail: shared camelCase options so StoredAccount round-trips through
// AccountStore and through .credentials.json using the same field names
// Claude Code itself uses (accessToken, refreshToken, ...).
internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions CamelCase = new(JsonSerializerDefaults.Web);
}

// ponytail: subscriptionType/rateLimitTier were dropped in an earlier pass as
// "dead" — they were never populated by TokenEndpointClient and never read by
// this app. That was true from OUR code's perspective, but wrong: dropping
// them means CredentialsMerger writes a claudeAiOauth block into the REAL
// ~/.claude/.credentials.json with these keys entirely ABSENT, whereas Claude
// Code's own login flow writes them (confirmed live: the real file had
// "expiresAt"/"scopes" alongside them before this app's first switch
// overwrote it) — and losing them broke the CLI's statusline. Restored as
// nullable pass-through fields; still not populated by our own OAuth flow
// today (see UsageClient/OAuthClient for a live source: /api/oauth/profile
// returns organization.organization_type/rate_limit_tier).
public sealed class StoredAccount
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }

    // ponytail: confirmed live to be Unix milliseconds against a real
    // .credentials.json value (Task 13 manual verification).
    public required long ExpiresAt { get; init; }

    public string? Scopes { get; init; }
    public string? SubscriptionType { get; init; }
    public string? RateLimitTier { get; init; }

    // ponytail: catches any claudeAiOauth field this app doesn't have an
    // explicit property for (present today, or added by a future Claude Code
    // version) so it survives a deserialize -> serialize round trip through
    // SwitcherService/AccountStore instead of being silently dropped — the
    // same class of bug that broke the statusline when subscriptionType/
    // rateLimitTier were dropped. System.Text.Json's own extension-data
    // support does this automatically; no hand-rolled JSON patching needed.
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

// ponytail: confirmed live against a real /api/oauth/profile response.
// DisplayName/Email/OrganizationUuid are the account.display_name/email and
// organization.uuid fields; IsTeamAccount is derived from
// organization.organization_type via AccountLabeler, not stored raw.
public sealed record ProfileInfo(string DisplayName, string Email, string OrganizationUuid, bool IsTeamAccount);

public sealed record RateLimitWindow(double UsedPercentage, DateTimeOffset? ResetsAt);

public sealed record UsageInfo(RateLimitWindow? FiveHour, RateLimitWindow? SevenDay);
