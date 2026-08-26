using System.Net;
using System.Text;

namespace ClaudeAccountSwitcher.Tests;

internal sealed class FakeHttpMessageHandler(string jsonResponse, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json"),
        };
        return Task.FromResult(response);
    }
}
