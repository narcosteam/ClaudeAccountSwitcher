using ClaudeAccountSwitcher;
using Xunit;

namespace ClaudeAccountSwitcher.Tests;

public class UsageClientTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cas-tests-" + Guid.NewGuid());

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task GetUsageAsync_ReturnsParsedUsage_ForFreshToken()
    {
        var store = new AccountStore(_dir);
        var id = store.AddAccount("Work", new StoredAccount
        {
            AccessToken = "at",
            RefreshToken = "rt",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds(),
        });
        var handler = new FakeHttpMessageHandler("""{"five_hour":{"utilization":5,"resets_at":"2026-08-23T22:00:00Z"}}""");
        var client = new UsageClient(new HttpClient(handler), new TokenRefresher(new StubTokenEndpointClient()), store);

        var usage = await client.GetUsageAsync(id, CancellationToken.None);

        Assert.Equal(5, usage!.FiveHour!.UsedPercentage);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsNull_ForUnknownAccount()
    {
        var store = new AccountStore(_dir);
        var client = new UsageClient(new HttpClient(new FakeHttpMessageHandler("{}")), new TokenRefresher(new StubTokenEndpointClient()), store);

        var usage = await client.GetUsageAsync("does-not-exist", CancellationToken.None);

        Assert.Null(usage);
    }

    [Fact]
    public async Task GetUsageAsync_RefreshesExpiredToken_AndPersistsIt()
    {
        var store = new AccountStore(_dir);
        var id = store.AddAccount("Work", new StoredAccount
        {
            AccessToken = "stale-at",
            RefreshToken = "rt",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds(),
        });
        var stub = new StubTokenEndpointClient
        {
            RefreshResult = new StoredAccount
            {
                AccessToken = "fresh-at",
                RefreshToken = "rt",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds(),
            },
        };
        var handler = new FakeHttpMessageHandler("""{"five_hour":{"utilization":5,"resets_at":"2026-08-23T22:00:00Z"}}""");
        var client = new UsageClient(new HttpClient(handler), new TokenRefresher(stub), store);

        var usage = await client.GetUsageAsync(id, CancellationToken.None);

        Assert.Equal(5, usage!.FiveHour!.UsedPercentage);
        Assert.Equal("rt", stub.LastRefreshTokenUsed);
        Assert.Equal("fresh-at", store.LoadAccount(id)!.AccessToken);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsNull_WhenServerRejectsRequest()
    {
        var store = new AccountStore(_dir);
        var id = store.AddAccount("Work", new StoredAccount
        {
            AccessToken = "at",
            RefreshToken = "rt",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds(),
        });
        var handler = new FakeHttpMessageHandler("""{"error":"unauthorized"}""", System.Net.HttpStatusCode.Unauthorized);
        var client = new UsageClient(new HttpClient(handler), new TokenRefresher(new StubTokenEndpointClient()), store);

        var usage = await client.GetUsageAsync(id, CancellationToken.None);

        Assert.Null(usage);
    }
}
