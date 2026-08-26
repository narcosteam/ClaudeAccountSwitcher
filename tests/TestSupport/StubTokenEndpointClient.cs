namespace ClaudeAccountSwitcher.Tests;

internal sealed class StubTokenEndpointClient : ITokenEndpointClient
{
    public StoredAccount? RefreshResult { get; set; }
    public string? LastRefreshTokenUsed { get; private set; }

    public Task<StoredAccount> ExchangeCodeAsync(string code, string state, string codeVerifier, string redirectUri, CancellationToken ct) =>
        throw new NotSupportedException("Not needed for these tests.");

    public Task<StoredAccount> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        LastRefreshTokenUsed = refreshToken;
        return Task.FromResult(RefreshResult ?? throw new InvalidOperationException("RefreshResult not set"));
    }
}
