using CommandLine;
using FishBucket.ApiClient;
using System.Net.Http.Headers;

namespace FishSyncClient.Cli;

public abstract class SyncCommandBase : CommandBase
{
    [Option("root", HelpText = "Absolute local directory. Priority: --root > config Root > current working directory.")]
    public string? Root { get; set; }

    [Option("token", HelpText = "Token override. Priority: --token > FISH_TOKEN > saved login for this server > config Token.")]
    public string? Token { get; set; }

    protected async Task<CommandSettings> GetSettings(string? bucketId)
    {
        var config = await LoadConfig();
        var host = ResolveServer(config);
        var token = FirstNotEmpty(Token, Environment.GetEnvironmentVariable("FISH_TOKEN"));
        if (token is null)
        {
            token = FirstNotEmpty(await new CredentialStore().Load(host), config?.Token);
        }

        return new CommandSettings(
            Root: FirstNotEmpty(Root, config?.Root, Environment.CurrentDirectory) ?? Environment.CurrentDirectory,
            Host: host,
            BucketId: FirstNotEmpty(bucketId, config?.BucketId)
                ?? throw new ArgumentException("bucket id"),
            Token: token);
    }

    protected FishApiClient CreateApiClient(CommandSettings settings, HttpClient httpClient)
    {
        var apiClient = new FishApiClient(settings.Host, httpClient);
        if (!string.IsNullOrEmpty(settings.Token))
        {
            // SDK는 목록 조회에 기본 헤더를, 동기화에 ApiKey를 사용한다.
            apiClient.ApiKey = settings.Token;
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token);
        }

        return apiClient;
    }
}
