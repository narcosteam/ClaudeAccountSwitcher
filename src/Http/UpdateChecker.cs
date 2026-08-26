using System.Net.Http;

namespace ClaudeAccountSwitcher;

public sealed class UpdateChecker(HttpClient httpClient, Version currentVersion)
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/narcosteam/ClaudeAccountSwitcher/releases/latest";

    // ponytail: returns null both when there's no newer release AND when the
    // check itself fails (network down, API rate-limited, no installer asset
    // attached) — callers don't need to distinguish "up to date" from
    // "couldn't tell", they just don't nag the user either way.
    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.Add("User-Agent", "ClaudeAccountSwitcher-UpdateChecker");

            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            var update = ReleaseParser.Parse(body);
            if (update is null || !Version.TryParse(update.TagName, out var latestVersion))
            {
                return null;
            }

            return latestVersion > currentVersion ? update : null;
        }
        catch (Exception) // ponytail: transient network/parse failure — same as UsageClient's "leave it, try again next cycle"
        {
            return null;
        }
    }
}
