using CommandLine;
using FishBucket.ApiClient;
using System.Text.Json;

namespace FishSyncClient.Cli;

public abstract class CommandBase
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Option("server", HelpText = "Server API URL. Priority: --server > config Host > FISH_SERVER. No built-in default.")]
    public string? Server { get; set; }

    [Option("config", Default = "config/config.json", HelpText = "JSON settings path, relative to the current working directory. Missing file is allowed.")]
    public string ConfigPath { get; set; } = "config/config.json";

    public int Run()
    {
        try
        {
            return RunAsync()
                .GetAwaiter()
                .GetResult();
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"서버 요청에 실패했습니다. HTTP 상태: {ex.StatusCode}");
            return 1;
        }
        catch (FishApiException)
        {
            Console.Error.WriteLine("서버 요청이 거부되었습니다. 로그인 정보와 서버 주소를 확인해 주세요.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    protected abstract ValueTask<int> RunAsync();

    protected async Task<string> GetServer()
    {
        return ResolveServer(await LoadConfig());
    }

    protected string ResolveServer(CliConfig? config)
    {
        var server = FirstNotEmpty(Server, config?.Host, Environment.GetEnvironmentVariable("FISH_SERVER"))
            ?? throw new ArgumentException("--server, config의 Host 또는 FISH_SERVER로 서버 주소를 지정해 주세요.");
        return CredentialStore.NormalizeServer(server);
    }

    protected async Task<CliConfig?> LoadConfig()
    {
        if (string.IsNullOrEmpty(ConfigPath) || !File.Exists(ConfigPath))
            return null;

        await using var fs = File.OpenRead(ConfigPath);
        return await JsonSerializer.DeserializeAsync<CliConfig>(fs, _jsonOptions);
    }

    protected static string? FirstNotEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

public record CommandSettings(string Root, string Host, string BucketId, string? Token);

public class CliConfig
{
    public string? Root { get; set; }
    public string? Host { get; set; }
    public string? BucketId { get; set; }
    public string? Token { get; set; }
}
