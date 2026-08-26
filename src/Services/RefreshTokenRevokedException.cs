namespace ClaudeAccountSwitcher;

// The refresh token is dead (400/401, not a transient error) — account needs re-authorization.
public sealed class RefreshTokenRevokedException : Exception;
