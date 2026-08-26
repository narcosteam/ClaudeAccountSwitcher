namespace ClaudeAccountSwitcher;

// ponytail: heuristic based on the one live-confirmed value ("claude_pro" ->
// personal). team/enterprise organization_type values are NOT verified live
// — guessed from Anthropic's public plan naming. Revisit this substring list
// if a team/enterprise account is ever tested against it.
public static class AccountLabeler
{
    public static bool IsTeamAccount(string? organizationType) =>
        organizationType is not null &&
        (organizationType.Contains("team", StringComparison.OrdinalIgnoreCase) ||
         organizationType.Contains("enterprise", StringComparison.OrdinalIgnoreCase));
}
