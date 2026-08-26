namespace ClaudeAccountSwitcher;

// Thrown when the OAuth server rejects a refresh_token grant with a
// definitive "this token is invalid" response (400/401) — as opposed to a
// network failure or transient server error. Signals that the account needs
// the user to sign in again; there is no way to recover without that.
public sealed class RefreshTokenRevokedException : Exception;
