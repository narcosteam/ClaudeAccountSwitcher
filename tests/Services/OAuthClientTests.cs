using ClaudeAccountSwitcher;
using Xunit;

namespace ClaudeAccountSwitcher.Tests;

public class OAuthClientTests
{
    [Fact]
    public void BuildAuthorizeUri_IncludesRequiredPkceAndClientParams()
    {
        var uri = OAuthClient.BuildAuthorizeUri("challenge123", "state456", "http://localhost:5000/callback");

        Assert.StartsWith("https://claude.ai/oauth/authorize?", uri);
        Assert.Contains("code=true", uri);
        Assert.Contains("client_id=9d1c250a-e61b-44d9-88ed-5944d1962f5e", uri);
        Assert.Contains("code_challenge=challenge123", uri);
        Assert.Contains("code_challenge_method=S256", uri);
        Assert.Contains("state=state456", uri);
        Assert.Contains("redirect_uri=http%3A%2F%2Flocalhost%3A5000%2Fcallback", uri);
        Assert.Contains("scope=org%3Acreate_api_key", uri);
        Assert.Contains("user%3Asessions%3Aclaude_code", uri);
    }

    [Fact]
    public void ParseQueryString_ExtractsCodeAndState()
    {
        var result = OAuthClient.ParseQueryString("code=abc123&state=xyz789");

        Assert.Equal("abc123", result["code"]);
        Assert.Equal("xyz789", result["state"]);
    }

    [Fact]
    public void ParseQueryString_UnescapesPercentEncodedValues()
    {
        var result = OAuthClient.ParseQueryString("code=a%2Bb%3Dc");

        Assert.Equal("a+b=c", result["code"]);
    }

    [Fact]
    public void ParseQueryString_ReturnsEmptyDictionary_ForEmptyQuery()
    {
        var result = OAuthClient.ParseQueryString("");

        Assert.Empty(result);
    }
}
