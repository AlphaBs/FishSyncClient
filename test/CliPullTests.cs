using System.Net;
using System.Text;
using FishSyncClient.Cli;
using Xunit;

namespace FishSyncClientTest;

public sealed class CliPullTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "fish-pull-" + Guid.NewGuid());

    public CliPullTests() => Directory.CreateDirectory(_directory);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuccessfulPullDeletesLocalOnlyFilesIncludingWhenBucketIsEmpty(bool emptyBucket)
    {
        var localOnly = Path.Combine(_directory, "local-only.txt");
        File.WriteAllText(localOnly, "remove me");
        var target = Path.Combine(_directory, "keep.txt");
        if (!emptyBucket)
            File.WriteAllText(target, "stale");

        using var handler = new PullHandler { EmptyBucket = emptyBucket };
        var result = Run(handler);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.False(File.Exists(localOnly));
        Assert.Contains("Deleted files (1)", result.Output);
        if (emptyBucket)
            Assert.Empty(Directory.GetFiles(_directory));
        else
            Assert.Equal("hello", File.ReadAllText(target));
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "hello")]
    [InlineData(HttpStatusCode.OK, "wrong")]
    public void FailedDownloadOrIntegrityCheckPreservesLocalOnlyFiles(HttpStatusCode status, string content)
    {
        var localOnly = Path.Combine(_directory, "local-only.txt");
        File.WriteAllText(localOnly, "preserve me");
        using var handler = new PullHandler { DownloadStatus = status, DownloadContent = content };

        var result = Run(handler);

        Assert.Equal(1, result.ExitCode);
        Assert.NotEmpty(result.Error);
        Assert.Equal("preserve me", File.ReadAllText(localOnly));
        Assert.DoesNotContain("Deleted files (", result.Output);
    }

    private (int ExitCode, string Output, string Error) Run(PullHandler handler)
    {
        var command = new TestPullCommand(handler)
        {
            Root = _directory,
            BucketId = "bucket",
            Server = "https://api.test/api",
            AssumeYes = true,
            ConfigPath = Path.Combine(_directory, "missing.json"),
            EnvironmentSettings = new CliEnvironment(
                Path.Combine(_directory, "missing.env"),
                name => name switch
                {
                    "FISH_USERNAME" => "user",
                    "FISH_PASSWORD" => "test-password",
                    _ => null
                })
        };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = command.Run(output: output, errorOutput: error);
        return (exitCode, output.ToString(), error.ToString());
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private sealed class TestPullCommand(HttpMessageHandler handler) : PullCommand
    {
        protected override HttpClient CreateHttpClient() => new(handler, disposeHandler: false);
    }

    private sealed class PullHandler : HttpMessageHandler
    {
        public bool EmptyBucket { get; init; }
        public HttpStatusCode DownloadStatus { get; init; } = HttpStatusCode.OK;
        public string DownloadContent { get; init; } = "hello";

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/auth/login")
                return Task.FromResult(Json("""{"token":"test-token"}"""));

            if (path == "/api/buckets/common/bucket/files")
            {
                Assert.Equal("Bearer test-token", request.Headers.Authorization?.ToString());
                var files = EmptyBucket ? "[]" : """
                    [{"path":"keep.txt","location":"https://download.test/keep.txt",
                      "metadata":{"size":5,"checksum":"5d41402abc4b2a76b9719d911017c592"}}]
                    """;
                return Task.FromResult(Json("{\"id\":\"bucket\",\"files\":" + files + "}"));
            }

            Assert.Equal("/keep.txt", path);
            Assert.Null(request.Headers.Authorization);
            return Task.FromResult(new HttpResponseMessage(DownloadStatus)
            {
                Content = new StringContent(DownloadContent, Encoding.UTF8)
            });
        }

        private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(value, Encoding.UTF8, "application/json")
        };
    }
}
