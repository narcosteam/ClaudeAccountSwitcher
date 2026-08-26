using System.Security.Cryptography;
using System.Text;

namespace ClaudeAccountSwitcher;

public static class Pkce
{
    public static string GenerateCodeVerifier() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static string ComputeCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    // ponytail: 32 bytes (43-char base64url), not 16 — a state shorter than
    // what the real server expects gets rejected as "Invalid request format"
    // before the user even sees the consent screen's Authorize action take
    // effect. Confirmed against a live-captured working state value's length
    // and an independent OAuth client bug report requiring exactly this size.
    public static string GenerateState() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
