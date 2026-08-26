using System.IO;
using System.Text;
using System.Text.Json;

namespace ClaudeAccountSwitcher;

public static class CredentialsMerger
{
    /// <summary>
    /// Returns credentialsJson with its top-level "claudeAiOauth" key replaced
    /// by claudeAiOauthJson. Every other top-level key is copied through
    /// unchanged (e.g. mcpOAuth) — this must never do a full-file overwrite.
    /// </summary>
    public static string MergeClaudeAiOauth(string credentialsJson, string claudeAiOauthJson)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(credentialsJson) ? "{}" : credentialsJson);
        using var oauthDoc = JsonDocument.Parse(claudeAiOauthJson);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.NameEquals("claudeAiOauth"))
                {
                    continue;
                }
                prop.WriteTo(writer);
            }
            writer.WritePropertyName("claudeAiOauth");
            oauthDoc.RootElement.WriteTo(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
