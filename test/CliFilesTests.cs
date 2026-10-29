using System.Net;
using System.Text;
using FishSyncClient.Cli;
using Xunit;

namespace FishSyncClientTest;

public sealed class CliFilesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "fish-files-" + Guid.NewGuid());

    public CliFilesTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void ListsAuthenticatedRemoteFilesInPathOrderWithoutTouchingLocalFiles()
    {
        using var handler = new ListingHandler
        {
            Files = """
                [{"path":"z.txt","location":"https://download.test/z","metadata":{"size":123,"checksum":"900150983cd24fb0d6963f7d28e17f72"}},
                 {"path":"docs/a.txt","location":"https://download.test/a","metadata":{"size":5,"checksum":"5d41402abc4b2a76b9719d911017c592"}}]
                """
        };
        var command = CreateCommand(handler);
        command.Root = "relative/nonexistent/root";
        var result = Run(command);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Contains("docs/a.txt\t5\t5d41402abc4b2a76b9719d911017c592", result.Output.ToLowerInvariant());
        Assert.Contains("z.txt\t123\t900150983cd24fb0d6963f7d28e17f72", result.Output.ToLowerInvariant());
        Assert.True(result.Output.IndexOf("docs/a.txt", StringComparison.Ordinal)
            < result.Output.IndexOf("z.txt", StringComparison.Ordinal));
        Assert.Contains("Files: 2", result.Output);
        Assert.Equal(new[] { "/api/auth/login", "/api/buckets/common/my-bucket/files" }, handler.Paths);
        Assert.Empty(Directory.GetFileSystemEntries(_directory));
        Assert.DoesNotContain("memory-token", result.Output + result.Error);
        Assert.DoesNotContain("secret-password", result.Output + result.Error);
    }

    [Fact]
    public void EmptyBucketSucceeds()
    {
        using var handler = new ListingHandler();
        var result = Run(CreateCommand(handler));
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("No files found." + Environment.NewLine, result.Output);
        Assert.Empty(result.Error);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public void BucketRequestFailureReturnsNonzeroWithoutPrintingAList(HttpStatusCode status)
    {
        using var handler = new ListingHandler { Status = status };
        var result = Run(CreateCommand(handler));
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.NotEmpty(result.Error);
    }

    [Fact]
    public void MissingCredentialsDoNotRequestFiles()
    {
        using var handler = new ListingHandler();
        var command = CreateCommand(handler);
        command.EnvironmentSettings = new CliEnvironment(Path.Combine(_directory, "missing.env"), _ => null);
        var result = Run(command);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("FISH_USERNAME", result.Error);
        Assert.Empty(handler.Paths);
    }

    [Fact]
    public void BucketCanComeFromEnvironment()
    {
        using var handler = new ListingHandler();
        var command = CreateCommand(handler);
        command.BucketId = null;
        var result = Run(command);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("/api/buckets/common/environment-bucket/files", handler.Paths.Last());
    }

    [Theory]
    [InlineData("files", "--help")]
    [InlineData("help", "files")]
    public void FilesHelpExplainsListingAndOnlyShowsRelevantOptions(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var result = CliApplication.Run(args, output, error);
        Assert.Equal(0, result);
        Assert.Empty(error.ToString());
        Assert.Contains("cli files my-bucket", output.ToString());
        Assert.Contains("--server", output.ToString());
        Assert.Contains("--config", output.ToString());
        Assert.DoesNotContain("--root", output.ToString());
        Assert.DoesNotContain("--yes", output.ToString());
    }

    [Theory]
    [InlineData("files", "--root", "/tmp/files")]
    [InlineData("files", "-y")]
    [InlineData("files", "--unknown")]
    [InlineData("files", "bucket", "extra")]
    public void InvalidFilesOptionsFailBeforeMakingRequests(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var checks = 0;
        var result = CliApplication.Run(args, output, error, checkForUpdates: _ => checks++);
        Assert.Equal(1, result);
        Assert.Empty(output.ToString());
        Assert.Contains("Usage: cli files", error.ToString());
        Assert.Equal(0, checks);
    }

    private TestFilesCommand CreateCommand(ListingHandler handler) => new(handler)
    {
        BucketId = "my-bucket",
        Server = "https://api.test/api",
        ConfigPath = Path.Combine(_directory, "missing.json"),
        EnvironmentSettings = new CliEnvironment(
            Path.Combine(_directory, "missing.env"),
            name => name switch
            {
                "FISH_USERNAME" => "user",
                "FISH_PASSWORD" => "secret-password",
                "FISH_BUCKET_ID" => "environment-bucket",
                _ => null
            })
    };

    private static (int ExitCode, string Output, string Error) Run(FilesCommand command)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var result = command.Run(output: output, errorOutput: error);
        return (result, output.ToString(), error.ToString());
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private sealed class TestFilesCommand(HttpMessageHandler handler) : FilesCommand
    {
        protected override HttpClient CreateHttpClient() => new(handler, disposeHandler: false);
    }

    private sealed class ListingHandler : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public string Files { get; init; } = "[]";
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            if (path == "/api/auth/login")
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                return Task.FromResult(Json(HttpStatusCode.OK, """{"token":"memory-token"}"""));
            }
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("Bearer memory-token", request.Headers.Authorization?.ToString());
            return Task.FromResult(Json(Status, "{\"id\":\"bucket\",\"files\":" + Files + "}"));
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}
