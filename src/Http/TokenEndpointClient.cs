using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

namespace ClaudeAccountSwitcher;

public sealed class TokenEndpointClient(HttpClient httpClient) : ITokenEndpointClient
{
    private const string TokenUrl = "https://console.anthropic.com/v1/oauth/token";
    private const string ClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";

    // The server requires "state" echoed back here too, despite it not being part of the OAuth2 spec.
    public Task<StoredAccount> ExchangeCodeAsync(string code, string state, string codeVerifier, string redirectUri, CancellationToken ct) =>
        PostAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["state"] = state,
            ["client_id"] = ClientId,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier,
        }, ct);

    public Task<StoredAccount> RefreshAsync(string refreshToken, CancellationToken ct) =>
        PostAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = ClientId,
        }, ct);

    private async Task<StoredAccount> PostAsync(Dictionary<string, string> payload, CancellationToken ct)
    {
        using var response = await httpClient.PostAsJsonAsync(TokenUrl, payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            // 400/401 on a refresh grant means the token itself is dead (invalid_grant),
            // distinct from a transient network/server error.
            if (payload["grant_type"] == "refresh_token" &&
                (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Unauthorized))
            {
                throw new RefreshTokenRevokedException();
            }
            response.EnsureSuccessStatusCode();
        }

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty token response");

        return new StoredAccount
        {
            AccessToken = body.access_token,
            RefreshToken = body.refresh_token,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(body.expires_in).ToUnixTimeMilliseconds(),
        };
    }

    // "scope" from this response is never consumed — see StoredAccount.
    private sealed record TokenResponse(string access_token, string refresh_token, long expires_in, string? scope);
}
