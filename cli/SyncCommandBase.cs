using FishBucket.ApiClient;
using System.Net.Http.Headers;

namespace FishSyncClient.Cli;

public abstract class SyncCommandBase : CommandBase
{
    public string? BucketId { get; set; }

    public string? Root { get; set; }

    public bool AssumeYes { get; set; }

    protected bool ConfirmWarnings(IEnumerable<string> paths, CommandSettings settings) =>
        SyncWarnings.Confirm(paths, settings.WarningPatterns, AssumeYes, Input, Output);

    protected async Task<CommandSettings> GetSettings(string? bucketId)
    {
        var config = await LoadConfig();
        var host = ResolveServer(config);
        return new CommandSettings(
            Root: FirstNotEmpty(
                Root,
                EnvironmentSettings.Get("FISH_ROOT"),
                config.Root,
                Environment.CurrentDirectory) ?? Environment.CurrentDirectory,
            Host: host,
            BucketId: FirstNotEmpty(bucketId, EnvironmentSettings.Get("FISH_BUCKET_ID"), config.BucketId)
                ?? throw new ArgumentException(
                    "Bucket ID is required. Pass it as an argument, or set FISH_BUCKET_ID or config BucketId."))
        {
            WarningPatterns = config.WarningPatterns ?? []
        };
    }

    protected static void EnsureAbsoluteRoot(CommandSettings settings)
    {
        // 상대 경로는 실행 위치에 따라 다른 폴더를 동기화할 수 있으므로 작업 전에 거부한다.
        if (!Path.IsPathFullyQualified(settings.Root))
        {
            throw new ArgumentException(
                $"Root must be an absolute path (from --root, FISH_ROOT, or config Root): {settings.Root}");
        }
    }

    protected virtual HttpClient CreateHttpClient() => new()
    {
        Timeout = TimeSpan.FromHours(1)
    };

    protected async Task<FishApiClient> CreateApiClient(CommandSettings settings, HttpClient httpClient)
    {
        var username = EnvironmentSettings.Get("FISH_USERNAME");
        var password = EnvironmentSettings.Get("FISH_PASSWORD");
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(username))
        {
            missing.Add("FISH_USERNAME");
        }
        if (string.IsNullOrWhiteSpace(password))
        {
            missing.Add("FISH_PASSWORD");
        }
        if (missing.Count > 0)
        {
            throw new ArgumentException(
                $"Missing credentials: {string.Join(", ", missing)}. "
                + "Set them as environment variables or in ./.env (current directory).");
        }

        var apiClient = new FishApiClient(settings.Host, httpClient);
        var token = await apiClient.Login(username!.Trim(), password!);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("The server returned an empty authentication token.");
        }

        // 토큰은 현재 명령에서만 사용하며 파일이나 키체인에 저장하지 않는다.
        // SDK는 목록 조회에 기본 헤더를, 동기화에 ApiKey를 사용한다.
        apiClient.ApiKey = token;
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return apiClient;
    }
}
