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

        using var request = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fresh.AccessToken);
        // ponytail: both headers are required. Without anthropic-beta the
        // endpoint rejects the OAuth token outright; without a claude-code
        // User-Agent it drops the caller into an aggressively rate-limited
        // bucket that returns persistent 429s even at slow poll rates.
        request.Headers.Add("anthropic-beta", "oauth-2025-04-20");
        request.Headers.Add("User-Agent", "claude-code/1.0.0");

        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            // ponytail: caller (tray menu) just shows "limits unavailable" — no retry loop.
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        return UsageParser.Parse(body);
    }
}
