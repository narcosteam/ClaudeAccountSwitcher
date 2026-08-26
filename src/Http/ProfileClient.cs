using System.Net.Http;
using System.Net.Http.Headers;

namespace ClaudeAccountSwitcher;

public sealed class ProfileClient(HttpClient httpClient)
{
    private const string ProfileUrl = "https://api.anthropic.com/api/oauth/profile";

    public async Task<ProfileInfo?> GetProfileAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ProfileUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        // Same required headers as UsageClient.
        request.Headers.Add("anthropic-beta", "oauth-2025-04-20");
        request.Headers.Add("User-Agent", "claude-code/1.0.0");

        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        return ProfileParser.Parse(body);
    }
}
