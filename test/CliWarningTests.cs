using System.CommandLine;
using FishSyncClient.Cli;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace FishSyncClientTest;

public sealed class CliWarningTests
{
    [Theory]
    [InlineData("\n", true)]
    [InlineData("y\n", true)]
    [InlineData("YES\n", true)]
    [InlineData("n\n", false)]
    [InlineData("No\n", false)]
    [InlineData("", false)]
    [InlineData("invalid\nn\n", false)]
    public void WarningLogsMatchesAndHandlesConsent(string answer, bool expected)
    {
        using var output = new StringWriter();
        using var input = new StringReader(answer);
        var proceed = SyncWarnings.Confirm(
            ["logs/a.txt", "logs/a.txt", "other.txt", "logs\\b.txt"],
            ["logs/**"],
            false,
            input,
            output);
        Assert.Equal(expected, proceed);
        Assert.Contains("[Y/n]", output.ToString());
        Assert.Contains("logs/b.txt", output.ToString());
        Assert.DoesNotContain("other.txt", output.ToString());
        Assert.Equal(1, output.ToString().Split("logs/a.txt").Length - 1);
    }

    [Fact]
    public void YesStillLogsMatchesWithoutReadingInput()
    {
        using var output = new StringWriter();
        Assert.True(SyncWarnings.Confirm([".env"], [".env"], true, new UnreadableInput(), output));
        Assert.Contains(".env", output.ToString());
        Assert.DoesNotContain("[Y/n]", output.ToString());
    }

    [Theory]
    [InlineData("pull", "-y")]
    [InlineData("pull", "--yes")]
    [InlineData("push", "-y")]
    [InlineData("push", "--yes")]
    public void BothSyncCommandsAcceptYesOptions(string command, string option)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var parser = CliApplication.CreateRootCommand(output, error);
        var result = parser.Parse([command, "bucket", option]);
        Assert.Empty(result.Errors);
        Assert.True(result.GetValue<bool>("--yes"));
    }

    [Fact]
    public void UnmatchedFilesDoNotPrompt()
    {
        using var output = new StringWriter();
        Assert.True(SyncWarnings.Confirm(["safe.txt"], ["logs/**"], false, new UnreadableInput(), output));
        Assert.Empty(output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PushOnlyContactsServerAfterConsent(bool assumeYes)
    {
        var directory = Path.Combine(Path.GetTempPath(), "fish-warning-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(directory, "logs"));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "logs", "debug.txt"), "log");
            var config = Path.Combine(directory, "settings.json");
            await File.WriteAllTextAsync(config, """{"WarningPatterns":["logs/**"]}""");
            using var handler = new SyncHandler();
            var command = new TestPush(handler)
            {
                ConfigPath = config,
                Root = directory,
                BucketId = "bucket",
                Server = "https://api.test/api",
                AssumeYes = assumeYes,
                EnvironmentSettings = new CliEnvironment(
                    Path.Combine(directory, "missing.env"),
                    name => name switch
                    {
                        "FISH_USERNAME" => "user",
                        "FISH_PASSWORD" => "secret",
                        _ => null
                    })
            };
            using var input = new StringReader("n\n");
            using var output = new StringWriter();
            using var error = new StringWriter();
            var result = command.Run(input, output, error);
            Assert.Equal(assumeYes ? 0 : 1, result);
            Assert.Equal(assumeYes ? 1 : 0, handler.SyncCalls);
            Assert.Empty(error.ToString());
            Assert.Contains("logs/debug.txt", output.ToString());
            if (assumeYes)
            {
                Assert.Contains("logs/debug.txt", handler.Body);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task PullCancellationProtectsLocalWarningFilesAndDoesNotDownload()
    {
        var directory = Path.Combine(Path.GetTempPath(), "fish-pull-warning-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(directory, "logs"));
        try
        {
            var local = Path.Combine(directory, "logs", "keep.txt");
            await File.WriteAllTextAsync(local, "keep");
            var config = Path.Combine(directory, "settings.json");
            await File.WriteAllTextAsync(config, """{"WarningPatterns":["logs/**"]}""");
            using var handler = new SyncHandler();
            var command = new TestPull(handler)
            {
                ConfigPath = config,
                Root = directory,
                BucketId = "bucket",
                Server = "https://api.test/api",
                EnvironmentSettings = new CliEnvironment(
                    Path.Combine(directory, "missing.env"),
                    name => name switch
                    {
                        "FISH_USERNAME" => "user",
                        "FISH_PASSWORD" => "secret",
                        _ => null
                    })
            };
            using var output = new StringWriter();
            using var error = new StringWriter();
            Assert.Equal(1, command.Run(new StringReader("n\n"), output, error));
            Assert.Empty(error.ToString());
            Assert.Contains("logs/keep.txt", output.ToString());
            Assert.Contains("logs/remote.txt", output.ToString());
            Assert.Equal("keep", await File.ReadAllTextAsync(local));
            Assert.Equal(1, handler.ListCalls);
            Assert.Equal(0, handler.DownloadCalls);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class TestPush(HttpMessageHandler handler) : PushCommand
    {
        protected override HttpClient CreateHttpClient() => new(handler, disposeHandler: false);
    }

    private sealed class TestPull(HttpMessageHandler handler) : PullCommand
    {
        protected override HttpClient CreateHttpClient() => new(handler, disposeHandler: false);
    }

    private sealed class UnreadableInput : TextReader
    {
        public override string? ReadLine() => throw new InvalidOperationException("Should not read stdin");
    }

    private sealed class SyncHandler : HttpMessageHandler
    {
        public int SyncCalls { get; private set; }
        public int ListCalls { get; private set; }
        public int DownloadCalls { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/auth/login"))
            {
                return Json(new { token = "test-token" });
            }
            if (request.Method == HttpMethod.Post)
            {
                SyncCalls++;
                Body = await request.Content!.ReadAsStringAsync(cancellationToken);
                return Json(new { isSuccess = true, requiredActions = Array.Empty<object>(), updatedAt = "2026-10-29T00:00:00Z" });
            }
            if (request.RequestUri!.AbsolutePath.EndsWith("/files"))
            {
                ListCalls++;
                return Json(new
                {
                    id = "bucket",
                    files = new[] { new { path = "logs/remote.txt", location = "https://api.test/download", metadata = new { size = 3, checksum = "900150983cd24fb0d6963f7d28e17f72" } } }
                });
            }
            DownloadCalls++;
            throw new InvalidOperationException("No download expected");
        }

        private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
        };
    }
}
