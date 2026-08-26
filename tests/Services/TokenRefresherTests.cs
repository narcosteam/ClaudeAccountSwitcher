using ClaudeAccountSwitcher;
using Xunit;

namespace ClaudeAccountSwitcher.Tests;

public class TokenRefresherTests
{
    [Fact]
    public void IsExpiringSoon_TrueWhenExpiryInPast()
    {
        var refresher = new TokenRefresher(new StubTokenEndpointClient());
        var account = new StoredAccount { AccessToken = "a", RefreshToken = "r", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds() };

        Assert.True(refresher.IsExpiringSoon(account));
    }

    [Fact]
    public void IsExpiringSoon_FalseWhenExpiryFarInFuture()
    {
        var refresher = new TokenRefresher(new StubTokenEndpointClient());
        var account = new StoredAccount { AccessToken = "a", RefreshToken = "r", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds() };

        Assert.False(refresher.IsExpiringSoon(account));
    }

    [Fact]
    public async Task EnsureFreshAsync_RefreshesWhenExpiringSoon()
    {
        var stub = new StubTokenEndpointClient { RefreshResult = new StoredAccount { AccessToken = "new", RefreshToken = "new-r", ExpiresAt = 0 } };
        var refresher = new TokenRefresher(stub);
        var account = new StoredAccount { AccessToken = "old", RefreshToken = "old-r", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds() };

        var result = await refresher.EnsureFreshAsync(account, CancellationToken.None);

        Assert.Equal("new", result.AccessToken);
        Assert.Equal("old-r", stub.LastRefreshTokenUsed);
    }

    [Fact]
    public async Task EnsureFreshAsync_PreservesSubscriptionFields_AcrossRefresh()
    {
        // Real refresh responses never include subscriptionType/rateLimitTier/extension fields.
        var stub = new StubTokenEndpointClient { RefreshResult = new StoredAccount { AccessToken = "new", RefreshToken = "new-r", ExpiresAt = 0 } };
        var refresher = new TokenRefresher(stub);
        var account = new StoredAccount
        {
            AccessToken = "old",
            RefreshToken = "old-r",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds(),
            SubscriptionType = "pro",
            RateLimitTier = "tier_1",
            ExtraFields = new() { ["futureField"] = System.Text.Json.JsonDocument.Parse("\"x\"").RootElement },
        };

        var result = await refresher.EnsureFreshAsync(account, CancellationToken.None);

        Assert.Equal("new", result.AccessToken);
        Assert.Equal("pro", result.SubscriptionType);
        Assert.Equal("tier_1", result.RateLimitTier);
        Assert.True(result.ExtraFields?.ContainsKey("futureField"));
    }

    [Fact]
    public async Task EnsureFreshAsync_ReturnsSameInstance_WhenNotExpiring()
    {
        var stub = new StubTokenEndpointClient();
        var refresher = new TokenRefresher(stub);
        var account = new StoredAccount { AccessToken = "a", RefreshToken = "r", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds() };

        var result = await refresher.EnsureFreshAsync(account, CancellationToken.None);

        Assert.Same(account, result);
        Assert.Null(stub.LastRefreshTokenUsed);
    }
}
