using System.Net;
using FishSyncClient.Cli;
using Xunit;

namespace FishSyncClientTest;

public sealed class CliUpdateTests
{
    [Theory]
    [InlineData("windows", "x64")]
    [InlineData("darwin", "aarch64")]
    [InlineData("linux", "x64")]
    public async Task ReportsNewVersionAndAbsoluteZipUrl(string os, string arch)
    {
        using var fixture = new Fixture();
        await fixture.Check(os, arch);

        Assert.Equal(1, fixture.Requests);
        Assert.Equal($"https://update.snowfrost.kr/v1/apps/FishSyncClientCli/{os}/{arch}/latest", fixture.LastUrl);
        Assert.Contains("2.0.0. Please update.", fixture.Output.ToString());
        Assert.Contains("https://update.snowfrost.kr/releases/cli.zip", fixture.Output.ToString());
    }

    [Fact]
    public async Task PersistsIntervalAcrossInstancesAndChecksAgainAtOneHour()
    {
        using var fixture = new Fixture();
        await fixture.Check();
        fixture.Clock.Now += TimeSpan.FromMinutes(59);
        await fixture.Check();
        Assert.Equal(1, fixture.Requests);

        fixture.Clock.Now += TimeSpan.FromMinutes(1);
        await fixture.Check();
        Assert.Equal(2, fixture.Requests);
    }

    [Fact]
    public async Task ManualCheckImmediatelyPrintsLatestUrlEvenWhenAlreadyUpToDate()
    {
        using var fixture = new Fixture();
        fixture.Respond = () => Manifest("1.0.0");
        await fixture.Check();
        var service = new CliUpdateService(fixture.Http, fixture.DirectoryPath, fixture.Clock);
        var result = await service.ShowLatestAsync(fixture.Output, "windows", "x64");
        Assert.Equal(0, result);
        Assert.Equal(2, fixture.Requests);
        Assert.Contains("Latest version: 1.0.0", fixture.Output.ToString());
        Assert.Contains("https://update.snowfrost.kr/releases/cli.zip", fixture.Output.ToString());
    }

    [Fact]
    public void UpdateCommandInvokesManualCheckWithoutAutomaticCheck()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var manualChecks = 0;
        var automaticChecks = 0;
        var result = CliApplication.Run(
            ["update"],
            output,
            error,
            checkForUpdates: _ => automaticChecks++,
            showLatestUpdate: _ =>
            {
                manualChecks++;
                return 0;
            });
        Assert.Equal(0, result);
        Assert.Equal(1, manualChecks);
        Assert.Equal(0, automaticChecks);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task MissingOrFailedReleaseIsSilentAndThrottled(HttpStatusCode status)
    {
        using var fixture = new Fixture();
        fixture.Respond = () => new HttpResponseMessage(status);
        await fixture.Check();
        await fixture.Check();
        Assert.Equal(1, fixture.Requests);
        Assert.Equal("", fixture.Output.ToString());
    }

    [Fact]
    public async Task IdenticalVersionIsSilent()
    {
        using var fixture = new Fixture();
        fixture.Respond = () => Manifest("1.0.0");
        await fixture.Check();
        Assert.Equal("", fixture.Output.ToString());
    }

    [Theory]
    [InlineData("http://update.snowfrost.kr/cli.zip")]
    [InlineData("https://elsewhere.test/cli.zip")]
    [InlineData("https://update.snowfrost.kr:444/cli.zip")]
    [InlineData("https://user@update.snowfrost.kr/cli.zip")]
    [InlineData("file:///tmp/cli.zip")]
    [InlineData("")]
    public async Task RejectsInvalidDownloadUrls(string url)
    {
        using var fixture = new Fixture();
        fixture.Respond = () => Manifest("2.0.0", url);
        await fixture.Check();
        Assert.Equal("", fixture.Output.ToString());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"schemaVersion\":2,\"version\":\"2.0.0\"}")]
    [InlineData("{\"schemaVersion\":1,\"version\":\"2.0.0\",\"files\":{}}")]
    public async Task InvalidManifestDoesNotFailSyncOrRetryImmediately(string json)
    {
        using var fixture = new Fixture();
        fixture.Respond = () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        };
        await fixture.Check();
        await fixture.Check();
        Assert.Equal(1, fixture.Requests);
        Assert.Equal("", fixture.Output.ToString());
    }

    [Fact]
    public async Task TimeoutIsSilentAndThrottled()
    {
        using var fixture = new Fixture();
        fixture.Respond = () => throw new TaskCanceledException();
        await fixture.Check();
        await fixture.Check();
        Assert.Equal(1, fixture.Requests);
        Assert.Equal("", fixture.Output.ToString());
    }

    [Fact]
    public async Task CorruptCacheIsRecoveredAndFutureTimestampDoesNotBlockChecks()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.StatePath, "broken");
        await fixture.Check();
        Assert.Equal(1, fixture.Requests);

        fixture.Clock.Now -= TimeSpan.FromHours(2);
        await fixture.Check();
        await fixture.Check();
        Assert.Equal(2, fixture.Requests);
    }

    [Fact]
    public async Task LockedCacheSkipsNetworkWithoutBlocking()
    {
        using var fixture = new Fixture();
        using var held = new FileStream(fixture.StatePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        await fixture.Check();
        Assert.Equal(0, fixture.Requests);
    }

    [Fact]
    public async Task UnwritableCacheSkipsNetworkWithoutFailing()
    {
        using var fixture = new Fixture();
        var filePath = Path.Combine(fixture.DirectoryPath, "file");
        File.WriteAllText(filePath, "");
        await new CliUpdateService(fixture.Http, filePath, fixture.Clock)
            .CheckAndNotifyAsync(fixture.Output, "windows", "x64", "1.0.0");
        Assert.Equal(0, fixture.Requests);
    }

    [Theory]
    [InlineData("0.9.0")]
    [InlineData("1.0")]
    [InlineData("v1.0.0")]
    [InlineData("1.0.0+build")]
    [InlineData("release-custom")]
    public async Task AnyDifferentVersionOffersUpdate(string version)
    {
        using var fixture = new Fixture();
        fixture.Respond = () => Manifest(version);
        await fixture.Check();
        Assert.Contains($"available: {version}. Please update.", fixture.Output.ToString());
        Assert.Contains("https://update.snowfrost.kr/releases/cli.zip", fixture.Output.ToString());
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("--version")]
    [InlineData("pull", "--help")]
    [InlineData("help", "push")]
    [InlineData("push", "--unknown")]
    public void InformationalAndInvalidCommandsDoNotCheckUpdates(params string[] args)
    {
        var calls = 0;
        using var output = new StringWriter();
        using var error = new StringWriter();
        CliApplication.Run(args, output, error, checkForUpdates: _ => calls++);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void SyncCommandInvokesUpdateCheck()
    {
        var calls = 0;
        using var output = new StringWriter();
        using var error = new StringWriter();
        CliApplication.Run(
            ["pull", "bucket", "--root", "relative/path"],
            output,
            error,
            checkForUpdates: _ => calls++);
        Assert.Equal(1, calls);
    }

    private static HttpResponseMessage Manifest(string version, string url = "/releases/cli.zip") => new(HttpStatusCode.OK)
    {
        Content = System.Net.Http.Json.JsonContent.Create(new
        {
            schemaVersion = 1,
            version,
            files = new { zip = new { url } }
        })
    };

    private sealed class Fixture : IDisposable
    {
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "fish-update-" + Guid.NewGuid());
        public string StatePath => Path.Combine(DirectoryPath, "update-check-windows-x64.json");
        public Clock Clock { get; } = new();
        public StringWriter Output { get; } = new();
        public HttpClient Http { get; }
        public int Requests { get; private set; }
        public string? LastUrl { get; private set; }
        public Func<HttpResponseMessage> Respond { get; set; } = () => Manifest("2.0.0");

        public Fixture()
        {
            Directory.CreateDirectory(DirectoryPath);
            Http = new HttpClient(new Handler(request =>
            {
                Requests++;
                LastUrl = request.RequestUri!.AbsoluteUri;
                return Respond();
            }));
        }

        public Task Check(string os = "windows", string arch = "x64") =>
            new CliUpdateService(Http, DirectoryPath, Clock)
                .CheckAndNotifyAsync(Output, os, arch, "1.0.0");

        public void Dispose()
        {
            Http.Dispose();
            Output.Dispose();
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
