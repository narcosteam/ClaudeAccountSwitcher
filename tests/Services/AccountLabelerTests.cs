using ClaudeAccountSwitcher;
using Xunit;

namespace ClaudeAccountSwitcher.Tests;

public class AccountLabelerTests
{
    [Fact]
    public void IsTeamAccount_FalseForConfirmedPersonalType()
    {
        Assert.False(AccountLabeler.IsTeamAccount("claude_pro"));
    }

    [Fact]
    public void IsTeamAccount_TrueWhenTypeContainsTeam()
    {
        Assert.True(AccountLabeler.IsTeamAccount("claude_team"));
    }

    [Fact]
    public void IsTeamAccount_TrueWhenTypeContainsEnterprise()
    {
        Assert.True(AccountLabeler.IsTeamAccount("claude_enterprise"));
    }

    [Fact]
    public void IsTeamAccount_IsCaseInsensitive()
    {
        Assert.True(AccountLabeler.IsTeamAccount("CLAUDE_TEAM"));
    }

    [Fact]
    public void IsTeamAccount_FalseForNull()
    {
        Assert.False(AccountLabeler.IsTeamAccount(null));
    }
}
