using ClaudeAccountSwitcher;
using Xunit;

namespace ClaudeAccountSwitcher.Tests;

public class ProfileParserTests
{
    private const string RealShapeJson = """
    {"account":{"uuid":"61e94f37-088d-4f55-9a89-7bf5b3eca4bd","full_name":"Vladyslav Smirnov","display_name":"Vlad","email":"vsmirnov116@gmail.com","has_claude_max":false,"has_claude_pro":true},"organization":{"uuid":"7ce1960d-97b6-4d29-a19b-4842f8caf1a3","name":"vsmirnov116@gmail.com's Organization","organization_type":"claude_pro","rate_limit_tier":"default_claude_ai"},"application":{"uuid":"9d1c250a-e61b-44d9-88ed-5944d1962f5e","name":"Claude Code"}}
    """;

    [Fact]
    public void Parse_ExtractsDisplayNameEmailAndOrganizationUuid()
    {
        var profile = ProfileParser.Parse(RealShapeJson);

        Assert.NotNull(profile);
        Assert.Equal("Vlad", profile!.DisplayName);
        Assert.Equal("vsmirnov116@gmail.com", profile.Email);
        Assert.Equal("7ce1960d-97b6-4d29-a19b-4842f8caf1a3", profile.OrganizationUuid);
    }

    [Fact]
    public void Parse_SetsIsTeamAccountFalse_ForClaudeProType()
    {
        var profile = ProfileParser.Parse(RealShapeJson);

        Assert.False(profile!.IsTeamAccount);
    }

    [Fact]
    public void Parse_ExtractsRateLimitTierVerbatim()
    {
        var profile = ProfileParser.Parse(RealShapeJson);

        Assert.Equal("default_claude_ai", profile!.RateLimitTier);
    }

    [Fact]
    public void Parse_StripsClaudePrefixFromOrganizationType_ForSubscriptionType()
    {
        var profile = ProfileParser.Parse(RealShapeJson);

        Assert.Equal("pro", profile!.SubscriptionType);
    }

    [Fact]
    public void Parse_SetsIsTeamAccountTrue_ForTeamType()
    {
        const string json = """
        {"account":{"display_name":"Sarah","email":"sarah@acme.com"},"organization":{"uuid":"org-1","organization_type":"claude_team"}}
        """;

        var profile = ProfileParser.Parse(json);

        Assert.True(profile!.IsTeamAccount);
    }

    [Fact]
    public void Parse_ReturnsNull_WhenAccountKeyMissing()
    {
        Assert.Null(ProfileParser.Parse("""{"organization":{"uuid":"org-1"}}"""));
    }

    [Fact]
    public void Parse_ReturnsNull_WhenOrganizationKeyMissing()
    {
        Assert.Null(ProfileParser.Parse("""{"account":{"display_name":"Vlad","email":"v@x.com"}}"""));
    }

    [Fact]
    public void Parse_ReturnsNull_WhenDisplayNameMissing()
    {
        const string json = """{"account":{"email":"v@x.com"},"organization":{"uuid":"org-1"}}""";

        Assert.Null(ProfileParser.Parse(json));
    }
}
