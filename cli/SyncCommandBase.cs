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
                ?? throw new ArgumentException("버킷 ID 또는 FISH_BUCKET_ID를 지정해 주세요."))
        {
            WarningPatterns = config.WarningPatterns ?? []
        };
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
            throw new ArgumentException($"환경변수 또는 .env에 {string.Join(", ", missing)}를 설정해 주세요.");
        }

        var apiClient = new FishApiClient(settings.Host, httpClient);
        var token = await apiClient.Login(username!.Trim(), password!);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("서버가 빈 인증 토큰을 반환했습니다.");
        }

        // 토큰은 현재 명령에서만 사용하며 파일이나 키체인에 저장하지 않는다.
        // SDK는 목록 조회에 기본 헤더를, 동기화에 ApiKey를 사용한다.
        apiClient.ApiKey = token;
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return apiClient;
    }
}
