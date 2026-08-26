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

    // ponytail: previously blanked .credentials.json when the removed account
    // was the switcher's active one — but that file is what the CLI is
    // actually using right now, independent of whether this app still
    // tracks that account in its own list. Blanking it force-logged-out a
    // perfectly live session for no benefit (no revoke-endpoint call either,
    // so it wasn't even really "signing out" server-side) — a user who
    // removed every account from the switcher lost their working `claude`
    // session over it. Sign out only ever touches this app's own bookkeeping
    // now; the real file is left alone.
    public void SignOut(string accountId) => accountStore.RemoveAccount(accountId);

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
