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
                $"HTTP {(int)response.StatusCode}: 인증 정보를 확인해 주세요. "
                + "FISH_USERNAME, FISH_PASSWORD와 버킷 쓰기 권한을 확인한 뒤 push를 다시 시도해 주세요.");
        }

        return response;
    }
}
