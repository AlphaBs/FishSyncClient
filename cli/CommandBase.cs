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

    // 연결 오류 메시지에 실제로 선택된 서버 주소를 보여주기 위해 보관한다.
    protected string? ResolvedServer { get; private set; }

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
        catch (HttpRequestException ex) when (ex.StatusCode is { } status)
        {
            ErrorOutput.WriteLine($"Server request failed: HTTP {(int)status} {status} ({ResolvedServer}).");
            return 1;
        }
        catch (HttpRequestException ex)
        {
            ErrorOutput.WriteLine($"Could not reach the server {ResolvedServer}: {ex.Message}");
            return 1;
        }
        catch (FishApiException)
        {
            ErrorOutput.WriteLine(
                $"The server rejected the request ({ResolvedServer}). Check FISH_USERNAME, FISH_PASSWORD, and the server URL.");
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
        ResolvedServer = ServerAddress.Normalize(server);
        return ResolvedServer;
    }

    protected async Task<CliConfig> LoadConfig()
    {
        Stream stream;
        string source;
        if (!string.IsNullOrEmpty(ConfigPath) && File.Exists(ConfigPath))
        {
            stream = File.OpenRead(ConfigPath);
            source = ConfigPath;
        }
        else
        {
            stream = typeof(CliConfig).Assembly.GetManifestResourceStream("FishSyncClient.Cli.Defaults.config.json")
                ?? throw new InvalidOperationException("Built-in default config is missing.");
            source = "built-in default config";
        }
        await using (stream)
        {
            try
            {
                return await JsonSerializer.DeserializeAsync(stream, CliJsonContext.Default.CliConfig)
                    ?? throw new FormatException($"Config file must contain a JSON object: {source}");
            }
            catch (JsonException ex)
            {
                throw new FormatException($"Could not parse config file {source}: {ex.Message}");
            }
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
