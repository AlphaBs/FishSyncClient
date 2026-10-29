using FishBucket.ApiClient;
using System.Text.Json;

namespace FishSyncClient.Cli;

public abstract class CommandBase
{
    public CliEnvironment EnvironmentSettings { get; set; } = new();
    protected TextReader Input { get; private set; } = Console.In;
    protected TextWriter Output { get; private set; } = Console.Out;
    protected TextWriter ErrorOutput { get; private set; } = Console.Error;

    public string? Server { get; set; }

    public string ConfigPath { get; set; } = "config/config.json";

    public int Run(TextReader? input = null, TextWriter? output = null, TextWriter? errorOutput = null)
    {
        Input = input ?? Console.In;
        Output = output ?? Console.Out;
        ErrorOutput = errorOutput ?? Console.Error;
        try
        {
            return RunAsync()
                .GetAwaiter()
                .GetResult();
        }
        catch (HttpRequestException ex)
        {
            ErrorOutput.WriteLine($"서버 요청에 실패했습니다. HTTP 상태: {ex.StatusCode}");
            return 1;
        }
        catch (FishApiException)
        {
            ErrorOutput.WriteLine("서버 요청이 거부되었습니다. 로그인 정보와 서버 주소를 확인해 주세요.");
            return 1;
        }
        catch (Exception ex)
        {
            ErrorOutput.WriteLine(ex.Message);
            return 1;
        }
    }

    protected abstract ValueTask<int> RunAsync();

    protected string ResolveServer(CliConfig? config)
    {
        var server = FirstNotEmpty(
            Server,
            EnvironmentSettings.Get("FISH_SERVER"),
            config?.Host,
            CliConfig.DefaultServer)!;
        return ServerAddress.Normalize(server);
    }

    protected async Task<CliConfig> LoadConfig()
    {
        Stream stream;
        if (!string.IsNullOrEmpty(ConfigPath) && File.Exists(ConfigPath))
        {
            stream = File.OpenRead(ConfigPath);
        }
        else
        {
            stream = typeof(CliConfig).Assembly.GetManifestResourceStream("FishSyncClient.Cli.Defaults.config.json")
                ?? throw new InvalidOperationException("내장 기본 설정을 찾을 수 없습니다.");
        }
        await using (stream)
        {
            return await JsonSerializer.DeserializeAsync(stream, CliJsonContext.Default.CliConfig)
                ?? throw new FormatException("설정 파일은 JSON 객체여야 합니다.");
        }
    }

    protected static string? FirstNotEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

public record CommandSettings(string Root, string Host, string BucketId)
{
    public IReadOnlyList<string> WarningPatterns { get; init; } = [];
}

public class CliConfig
{
    public const string DefaultServer = "https://fish2.snowfrost.kr/api";
    public List<string> WarningPatterns { get; set; } = [];
    public string? Root { get; set; }
    public string? Host { get; set; }
    public string? BucketId { get; set; }
}
