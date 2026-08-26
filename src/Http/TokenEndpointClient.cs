using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

namespace ClaudeAccountSwitcher;

public sealed class TokenEndpointClient(HttpClient httpClient) : ITokenEndpointClient
{
    private const string TokenUrl = "https://console.anthropic.com/v1/oauth/token";
    private const string ClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";

    public Task<StoredAccount> ExchangeCodeAsync(string code, string state, string codeVerifier, string redirectUri, CancellationToken ct) =>
        // ponytail: the real token endpoint returned 400 Bad Request without
        // "state" in the exchange body — even though it's not part of the
        // OAuth 2.0 authorization_code grant spec, the real server expects
        // it echoed back here alongside code/code_verifier.
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
            // ponytail: a refresh_token grant rejected with 400/401 means the
            // refresh token itself is dead (revoked, expired past its own
            // lifetime, or issued to a different client) — OAuth2's
            // invalid_grant. Distinguish this from a transient network/server
            // error so callers can tell "sign in again" apart from "try later".
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
            Scopes = body.scope,
        };
    }

    private sealed record TokenResponse(string access_token, string refresh_token, long expires_in, string? scope);
}
