namespace ClaudeAccountSwitcher;

public sealed class TokenRefresher(ITokenEndpointClient tokenEndpoint)
{
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromMinutes(2);

    public bool IsExpiringSoon(StoredAccount account) =>
        DateTimeOffset.FromUnixTimeMilliseconds(account.ExpiresAt) <= DateTimeOffset.UtcNow.Add(ExpiryBuffer);

    public Task<StoredAccount> EnsureFreshAsync(StoredAccount account, CancellationToken ct) =>
        IsExpiringSoon(account)
            ? tokenEndpoint.RefreshAsync(account.RefreshToken, ct)
            : Task.FromResult(account);
}
