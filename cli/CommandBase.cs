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

    [Option("root", HelpText = "Local sync root. Defaults to config root or current directory.")]
    public string? Root { get; set; }

    [Option("server", HelpText = "Fish server URL. Defaults to config host or FISH_SERVER.")]
    public string? Server { get; set; }

    [Option("token", HelpText = "API token. Defaults to config token or FISH_TOKEN.")]
    public string? Token { get; set; }

    [Option("config", Default = "config/config.json", HelpText = "GUI-compatible config file path.")]
    public string ConfigPath { get; set; } = "config/config.json";

    public int Run()
    {
        return RunAsync().GetAwaiter().GetResult();
    }

    protected abstract ValueTask<int> RunAsync();

    protected async Task<CommandSettings> GetSettings(string? bucketId)
    {
        var config = await LoadConfig();
        return new CommandSettings(
            Root: FirstNotEmpty(Root, config?.Root, Environment.CurrentDirectory) ?? Environment.CurrentDirectory,
            Host: FirstNotEmpty(Server, config?.Host, Environment.GetEnvironmentVariable("FISH_SERVER"))
                ?? throw new ArgumentException("host"),
            BucketId: FirstNotEmpty(bucketId, config?.BucketId)
                ?? throw new ArgumentException("bucket id"),
            Token: FirstNotEmpty(Token, config?.Token, Environment.GetEnvironmentVariable("FISH_TOKEN")));
    }

    protected FishApiClient CreateApiClient(CommandSettings settings, HttpClient httpClient)
    {
        var apiClient = new FishApiClient(settings.Host, httpClient);
        if (!string.IsNullOrEmpty(settings.Token))
            apiClient.ApiKey = settings.Token;
        return apiClient;
    }

    private async Task<CliConfig?> LoadConfig()
    {
        if (string.IsNullOrEmpty(ConfigPath) || !File.Exists(ConfigPath))
            return null;

        await using var fs = File.OpenRead(ConfigPath);
        return await JsonSerializer.DeserializeAsync<CliConfig>(fs, _jsonOptions);
    }

    private static string? FirstNotEmpty(params string?[] values) =>
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
