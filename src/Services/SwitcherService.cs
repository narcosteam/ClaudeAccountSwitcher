using System.IO;
using System.Text.Json;

namespace ClaudeAccountSwitcher;

public sealed class SwitcherService(AccountStore accountStore, string credentialsPath)
{
    public void SwitchTo(string targetAccountId)
    {
        var currentJson = File.Exists(credentialsPath) ? File.ReadAllText(credentialsPath) : "{}";

        // Capture the live file into the active account BEFORE loading the
        // target: switching to the already-active account must see this
        // fresh capture, not a stale pre-switch snapshot.
        var activeId = accountStore.GetActiveAccountId();
        if (activeId is not null)
        {
            var currentOauthJson = ExtractClaudeAiOauth(currentJson);
            if (currentOauthJson is not null)
            {
                var currentAccount = JsonSerializer.Deserialize<StoredAccount>(currentOauthJson, JsonDefaults.CamelCase);
                if (currentAccount is not null)
                {
                    accountStore.SaveAccount(activeId, currentAccount);
                }
            }
        }

        var target = accountStore.LoadAccount(targetAccountId)
            ?? throw new InvalidOperationException($"Account '{targetAccountId}' could not be decrypted; needs re-authorization.");

        var targetOauthJson = JsonSerializer.Serialize(target, JsonDefaults.CamelCase);
        var merged = CredentialsMerger.MergeClaudeAiOauth(currentJson, targetOauthJson);

        WriteAtomic(credentialsPath, merged);
        accountStore.SetActiveAccountId(targetAccountId);
    }

    // Never blanks .credentials.json — only switches to another account (if
    // one remains) when the removed account was active; no revoke call.
    public void SignOut(string accountId)
    {
        var wasActive = accountStore.GetActiveAccountId() == accountId;
        accountStore.RemoveAccount(accountId);
        if (!wasActive)
        {
            return;
        }

        var remaining = accountStore.ListAccounts();
        if (remaining.Count > 0)
        {
            SwitchTo(remaining[0].Id);
        }
    }

    private static string? ExtractClaudeAiOauth(string credentialsJson)
    {
        using var doc = JsonDocument.Parse(credentialsJson);
        return doc.RootElement.TryGetProperty("claudeAiOauth", out var el) ? el.GetRawText() : null;
    }

    private static void WriteAtomic(string path, string content)
    {
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, content);
        File.Move(tempPath, path, overwrite: true);
    }
}
