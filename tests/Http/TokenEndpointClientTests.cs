using ClaudeAccountSwitcher;
using Xunit;

namespace ClaudeAccountSwitcher.Tests;

public class TokenEndpointClientTests
{
    [Fact]
    public async Task ExchangeCodeAsync_ParsesTokenResponse()
    {
        var handler = new FakeHttpMessageHandler("""{"access_token":"at1","refresh_token":"rt1","expires_in":3600,"scope":"user:inference"}""");
        var client = new TokenEndpointClient(new HttpClient(handler));

        var account = await client.ExchangeCodeAsync("code123", "state123", "verifier123", "http://localhost:5000/callback/", CancellationToken.None);

        Assert.Equal("at1", account.AccessToken);
        Assert.Equal("rt1", account.RefreshToken);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ConvertsSpaceSeparatedScopeIntoScopesArray()
    {
        // Real .credentials.json stores "scopes" as an array; Claude Code treats a
        // missing "scopes" array as not-logged-in even with a valid accessToken.
        var handler = new FakeHttpMessageHandler("""{"access_token":"at1","refresh_token":"rt1","expires_in":3600,"scope":"user:profile user:inference"}""");
        var client = new TokenEndpointClient(new HttpClient(handler));

        var account = await client.ExchangeCodeAsync("code123", "state123", "verifier123", "http://localhost:5000/callback/", CancellationToken.None);

        var scopes = account.ExtraFields!["scopes"].EnumerateArray().Select(e => e.GetString()!).ToArray();
        Assert.Equal(["user:profile", "user:inference"], scopes);
    }

    [Fact]
    public async Task RefreshAsync_ParsesTokenResponse()
    {
        var handler = new FakeHttpMessageHandler("""{"access_token":"at2","refresh_token":"rt2","expires_in":3600}""");
        var client = new TokenEndpointClient(new HttpClient(handler));

        var account = await client.RefreshAsync("old-refresh", CancellationToken.None);

        Assert.Equal("at2", account.AccessToken);
        Assert.Equal("rt2", account.RefreshToken);
    }

    [Fact]
    public async Task RefreshAsync_ThrowsRefreshTokenRevokedException_On400()
    {
        var handler = new FakeHttpMessageHandler("""{"error":"invalid_grant"}""", System.Net.HttpStatusCode.BadRequest);
        var client = new TokenEndpointClient(new HttpClient(handler));

        await Assert.ThrowsAsync<RefreshTokenRevokedException>(() => client.RefreshAsync("dead-refresh-token", CancellationToken.None));
    }

    [Fact]
    public async Task RefreshAsync_ThrowsRefreshTokenRevokedException_On401()
    {
        var handler = new FakeHttpMessageHandler("""{"error":"invalid_grant"}""", System.Net.HttpStatusCode.Unauthorized);
        var client = new TokenEndpointClient(new HttpClient(handler));

        await Assert.ThrowsAsync<RefreshTokenRevokedException>(() => client.RefreshAsync("dead-refresh-token", CancellationToken.None));
    }

    [Fact]
    public async Task RefreshAsync_ThrowsGenericException_ForTransientServerError()
    {
        var handler = new FakeHttpMessageHandler("""{"error":"server_error"}""", System.Net.HttpStatusCode.InternalServerError);
        var client = new TokenEndpointClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => client.RefreshAsync("some-refresh-token", CancellationToken.None));
        Assert.IsNotType<RefreshTokenRevokedException>(ex);
    }

    [Fact]
    public async Task ExchangeCodeAsync_DoesNotThrowRefreshTokenRevokedException_On400()
    {
        var handler = new FakeHttpMessageHandler("""{"error":"invalid_grant"}""", System.Net.HttpStatusCode.BadRequest);
        var client = new TokenEndpointClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            client.ExchangeCodeAsync("code", "state", "verifier", "http://localhost/callback", CancellationToken.None));
        Assert.IsNotType<RefreshTokenRevokedException>(ex);
    }
}
