using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClaudeAccountSwitcher;

public sealed class AccountStore
{
    private readonly string _accountsDir;
    private readonly string _indexPath;
    private readonly string _activePath;

    public AccountStore(string rootDir)
    {
        _accountsDir = Path.Combine(rootDir, "accounts");
        _indexPath = Path.Combine(rootDir, "accounts.json");
        _activePath = Path.Combine(rootDir, "active.txt");
        Directory.CreateDirectory(_accountsDir);
    }

    public IReadOnlyList<AccountIndexEntry> ListAccounts()
    {
        if (!File.Exists(_indexPath))
        {
            return [];
        }
        var json = File.ReadAllText(_indexPath);
        return JsonSerializer.Deserialize<List<AccountIndexEntry>>(json, JsonDefaults.CamelCase) ?? [];
    }

    public string AddAccount(string label, StoredAccount account, string? email = null, string? organizationUuid = null, bool isTeamAccount = false)
    {
        var id = Guid.NewGuid().ToString("N");
        SaveAccount(id, account);

        var entries = ListAccounts().ToList();
        entries.Add(new AccountIndexEntry(id, label, DateTimeOffset.UtcNow, email, organizationUuid, isTeamAccount));
        File.WriteAllText(_indexPath, JsonSerializer.Serialize(entries, JsonDefaults.CamelCase));

        return id;
    }

    public AccountIndexEntry? FindByOrganizationUuid(string organizationUuid) =>
        ListAccounts().FirstOrDefault(e => e.OrganizationUuid == organizationUuid);

    public void RemoveAccount(string id)
    {
        var entries = ListAccounts().ToList();
        if (entries.RemoveAll(e => e.Id == id) == 0)
        {
            return;
        }
        File.WriteAllText(_indexPath, JsonSerializer.Serialize(entries, JsonDefaults.CamelCase));
        File.Delete(AccountPath(id)); // no-op if already missing
        if (GetActiveAccountId() == id)
        {
            File.Delete(_activePath); // no-op if already missing
        }
    }

    public void RenameAccount(string id, string newLabel)
    {
        var entries = ListAccounts().ToList();
        var index = entries.FindIndex(e => e.Id == id);
        if (index < 0)
        {
            throw new InvalidOperationException($"Account '{id}' not found.");
        }
        entries[index] = entries[index] with { Label = newLabel };
        File.WriteAllText(_indexPath, JsonSerializer.Serialize(entries, JsonDefaults.CamelCase));
    }

    public void SaveAccount(string id, StoredAccount account)
    {
        var json = JsonSerializer.Serialize(account, JsonDefaults.CamelCase);
        var plainBytes = Encoding.UTF8.GetBytes(json);
        var encrypted = ProtectedData.Protect(plainBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(AccountPath(id), encrypted);
    }

    public StoredAccount? LoadAccount(string id)
    {
        var path = AccountPath(id);
        if (!File.Exists(path))
        {
            return null;
        }

        byte[] plainBytes;
        try
        {
            plainBytes = ProtectedData.Unprotect(File.ReadAllBytes(path), optionalEntropy: null, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException)
        {
            // Different Windows profile than the one that encrypted this — caller treats null as needs-reauth.
            return null;
        }

        return JsonSerializer.Deserialize<StoredAccount>(plainBytes, JsonDefaults.CamelCase);
    }

    public string? GetActiveAccountId() => File.Exists(_activePath) ? File.ReadAllText(_activePath).Trim() : null;

    public void SetActiveAccountId(string id) => File.WriteAllText(_activePath, id);

    private string AccountPath(string id) => Path.Combine(_accountsDir, $"{id}.dat");
}
