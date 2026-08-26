namespace ClaudeAccountSwitcher;

public interface ITokenEndpointClient
{
    Task<StoredAccount> ExchangeCodeAsync(string code, string state, string codeVerifier, string redirectUri, CancellationToken ct);
    Task<StoredAccount> RefreshAsync(string refreshToken, CancellationToken ct);
}
