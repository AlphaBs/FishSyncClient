using System.Net.Http;

namespace FishSyncClient.Gui;

public static class HttpUtil
{
    public static readonly HttpClient HttpClient = createHttpClient();

    private static HttpClient createHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromHours(1);
        return httpClient;
    }
}
