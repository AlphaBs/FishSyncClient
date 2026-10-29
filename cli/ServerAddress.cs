namespace FishSyncClient.Cli;

public static class ServerAddress
{
    public static string Normalize(string server)
    {
        if (!Uri.TryCreate(server.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http")
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("올바른 서버 API 주소를 입력해 주세요.");
        }

        return uri.AbsoluteUri.TrimEnd('/');
    }
}
