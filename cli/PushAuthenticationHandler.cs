using System.Net;

namespace FishSyncClient.Cli;

public sealed class PushAuthenticationHandler(
    HttpMessageHandler innerHandler,
    TextWriter errorOutput) : DelegatingHandler(innerHandler)
{
    private int _noticeWritten;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            && Interlocked.Exchange(ref _noticeWritten, 1) == 0)
        {
            errorOutput.WriteLine(
                $"HTTP {(int)response.StatusCode}: authentication failed. "
                + "Check FISH_USERNAME, FISH_PASSWORD, and write permission for the bucket, then run push again.");
        }

        return response;
    }
}
