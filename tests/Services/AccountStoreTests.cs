using System.Text.Json;
using ClaudeAccountSwitcher;
using Xunit;

namespace ClaudeAccountSwitcher.Tests;

public class AccountStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cas-tests-" + Guid.NewGuid());
    private readonly AccountStore _store;

    public AccountStoreTests()
    {
        _store = new AccountStore(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void AddAccount_ThenLoadAccount_RoundTripsData()
    {
        var account = new StoredAccount { AccessToken = "at", RefreshToken = "rt", ExpiresAt = 123 };

        var id = _store.AddAccount("Work", account);
        var loaded = _store.LoadAccount(id);

        Assert.Equal("at", loaded!.AccessToken);
        Assert.Equal("rt", loaded.RefreshToken);
        Assert.Equal(123, loaded.ExpiresAt);
    }

    [Fact]
    public void AddAccount_AppearsInIndex()
    {
        _store.AddAccount("Personal", new StoredAccount { AccessToken = "a", RefreshToken = "b", ExpiresAt = 0 });

        var entries = _store.ListAccounts();

        Assert.Single(entries);
        Assert.Equal("Personal", entries[0].Label);
    }

    [Fact]
    public void ActiveAccountId_DefaultsToNull_ThenPersistsAfterSet()
    {
        Assert.Null(_store.GetActiveAccountId());

        _store.SetActiveAccountId("abc123");

        Assert.Equal("abc123", _store.GetActiveAccountId());
    }

    [Fact]
    public void StoredFile_OnDisk_IsNotPlaintext()
    {
        var id = _store.AddAccount("Work", new StoredAccount { AccessToken = "super-secret-token", RefreshToken = "r", ExpiresAt = 0 });

        var raw = File.ReadAllText(Path.Combine(_dir, "accounts", $"{id}.dat"));

        Assert.DoesNotContain("super-secret-token", raw);
    }

    [Fact]
    public void LoadAccount_ReturnsNull_ForUnknownId()
    {
        Assert.Null(_store.LoadAccount("does-not-exist"));
    }

    [Fact]
    public void AddAccount_WithMetadata_RoundTripsThroughIndex()
    {
        var id = _store.AddAccount("Work", new StoredAccount { AccessToken = "a", RefreshToken = "b", ExpiresAt = 0 },
            email: "work@example.com", organizationUuid: "org-1", isTeamAccount: true);

        var entry = _store.ListAccounts().Single(e => e.Id == id);

        Assert.Equal("work@example.com", entry.Email);
        Assert.Equal("org-1", entry.OrganizationUuid);
        Assert.True(entry.IsTeamAccount);
    }

    [Fact]
    public void FindByOrganizationUuid_ReturnsMatchingEntry()
    {
        _store.AddAccount("Work", new StoredAccount { AccessToken = "a", RefreshToken = "b", ExpiresAt = 0 }, organizationUuid: "org-1");

        var found = _store.FindByOrganizationUuid("org-1");

        Assert.NotNull(found);
        Assert.Equal("Work", found!.Label);
    }

    [Fact]
    public void FindByOrganizationUuid_ReturnsNull_WhenNoMatch()
    {
        Assert.Null(_store.FindByOrganizationUuid("does-not-exist"));
    }

    [Fact]
    public void RenameAccount_UpdatesLabel()
    {
        var id = _store.AddAccount("Old Name", new StoredAccount { AccessToken = "a", RefreshToken = "b", ExpiresAt = 0 });

        _store.RenameAccount(id, "New Name");

        Assert.Equal("New Name", _store.ListAccounts().Single().Label);
    }

    [Fact]
    public void RenameAccount_Throws_WhenIdUnknown()
    {
        Assert.Throws<InvalidOperationException>(() => _store.RenameAccount("does-not-exist", "New Name"));
    }

    [Fact]
    public void ListAccounts_DeserializesOldIndexFormat_WithoutNewFields()
    {
        var oldFormatJson = """[{"id":"abc123","label":"Legacy","addedAt":"2026-01-01T00:00:00+00:00"}]""";
        File.WriteAllText(Path.Combine(_dir, "accounts.json"), oldFormatJson);

        var entries = _store.ListAccounts();

        Assert.Single(entries);
        Assert.Equal("Legacy", entries[0].Label);
        Assert.Null(entries[0].Email);
        Assert.Null(entries[0].OrganizationUuid);
        Assert.False(entries[0].IsTeamAccount);
    }

    [Fact]
    public void RemoveAccount_RemovesFromIndexAndDeletesFile()
    {
        var id = _store.AddAccount("Work", new StoredAccount { AccessToken = "a", RefreshToken = "b", ExpiresAt = 0 });

        _store.RemoveAccount(id);

        Assert.Empty(_store.ListAccounts());
        Assert.Null(_store.LoadAccount(id));
    }

    [Fact]
    public void RemoveAccount_ClearsActiveAccountId_WhenRemovingTheActiveAccount()
    {
        var id = _store.AddAccount("Work", new StoredAccount { AccessToken = "a", RefreshToken = "b", ExpiresAt = 0 });
        _store.SetActiveAccountId(id);

        _store.RemoveAccount(id);

        Assert.Null(_store.GetActiveAccountId());
    }

    [Fact]
    public void RemoveAccount_LeavesActiveAccountId_WhenRemovingADifferentAccount()
    {
        var activeId = _store.AddAccount("Work", new StoredAccount { AccessToken = "a", RefreshToken = "b", ExpiresAt = 0 });
        var otherId = _store.AddAccount("Personal", new StoredAccount { AccessToken = "c", RefreshToken = "d", ExpiresAt = 0 });
        _store.SetActiveAccountId(activeId);

        _store.RemoveAccount(otherId);

        Assert.Equal(activeId, _store.GetActiveAccountId());
    }

    [Fact]
    public void RemoveAccount_DoesNothing_ForUnknownId()
    {
        _store.AddAccount("Work", new StoredAccount { AccessToken = "a", RefreshToken = "b", ExpiresAt = 0 });

        _store.RemoveAccount("does-not-exist");

        Assert.Single(_store.ListAccounts());
    }

    [Fact]
    public void SaveAccount_ThenLoadAccount_RoundTripsExtraFields()
    {
        var account = new StoredAccount
        {
            AccessToken = "at",
            RefreshToken = "rt",
            ExpiresAt = 0,
            ExtraFields = new Dictionary<string, JsonElement> { ["someFutureField"] = JsonSerializer.SerializeToElement("keep-me") },
        };

        var id = _store.AddAccount("Work", account);
        var loaded = _store.LoadAccount(id);

        Assert.Equal("keep-me", loaded!.ExtraFields!["someFutureField"].GetString());
    }
}
