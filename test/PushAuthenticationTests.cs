using FishSyncClient.Cli;
using System.Net;
using Xunit;

namespace FishSyncClientTest;

public sealed class PushAuthenticationTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task AuthenticationFailurePrintsReloginInstructionsOnce(HttpStatusCode status)
    {
        using var output = new StringWriter();
        using var handler = new PushAuthenticationHandler(new ResponseHandler(status), output);
        using var http = new HttpClient(handler);

        using var first = await http.PostAsync("https://example.com/api/sync", null);
        using var second = await http.PostAsync("https://example.com/api/sync", null);

        Assert.Equal(status, first.StatusCode);
        Assert.Equal(status, second.StatusCode);
        var notice = output.ToString();
        Assert.Contains($"HTTP {(int)status}", notice);
        Assert.Contains("재로그인이 필요합니다", notice);
        Assert.Contains("login <username> --server", notice);
        Assert.Contains("push", notice);
        Assert.Single(notice.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task OtherResponsesDoNotRequestRelogin(HttpStatusCode status)
    {
        using var output = new StringWriter();
        using var handler = new PushAuthenticationHandler(new ResponseHandler(status), output);
        using var http = new HttpClient(handler);

        using var response = await http.PostAsync("https://example.com/api/sync", null);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("", output.ToString());
    }

    private sealed class ResponseHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
