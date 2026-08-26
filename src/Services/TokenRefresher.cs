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

        // ponytail: the refresh_token grant response never carries
        // subscriptionType/rateLimitTier/extension fields (confirmed live —
        // see TokenEndpointClientTests), so a fresh StoredAccount built from
        // it alone is missing them. Carry them forward from the account being
        // refreshed — the same fields whose absence already broke the CLI's
        // statusline once (see Models.cs).
        return new StoredAccount
        {
            AccessToken = refreshed.AccessToken,
            RefreshToken = refreshed.RefreshToken,
            ExpiresAt = refreshed.ExpiresAt,
            Scopes = refreshed.Scopes ?? account.Scopes,
            SubscriptionType = account.SubscriptionType,
            RateLimitTier = account.RateLimitTier,
            ExtraFields = account.ExtraFields,
        };
    }
}
