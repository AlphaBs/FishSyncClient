using FishSyncClient.Cli;
using System.Text.Json;
using Xunit;

namespace FishSyncClientTest;

public sealed class CliConfigurationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "fish-settings-" + Guid.NewGuid());

    public CliConfigurationTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task MissingConfigUsesBuiltInServerAndWarningPatterns()
    {
        var command = Probe();
        var settings = await command.Settings("bucket");
        Assert.Equal("https://fish2.snowfrost.kr/api", settings.Host);
        Assert.Contains("logs/**", settings.WarningPatterns);
        Assert.Contains(".env", settings.WarningPatterns);
    }

    [Fact]
    public async Task OptionsOverrideEnvironmentThenDotEnvThenConfig()
    {
        var envPath = Path.Combine(_directory, ".env");
        await File.WriteAllTextAsync(envPath, """
            FISH_SERVER=https://dotenv.test/api/
            FISH_ROOT=/dotenv-root
            FISH_BUCKET_ID=dotenv-bucket
            FISH_PASSWORD='secret # with spaces'
            """);
        var configPath = Path.Combine(_directory, "config.json");
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(new
        {
            Host = "https://config.test/api", Root = "/config-root", BucketId = "config-bucket"
        }));
        var environment = new Dictionary<string, string>();
        var command = Probe();
        command.ConfigPath = configPath;
        command.EnvironmentSettings = new CliEnvironment(envPath, name => environment.GetValueOrDefault(name));

        var dotenv = await command.Settings(null);
        Assert.Equal("https://dotenv.test/api", dotenv.Host);
        Assert.Equal("/dotenv-root", dotenv.Root);
        Assert.Equal("dotenv-bucket", dotenv.BucketId);
        Assert.Equal("secret # with spaces", command.EnvironmentSettings.Get("FISH_PASSWORD"));

        environment["FISH_SERVER"] = "https://environment.test/api";
        Assert.Equal("https://environment.test/api", (await command.Settings(null)).Host);

        command.Server = "https://option.test/api";
        command.Root = "/option-root";
        var options = await command.Settings("option-bucket");
        Assert.Equal("https://option.test/api", options.Host);
        Assert.Equal("/option-root", options.Root);
        Assert.Equal("option-bucket", options.BucketId);
    }

    [Fact]
    public async Task DotEnvSupportsExportQuotesCommentsAndDoesNotModifyProcessEnvironment()
    {
        var name = "FISH_TEST_" + Guid.NewGuid().ToString("N");
        var path = Path.Combine(_directory, ".env");
        await File.WriteAllTextAsync(path, $"export {name}=\"hello world\" # comment\n");
        var environment = new CliEnvironment(path, _ => null);
        Assert.Equal("hello world", environment.Get(name));
        Assert.Null(Environment.GetEnvironmentVariable(name));
    }

    [Fact]
    public async Task InvalidDotEnvDoesNotExposeSecretInError()
    {
        var path = Path.Combine(_directory, ".env");
        await File.WriteAllTextAsync(path, "FISH_PASSWORD=\"unterminated-super-secret");
        var environment = new CliEnvironment(path, _ => null);
        var error = Assert.Throws<FormatException>(() => environment.Get("FISH_PASSWORD"));
        Assert.DoesNotContain("super-secret", error.ToString());
    }

    [Fact]
    public async Task PartialConfigKeepsItsWarningPatternsAndFallsBackToDefaultServer()
    {
        var path = Path.Combine(_directory, "config.json");
        await File.WriteAllTextAsync(path, """{"WarningPatterns":["custom/**"]}""");
        var command = Probe();
        command.ConfigPath = path;
        var settings = await command.Settings("bucket");
        Assert.Equal(CliConfig.DefaultServer, settings.Host);
        Assert.Equal(new[] { "custom/**" }, settings.WarningPatterns);
    }

    private ProbeCommand Probe() => new()
    {
        ConfigPath = Path.Combine(_directory, "missing.json"),
        EnvironmentSettings = new CliEnvironment(Path.Combine(_directory, "missing.env"), _ => null)
    };

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private sealed class ProbeCommand : SyncCommandBase
    {
        public Task<CommandSettings> Settings(string? bucket) => GetSettings(bucket);
        protected override ValueTask<int> RunAsync() => ValueTask.FromResult(0);
    }
}
