using System.Text.Json;
using ClaudeAccountSwitcher;
using Xunit;

namespace ClaudeAccountSwitcher.Tests;

public class SwitcherServiceTests : IDisposable
{
    private readonly string _storeDir = Path.Combine(Path.GetTempPath(), "cas-tests-" + Guid.NewGuid());
    private readonly string _credentialsPath = Path.Combine(Path.GetTempPath(), "cas-creds-" + Guid.NewGuid() + ".json");
    private readonly AccountStore _store;
    private readonly SwitcherService _switcher;

    public SwitcherServiceTests()
    {
        _store = new AccountStore(_storeDir);
        _switcher = new SwitcherService(_store, _credentialsPath);
    }

    public void Dispose()
    {
        Directory.Delete(_storeDir, recursive: true);
        if (File.Exists(_credentialsPath))
        {
            File.Delete(_credentialsPath);
        }
    }

    [Fact]
    public void SwitchTo_WritesTargetAccountIntoCredentialsFile_PreservingOtherKeys()
    {
        File.WriteAllText(_credentialsPath, """{"mcpOAuth":{"sentry":{"accessToken":"x"}},"claudeAiOauth":{"accessToken":"old"}}""");
        var targetId = _store.AddAccount("Personal", new StoredAccount { AccessToken = "new-token", RefreshToken = "new-refresh", ExpiresAt = 999 });

        _switcher.SwitchTo(targetId);

        using var doc = JsonDocument.Parse(File.ReadAllText(_credentialsPath));
        Assert.Equal("new-token", doc.RootElement.GetProperty("claudeAiOauth").GetProperty("accessToken").GetString());
        Assert.Equal("x", doc.RootElement.GetProperty("mcpOAuth").GetProperty("sentry").GetProperty("accessToken").GetString());
    }

    [Fact]
    public void SwitchTo_SavesCurrentCredentialsBackIntoPreviouslyActiveAccount()
    {
        var accountAId = _store.AddAccount("Work", new StoredAccount { AccessToken = "a-old", RefreshToken = "a-old-r", ExpiresAt = 1 });
        var accountBId = _store.AddAccount("Personal", new StoredAccount { AccessToken = "b-token", RefreshToken = "b-r", ExpiresAt = 2 });
        _store.SetActiveAccountId(accountAId);

        // Simulate Claude Code itself having refreshed account A's token while it was active.
        File.WriteAllText(_credentialsPath, """{"claudeAiOauth":{"accessToken":"a-refreshed-by-claude","refreshToken":"a-refreshed-r","expiresAt":123}}""");

        _switcher.SwitchTo(accountBId);

        var savedA = _store.LoadAccount(accountAId);
        Assert.Equal("a-refreshed-by-claude", savedA!.AccessToken);
    }

    [Fact]
    public void SwitchTo_PreservesUnknownFieldsInPreviouslyActiveAccountsOauthBlock()
    {
        var accountAId = _store.AddAccount("Work", new StoredAccount { AccessToken = "a-old", RefreshToken = "a-old-r", ExpiresAt = 1 });
        var accountBId = _store.AddAccount("Personal", new StoredAccount { AccessToken = "b-token", RefreshToken = "b-r", ExpiresAt = 2 });
        _store.SetActiveAccountId(accountAId);

        // Simulates a real claudeAiOauth block containing a field this app's
        // StoredAccount doesn't have an explicit property for.
        File.WriteAllText(_credentialsPath, """{"claudeAiOauth":{"accessToken":"a-refreshed","refreshToken":"a-refreshed-r","expiresAt":123,"someFutureField":"keep-me"}}""");

        _switcher.SwitchTo(accountBId);

        var savedA = _store.LoadAccount(accountAId);
        Assert.Equal("a-refreshed", savedA!.AccessToken);
        Assert.NotNull(savedA.ExtraFields);
        Assert.Equal("keep-me", savedA.ExtraFields!["someFutureField"].GetString());
    }

    [Fact]
    public void SwitchTo_UpdatesActiveAccountId()
    {
        var targetId = _store.AddAccount("Personal", new StoredAccount { AccessToken = "t", RefreshToken = "r", ExpiresAt = 0 });

        _switcher.SwitchTo(targetId);

        Assert.Equal(targetId, _store.GetActiveAccountId());
    }

    [Fact]
    public void SwitchTo_CreatesCredentialsFile_WhenNoneExists()
    {
        var targetId = _store.AddAccount("Personal", new StoredAccount { AccessToken = "t", RefreshToken = "r", ExpiresAt = 0 });

        _switcher.SwitchTo(targetId);

        Assert.True(File.Exists(_credentialsPath));
    }

    [Fact]
    public void SwitchTo_Throws_WhenTargetAccountUnknown()
    {
        Assert.Throws<InvalidOperationException>(() => _switcher.SwitchTo("does-not-exist"));
    }

    [Fact]
    public void SignOut_RemovesAccount()
    {
        var id = _store.AddAccount("Personal", new StoredAccount { AccessToken = "t", RefreshToken = "r", ExpiresAt = 0 });

        _switcher.SignOut(id);

        Assert.Empty(_store.ListAccounts());
    }

    [Fact]
    public void SignOut_ClearsCredentialsFile_WhenSigningOutOfActiveAccount()
    {
        var id = _store.AddAccount("Personal", new StoredAccount { AccessToken = "t", RefreshToken = "r", ExpiresAt = 0 });
        _switcher.SwitchTo(id);

        _switcher.SignOut(id);

        using var doc = JsonDocument.Parse(File.ReadAllText(_credentialsPath));
        Assert.False(doc.RootElement.TryGetProperty("claudeAiOauth", out _));
    }

    [Fact]
    public void SignOut_LeavesCredentialsFileUntouched_WhenSigningOutOfInactiveAccount()
    {
        var activeId = _store.AddAccount("Work", new StoredAccount { AccessToken = "a", RefreshToken = "b", ExpiresAt = 0 });
        var otherId = _store.AddAccount("Personal", new StoredAccount { AccessToken = "c", RefreshToken = "d", ExpiresAt = 0 });
        _switcher.SwitchTo(activeId);

        _switcher.SignOut(otherId);

        using var doc = JsonDocument.Parse(File.ReadAllText(_credentialsPath));
        Assert.Equal("a", doc.RootElement.GetProperty("claudeAiOauth").GetProperty("accessToken").GetString());
    }
}
