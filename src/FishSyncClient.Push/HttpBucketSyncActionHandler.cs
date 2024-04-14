using FishSyncClient.Common;
using System.Net.Http.Headers;

namespace FishSyncClient.Push;

public class HttpBucketSyncActionHandler : IBucketSyncActionHandler
{
    private readonly HttpClient _httpClient;

    public HttpBucketSyncActionHandler(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public bool CanHandle(BucketSyncAction action)
    {
        return action.Action.Type == "Http" && action.Action.Parameters != null;
    }

    public async ValueTask Handle(
        Stream content, 
        BucketSyncAction action, 
        IProgress<ByteProgress>? progress, 
        CancellationToken cancellationToken)
    {
        if (action.Action.Type != "Http")
            throw new InvalidOperationException();
        if (action.Action.Parameters == null)
            throw new InvalidOperationException();

        var reqContent = new StreamContent(content); // do not dispose this! it will dispose the inner stream (`content`)
        reqContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        var reqMessage = new HttpRequestMessage();
        foreach (var kv in action.Action.Parameters)
        {

            if (kv.Key == "method")
                reqMessage.Method = new HttpMethod(kv.Value);
            else if (kv.Key == "url")
                reqMessage.RequestUri = new Uri(kv.Value);
            else if (getHeader(kv.Key, out var headerName))
            {
                if (!setContentHeader(reqContent, headerName, kv.Value)) // wtf
                    reqMessage.Headers.Add(headerName, kv.Value);
            }
        }

        reqMessage.Content = reqContent;

        var res = await _httpClient.SendAsync(reqMessage);
        var resStream = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            Console.WriteLine(resStream);
    }

    private bool getHeader(string key, out string headerName)
    {
        const string headersPrefix = "headers.";
        if (key.StartsWith(headersPrefix) && key.Length > headersPrefix.Length)
        {
            headerName = key.Substring(headersPrefix.Length);
            return true;
        }
        else
        {
            headerName = null!;
            return false;
        }
    }

    private bool setContentHeader(HttpContent content, string headerName, string headerValue)
    {
        headerName = headerName.ToLowerInvariant();

        if (headerName == "content-md5")
            content.Headers.ContentMD5 = Convert.FromBase64String(headerValue);
        else if (headerName == "content-type")
            content.Headers.ContentType = new MediaTypeHeaderValue(headerValue);
        else
            return false;

        return true;
    }
}