using System.Net.Http;
using System.Net.Http.Headers;

namespace ClaudeAccountSwitcher;

public sealed class UsageClient(HttpClient httpClient, TokenRefresher tokenRefresher, AccountStore accountStore)
{
    private const string UsageUrl = "https://api.anthropic.com/api/oauth/usage";

    public async Task<UsageInfo?> GetUsageAsync(string accountId, CancellationToken ct)
    {
        var account = accountStore.LoadAccount(accountId);
        if (account is null)
        {
            return null;
        }

        var fresh = await tokenRefresher.EnsureFreshAsync(account, ct);
        if (!ReferenceEquals(fresh, account))
        {
            accountStore.SaveAccount(accountId, fresh);
        }

        return await FetchAsync(fresh.AccessToken, ct);
    }

    // For the active account: call with the access token read straight off the live
    // credentials file (SwitcherService.ReadLiveAccount) instead of routing through
    // AccountStore/TokenRefresher — see ReadLiveAccount for why.
    public Task<UsageInfo?> GetUsageForLiveTokenAsync(string accessToken, CancellationToken ct) =>
        FetchAsync(accessToken, ct);

    private async Task<UsageInfo?> FetchAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        // Both headers required — without anthropic-beta the token is rejected outright,
        // without a claude-code User-Agent it gets rate-limited hard.
        request.Headers.Add("anthropic-beta", "oauth-2025-04-20");
        request.Headers.Add("User-Agent", "claude-code/1.0.0");

        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        return UsageParser.Parse(body);
    }
}
