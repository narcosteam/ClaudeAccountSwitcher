namespace ClaudeAccountSwitcher;

// Only "claude_pro" is live-confirmed; team/enterprise values are guessed.
public static class AccountLabeler
{
    public static bool IsTeamAccount(string? organizationType) =>
        organizationType is not null &&
        (organizationType.Contains("team", StringComparison.OrdinalIgnoreCase) ||
         organizationType.Contains("enterprise", StringComparison.OrdinalIgnoreCase));
}
