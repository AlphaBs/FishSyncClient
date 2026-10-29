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
            throw new ArgumentException("Invalid server API URL. Use an absolute http(s) URL without credentials, query, or fragment, "
                + $"for example {CliConfig.DefaultServer}");
        }

        return uri.AbsoluteUri.TrimEnd('/');
    }
}
