using System.Net.Http;

namespace ClaudeAccountSwitcher;

public sealed class UpdateChecker(HttpClient httpClient, Version currentVersion)
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/narcosteam/ClaudeAccountSwitcher/releases/latest";

    // Null means both "no update" and "check failed" — callers don't need to tell them apart.
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
        catch (Exception)
        {
            return null;
        }
    }
}
