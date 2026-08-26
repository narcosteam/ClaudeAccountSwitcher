using ClaudeAccountSwitcher;
using Xunit;

namespace ClaudeAccountSwitcher.Tests;

public class ProfileClientTests
{
    private const string RealShapeJson = """
    {"account":{"display_name":"Vlad","email":"vsmirnov116@gmail.com"},"organization":{"uuid":"org-1","organization_type":"claude_pro"}}
    """;

    [Fact]
    public async Task GetProfileAsync_ReturnsParsedProfile_OnSuccess()
    {
        var handler = new FakeHttpMessageHandler(RealShapeJson);
        var client = new ProfileClient(new HttpClient(handler));

        var profile = await client.GetProfileAsync("some-access-token", CancellationToken.None);

        Assert.Equal("Vlad", profile!.DisplayName);
    }

    [Fact]
    public async Task GetProfileAsync_ReturnsNull_WhenServerRejectsRequest()
    {
        var handler = new FakeHttpMessageHandler("""{"error":"unauthorized"}""", System.Net.HttpStatusCode.Unauthorized);
        var client = new ProfileClient(new HttpClient(handler));

        var profile = await client.GetProfileAsync("bad-token", CancellationToken.None);

        Assert.Null(profile);
    }
}
