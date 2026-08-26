using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ClaudeAccountSwitcher;

public sealed class OAuthClient(ITokenEndpointClient tokenEndpoint)
{
    private const string AuthorizeUrl = "https://claude.ai/oauth/authorize";
    private const string ClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";
    // code=true and this exact scope set are both required — a narrower scope
    // or missing code=true gets "Invalid request format" from the real server.
    private const string Scope = "org:create_api_key user:profile user:inference user:sessions:claude_code user:mcp_servers user:file_upload";
    private static readonly TimeSpan LoginTimeout = TimeSpan.FromMinutes(5);

    public static string BuildAuthorizeUri(string codeChallenge, string state, string redirectUri) =>
        $"{AuthorizeUrl}?code=true&client_id={ClientId}&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
        $"&scope={Uri.EscapeDataString(Scope)}&code_challenge={codeChallenge}&code_challenge_method=S256&state={state}";

    public async Task<StoredAccount> RunFlowAsync(CancellationToken ct)
    {
        var codeVerifier = Pkce.GenerateCodeVerifier();
        var codeChallenge = Pkce.ComputeCodeChallenge(codeVerifier);
        var state = Pkce.GenerateState();

        // TcpListener, not HttpListener — HttpListener's native HTTP.sys queue is
        // shared process-wide and throws on a second login attempt in the same run.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var redirectUri = $"http://localhost:{port}/callback";

        Process.Start(new ProcessStartInfo(BuildAuthorizeUri(codeChallenge, state, redirectUri)) { UseShellExecute = true });

        using var timeoutCts = new CancellationTokenSource(LoginTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        TcpClient client;
        try
        {
            client = await listener.AcceptTcpClientAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("Timed out waiting for the browser login to complete.");
        }
        finally
        {
            listener.Stop();
        }

        using (client)
        {
            var (code, returnedState) = await ReadCallbackRequestAsync(client, ct);
            await WriteSuccessResponseAsync(client, ct);

            if (returnedState != state || string.IsNullOrEmpty(code))
            {
                throw new InvalidOperationException("Authorization server returned an invalid response.");
            }

            return await tokenEndpoint.ExchangeCodeAsync(code, state, codeVerifier, redirectUri, ct);
        }
    }

    private static async Task<(string? Code, string? State)> ReadCallbackRequestAsync(TcpClient client, CancellationToken ct)
    {
        var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, leaveOpen: true);

        // "GET /callback?code=...&state=... HTTP/1.1"
        var requestLine = await reader.ReadLineAsync(ct) ?? "";

        // Drain remaining headers — unread data before writing the response can confuse some browsers.
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(ct)))
        {
        }

        var pathAndQuery = requestLine.Split(' ') is [_, var target, ..] ? target : "";
        var queryStart = pathAndQuery.IndexOf('?');
        var query = queryStart >= 0 ? pathAndQuery[(queryStart + 1)..] : "";
        var parsed = ParseQueryString(query);

        return (parsed.GetValueOrDefault("code"), parsed.GetValueOrDefault("state"));
    }

    public static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>();
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }
            var key = Uri.UnescapeDataString(pair[..separatorIndex]);
            var value = Uri.UnescapeDataString(pair[(separatorIndex + 1)..]);
            result[key] = value;
        }
        return result;
    }

    private static async Task WriteSuccessResponseAsync(TcpClient client, CancellationToken ct)
    {
        const string body = "<html><body>Authorization complete — you can close this tab.</body></html>";
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var header =
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: text/html; charset=utf-8\r\n" +
            $"Content-Length: {bodyBytes.Length}\r\n" +
            "Connection: close\r\n\r\n";

        var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(header), ct);
        await stream.WriteAsync(bodyBytes, ct);
        await stream.FlushAsync(ct);
    }
}
