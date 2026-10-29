using FishBucket.ApiClient;
using FishSyncClient.Cli;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace FishSyncClientTest;

public sealed class CliAuthenticationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "fish-auth-" + Guid.NewGuid());

    public CliAuthenticationTests() => Directory.CreateDirectory(_directory);

    [Theory]
    [InlineData(null, null, "FISH_USERNAME", "FISH_PASSWORD")]
    [InlineData("", "secret", "FISH_USERNAME", null)]
    [InlineData("user", "", "FISH_PASSWORD", null)]
    [InlineData("  ", "secret", "FISH_USERNAME", null)]
    [InlineData("user", "  ", "FISH_PASSWORD", null)]
    public async Task MissingCredentialsFailBeforeServerRequest(
        string? username,
        string? password,
        string missing,
        string? alsoMissing)
    {
        using var handler = new AuthHandler();
        using var http = new HttpClient(handler);
        var command = Probe(username, password);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => command.Client(http));
        Assert.Contains(missing, error.Message);
        if (alsoMissing != null)
        {
            Assert.Contains(alsoMissing, error.Message);
        }
        Assert.Contains("설정해 주세요", error.Message);
        Assert.DoesNotContain("secret", error.Message);
        Assert.Equal(0, handler.Requests);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task EachOperationAuthenticatesAndUsesMemoryTokenWithoutSavingFiles(bool dotenv, bool sync)
    {
        var command = Probe(" user ", " secret with spaces ");
        if (dotenv)
        {
            var path = Path.Combine(_directory, ".env");
            await File.WriteAllTextAsync(path, "FISH_USERNAME=' user '\nFISH_PASSWORD=' secret with spaces '\n");
            command.EnvironmentSettings = new CliEnvironment(path, _ => null);
        }
        using var handler = new AuthHandler();
        for (var run = 0; run < 2; run++)
        {
            using var http = new HttpClient(handler, disposeHandler: false);
            var client = await command.Client(http);
            if (sync)
            {
                await client.Sync("bucket", Array.Empty<FishSyncClient.Files.SyncFile>(), default);
            }
            else
            {
                await client.GetBucketFiles("bucket");
            }
        }
        Assert.Equal(2, handler.Authentications);
        Assert.Equal(2, handler.AuthorizedRequests);
        using var body = JsonDocument.Parse(handler.LoginBody!);
        Assert.Contains(body.RootElement.EnumerateObject(), property => property.Value.GetString() == "user");
        Assert.Contains(body.RootElement.EnumerateObject(), property => property.Value.GetString() == " secret with spaces ");
        Assert.Equal(dotenv ? new[] { ".env" } : Array.Empty<string>(), Directory.GetFiles(_directory).Select(Path.GetFileName));
    }

    [Fact]
    public async Task FailedAuthenticationDoesNotStartBucketRequests()
    {
        using var handler = new AuthHandler { Status = HttpStatusCode.Unauthorized };
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<FishApiException>(() => Probe("user", "wrong").Client(http));
        Assert.Equal(1, handler.Authentications);
        Assert.Equal(0, handler.AuthorizedRequests);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task EmptyTokenIsRejected()
    {
        using var handler = new AuthHandler { Token = "" };
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Probe("user", "secret").Client(http));
        Assert.Equal(0, handler.AuthorizedRequests);
    }

    [Theory]
    [InlineData("file:///tmp/token")]
    [InlineData("https://user:password@example.com/api")]
    [InlineData("https://example.com/api?token=secret")]
    [InlineData("https://example.com/api#fragment")]
    public void InvalidServerCannotBeUsedForAuthentication(string server)
    {
        Assert.Throws<ArgumentException>(() => ServerAddress.Normalize(server));
    }

    [Fact]
    public void NormalizesServerAddress()
    {
        Assert.Equal("https://example.com/api", ServerAddress.Normalize("https://EXAMPLE.com:443/api/"));
    }

    private ProbeCommand Probe(string? username, string? password) => new()
    {
        EnvironmentSettings = new CliEnvironment(
            Path.Combine(_directory, "missing.env"),
            name => name switch
            {
                "FISH_USERNAME" => username,
                "FISH_PASSWORD" => password,
                _ => null
            })
    };

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private sealed class ProbeCommand : SyncCommandBase
    {
        public Task<FishApiClient> Client(HttpClient http) =>
            CreateApiClient(new CommandSettings("/unused", "https://api.test/api", "bucket"), http);

        protected override ValueTask<int> RunAsync() => ValueTask.FromResult(0);
    }

    private sealed class AuthHandler : HttpMessageHandler
    {
        public string Token { get; init; } = "memory-token";
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public int Requests { get; private set; }
        public int Authentications { get; private set; }
        public int AuthorizedRequests { get; private set; }
        public string? LoginBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            if (request.RequestUri!.AbsolutePath.EndsWith("/auth/login"))
            {
                Authentications++;
                Assert.Null(request.Headers.Authorization);
                LoginBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(Status)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new { token = Token }), Encoding.UTF8, "application/json")
                };
            }
            AuthorizedRequests++;
            Assert.Equal("Bearer memory-token", request.Headers.Authorization?.ToString());
            var json = request.Method == HttpMethod.Post
                ? "{\"isSuccess\":true,\"requiredActions\":[],\"updatedAt\":\"2026-10-29T00:00:00Z\"}"
                : "{\"id\":\"bucket\",\"files\":[]}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }
}
