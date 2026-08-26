using System.Text.Json;

namespace ClaudeAccountSwitcher;

public sealed record UpdateInfo(string TagName, string InstallerUrl);

// GitHub's /releases/latest already excludes pre-releases.
public static class ReleaseParser
{
    public static UpdateInfo? Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("tag_name", out var tagNameProp) || !root.TryGetProperty("assets", out var assets))
        {
            return null;
        }

        var tagName = tagNameProp.GetString();
        if (tagName is null)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (name is not null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && asset.TryGetProperty("browser_download_url", out var url) && url.GetString() is { } downloadUrl)
            {
                return new UpdateInfo(tagName, downloadUrl);
            }
        }

        return null;
    }
}
