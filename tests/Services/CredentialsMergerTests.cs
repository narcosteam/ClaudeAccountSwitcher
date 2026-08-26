using System.Text.Json;
using ClaudeAccountSwitcher;
using Xunit;

namespace ClaudeAccountSwitcher.Tests;

public class CredentialsMergerTests
{
    [Fact]
    public void Merge_ReplacesClaudeAiOauth_PreservingOtherTopLevelKeys()
    {
        const string original = """
        {"mcpOAuth":{"sentry":{"accessToken":"x"}},"claudeAiOauth":{"accessToken":"old"}}
        """;
        const string newOauth = """{"accessToken":"new","refreshToken":"r"}""";

        var result = CredentialsMerger.MergeClaudeAiOauth(original, newOauth);

        using var doc = JsonDocument.Parse(result);
        Assert.Equal("x", doc.RootElement.GetProperty("mcpOAuth").GetProperty("sentry").GetProperty("accessToken").GetString());
        Assert.Equal("new", doc.RootElement.GetProperty("claudeAiOauth").GetProperty("accessToken").GetString());
    }

    [Fact]
    public void Merge_AddsClaudeAiOauth_WhenOriginalIsEmpty()
    {
        var result = CredentialsMerger.MergeClaudeAiOauth("", """{"accessToken":"new"}""");

        using var doc = JsonDocument.Parse(result);
        Assert.Equal("new", doc.RootElement.GetProperty("claudeAiOauth").GetProperty("accessToken").GetString());
    }

    [Fact]
    public void Merge_AddsClaudeAiOauth_WhenOriginalHasNoOauthKeyYet()
    {
        const string original = """{"mcpOAuth":{}}""";

        var result = CredentialsMerger.MergeClaudeAiOauth(original, """{"accessToken":"new"}""");

        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("mcpOAuth", out _));
        Assert.Equal("new", doc.RootElement.GetProperty("claudeAiOauth").GetProperty("accessToken").GetString());
    }
}
