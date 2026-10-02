using FishSyncClient.Gui;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FishSyncClient.Gui.Tests;

public class UpdateTests
{
    [Theory]
    [InlineData("20260925", "20260924", true)]
    [InlineData("20260924", "20260924", false)]
    [InlineData("20250101", "20260924", false)]
    [InlineData("1.10.0", "1.9.0", true)]
    [InlineData("v1.2", "1.2.0", false)]
    [InlineData("invalid", "20260924", false)]
    public void ComparesReleaseVersions(string latest, string current, bool expected) =>
        Assert.Equal(expected, UpdateService.IsNewer(latest, current));

    [Theory]
    [InlineData("windows", "x64")]
    [InlineData("darwin", "aarch64")]
    [InlineData("linux", "x64")]
    public async Task SelectsZipForPlatform(string os, string arch)
    {
        using var handler = new Handler(request =>
        {
            Assert.Equal($"/v1/apps/FishSyncClientGui/{os}/{arch}/latest", request.RequestUri!.AbsolutePath);
            return Manifest("/v1/apps/FishSyncClientGui/windows/x64/20260925/gui.zip");
        });
        using var http = new HttpClient(handler);
        var update = await new UpdateService(http).Check(os, arch, "20260924", default);
        Assert.Equal("20260925", update!.Version);
        Assert.Equal("https", update.Download.Scheme);
    }

    [Fact]
    public async Task MissingReleaseDoesNotOfferUpdate()
    {
        using var handler = new Handler(_ => new(HttpStatusCode.NotFound));
        using var http = new HttpClient(handler);
        Assert.Null(await new UpdateService(http).Check("windows", "x64", "20260924", default));
    }

    [Theory]
    [InlineData("file:///C:/bad.exe")]
    [InlineData("https://elsewhere.test/gui.zip")]
    [InlineData("http://update.snowfrost.kr/gui.zip")]
    public async Task RejectsUnexpectedDownloadLocations(string url)
    {
        using var handler = new Handler(_ => Manifest(url));
        using var http = new HttpClient(handler);
        Assert.Null(await new UpdateService(http).Check("windows", "x64", "20260924", default));
    }

    [Fact]
    public async Task MissingConfigUsesBundledWarningPatterns()
    {
        using var directory = new TestDirectory();
        var config = await new ConfigManager(Path.Combine(directory.Path, "config.json")).LoadConfig();
        Assert.Contains(".fabric/**", config.WarningPatterns);
        Assert.Contains("launcher_msa_credentials.bin", config.WarningPatterns);
        Assert.True(string.IsNullOrEmpty(config.Root));
        Assert.True(string.IsNullOrEmpty(config.Token));
        Assert.Equal("https://fish2.snowfrost.kr/api", config.Host);
    }

    private static HttpResponseMessage Manifest(string url) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new { schemaVersion = 1, version = "20260925",
            files = new { zip = new { url, filename = "gui.zip", md5 = "", size = 10 } } })
    };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond(request));
    }
}
