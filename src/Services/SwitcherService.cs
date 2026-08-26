using System.IO;
using System.Text.Json;

namespace ClaudeAccountSwitcher;

public sealed class SwitcherService(AccountStore accountStore, string credentialsPath)
{
    public void SwitchTo(string targetAccountId)
    {
        var currentJson = File.Exists(credentialsPath) ? File.ReadAllText(credentialsPath) : "{}";

        // Save whatever is currently in .credentials.json back into the account
        // we believe is active — Claude Code may have refreshed it while
        // running. Must happen BEFORE loading the target below: if
        // targetAccountId is the account that's already active (re-clicking
        // its own row), the load needs to see this fresh capture — otherwise
        // it reads a stale pre-switch snapshot and overwrites the live file
        // with old tokens, which can be dead if the server already rotated
        // them (breaks the CLI's auth outright).
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

    // ponytail: confirmed with user — signing out of the active account just
    // blanks .credentials.json (Claude Code CLI is left without an active
    // login until the next switch/login), no revoke-endpoint call.
    public void SignOut(string accountId)
    {
        var wasActive = accountStore.GetActiveAccountId() == accountId;
        accountStore.RemoveAccount(accountId);
        if (wasActive)
        {
            WriteAtomic(credentialsPath, "{}");
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
