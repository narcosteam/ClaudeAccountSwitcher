using ClaudeAccountSwitcher;
using Xunit;

namespace ClaudeAccountSwitcher.Tests;

public class ReleaseParserTests
{
    // GitHub's real /releases/latest response, trimmed to the fields we read.
    private const string RealShapeJson = """
    {"tag_name":"1.3.0","name":"1.3.0","prerelease":false,"assets":[{"name":"ClaudeAccountSwitcherSetup.exe","browser_download_url":"https://github.com/narcosteam/ClaudeAccountSwitcher/releases/download/1.3.0/ClaudeAccountSwitcherSetup.exe"}]}
    """;

    [Fact]
    public void Parse_ExtractsTagNameAndInstallerUrl()
    {
        var update = ReleaseParser.Parse(RealShapeJson);

        Assert.NotNull(update);
        Assert.Equal("1.3.0", update!.TagName);
        Assert.Equal("https://github.com/narcosteam/ClaudeAccountSwitcher/releases/download/1.3.0/ClaudeAccountSwitcherSetup.exe", update.InstallerUrl);
    }

    [Fact]
    public void Parse_SkipsNonExeAssets_AndPicksTheInstaller()
    {
        const string json = """
        {"tag_name":"1.3.0","assets":[
            {"name":"source.zip","browser_download_url":"https://example.com/source.zip"},
            {"name":"ClaudeAccountSwitcherSetup.exe","browser_download_url":"https://example.com/setup.exe"}
        ]}
        """;

        var update = ReleaseParser.Parse(json);

        Assert.Equal("https://example.com/setup.exe", update!.InstallerUrl);
    }

    [Fact]
    public void Parse_ReturnsNull_WhenNoExeAssetPresent()
    {
        const string json = """{"tag_name":"1.3.0","assets":[{"name":"source.zip","browser_download_url":"https://example.com/source.zip"}]}""";

        var update = ReleaseParser.Parse(json);

        Assert.Null(update);
    }

    [Fact]
    public void Parse_ReturnsNull_OnUnexpectedShape()
    {
        var update = ReleaseParser.Parse("""{"message":"Not Found"}""");

        Assert.Null(update);
    }
}
