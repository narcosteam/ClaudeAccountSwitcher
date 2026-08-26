using ClaudeAccountSwitcher;
using Xunit;

namespace ClaudeAccountSwitcher.Tests;

public class UpdateCheckerTests
{
    private const string ReleaseJson = """
    {"tag_name":"1.3.0","assets":[{"name":"ClaudeAccountSwitcherSetup.exe","browser_download_url":"https://example.com/setup.exe"}]}
    """;

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsUpdate_WhenLatestIsNewer()
    {
        var handler = new FakeHttpMessageHandler(ReleaseJson);
        var checker = new UpdateChecker(new HttpClient(handler), currentVersion: new Version(1, 2, 0));

        var update = await checker.CheckForUpdateAsync(CancellationToken.None);

        Assert.NotNull(update);
        Assert.Equal("1.3.0", update!.TagName);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsNull_WhenCurrentIsUpToDate()
    {
        var handler = new FakeHttpMessageHandler(ReleaseJson);
        var checker = new UpdateChecker(new HttpClient(handler), currentVersion: new Version(1, 3, 0));

        var update = await checker.CheckForUpdateAsync(CancellationToken.None);

        Assert.Null(update);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsNull_WhenCurrentIsNewerThanLatest()
    {
        var handler = new FakeHttpMessageHandler(ReleaseJson);
        var checker = new UpdateChecker(new HttpClient(handler), currentVersion: new Version(2, 0, 0));

        var update = await checker.CheckForUpdateAsync(CancellationToken.None);

        Assert.Null(update);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsNull_OnServerError()
    {
        var handler = new FakeHttpMessageHandler("""{"message":"rate limited"}""", System.Net.HttpStatusCode.Forbidden);
        var checker = new UpdateChecker(new HttpClient(handler), currentVersion: new Version(1, 0, 0));

        var update = await checker.CheckForUpdateAsync(CancellationToken.None);

        Assert.Null(update);
    }
}
