using System.Text.Json;

namespace ClaudeAccountSwitcher;

public sealed class TokenRefresher(ITokenEndpointClient tokenEndpoint)
{
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromMinutes(2);

    public bool IsExpiringSoon(StoredAccount account) =>
        DateTimeOffset.FromUnixTimeMilliseconds(account.ExpiresAt) <= DateTimeOffset.UtcNow.Add(ExpiryBuffer);

    public async Task<StoredAccount> EnsureFreshAsync(StoredAccount account, CancellationToken ct)
    {
        if (!IsExpiringSoon(account))
        {
            return account;
        }

        var refreshed = await tokenEndpoint.RefreshAsync(account.RefreshToken, ct);

        // The refresh response never carries subscriptionType/rateLimitTier — keep those
        // from the account being refreshed. It does carry "scopes" though (see
        // TokenEndpointClient); merge rather than discard so an account whose stored
        // snapshot is missing it (e.g. added before that was fixed) self-heals here.
        return new StoredAccount
        {
            AccessToken = refreshed.AccessToken,
            RefreshToken = refreshed.RefreshToken,
            ExpiresAt = refreshed.ExpiresAt,
            SubscriptionType = account.SubscriptionType,
            RateLimitTier = account.RateLimitTier,
            ExtraFields = MergeExtraFields(account.ExtraFields, refreshed.ExtraFields),
        };
    }

    private static Dictionary<string, JsonElement>? MergeExtraFields(
        Dictionary<string, JsonElement>? previous, Dictionary<string, JsonElement>? fromRefresh)
    {
        if (fromRefresh is null || fromRefresh.Count == 0)
        {
            return previous;
        }
        var merged = previous is null ? [] : new Dictionary<string, JsonElement>(previous);
        foreach (var (key, value) in fromRefresh)
        {
            merged[key] = value;
        }
        return merged;
    }
}
