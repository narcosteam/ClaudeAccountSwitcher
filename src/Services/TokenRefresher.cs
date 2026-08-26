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

        // The refresh response never carries subscriptionType/rateLimitTier/extension
        // fields — keep them from the account being refreshed.
        return new StoredAccount
        {
            AccessToken = refreshed.AccessToken,
            RefreshToken = refreshed.RefreshToken,
            ExpiresAt = refreshed.ExpiresAt,
            SubscriptionType = account.SubscriptionType,
            RateLimitTier = account.RateLimitTier,
            ExtraFields = account.ExtraFields,
        };
    }
}
