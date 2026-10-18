using FishSyncClient.Cli;
using FishBucket.ApiClient;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace FishSyncClientTest;

public sealed class CliLoginTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "fish-cli-test-" + Guid.NewGuid());

    [Fact]
    public async Task BucketRequestsUseBearerAuthentication()
    {
        using var handler = new LoginHandler(HttpStatusCode.OK, "{\"id\":\"bucket\",\"files\":[]}");
        using var http = new HttpClient(handler);
        var settings = new CommandSettings(_directory, "https://example.com/api", "bucket", "saved-token");
        var api = new ProbeCommand().CreateApi(settings, http);

        await api.GetBucketFiles("bucket");

        Assert.Equal("Bearer saved-token", handler.Authorization);
    }

    [Fact]
    public async Task SyncRequestsUseBearerAuthentication()
    {
        using var handler = new LoginHandler(
            HttpStatusCode.OK,
            "{\"isSuccess\":true,\"requiredActions\":[],\"updatedAt\":\"2026-10-16T00:00:00Z\"}");
        using var http = new HttpClient(handler);
        var settings = new CommandSettings(_directory, "https://example.com/api", "bucket", "saved-token");
        var api = new ProbeCommand().CreateApi(settings, http);

        await api.Sync("bucket", Array.Empty<FishSyncClient.Files.SyncFile>(), default);

        Assert.Equal("Bearer saved-token", handler.Authorization);
    }

    [Fact]
    public async Task LoginSavesServerScopedTokenWithoutPassword()
    {
        var store = new CredentialStore(_directory);
        using var handler = new LoginHandler(HttpStatusCode.OK, "{\"token\":\"test-token\"}");
        using var http = new HttpClient(handler);
        var login = new CliLogin(http, store);

        await login.Login("https://EXAMPLE.com/api/", "test-user", "test-password");

        Assert.Equal("/api/auth/login", handler.Path);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Contains(body.RootElement.EnumerateObject(), property => property.Value.GetString() == "test-user");
        Assert.Contains(body.RootElement.EnumerateObject(), property => property.Value.GetString() == "test-password");
        Assert.Equal("test-token", await store.Load("https://example.com/api"));
        Assert.Null(await store.Load("https://other.example.com/api"));
        Assert.Null(await store.Load("https://example.com/other-api"));
        var persisted = await File.ReadAllTextAsync(store.GetPath("https://example.com/api"));
        Assert.DoesNotContain("test-password", persisted);
        Assert.DoesNotContain("test-user", persisted);
    }

    [Fact]
    public async Task FailedLoginPreservesPreviousToken()
    {
        var store = new CredentialStore(_directory);
        await store.Save("https://example.com/api", "previous-token");
        using var handler = new LoginHandler(HttpStatusCode.Unauthorized, "{}");
        using var http = new HttpClient(handler);
        var login = new CliLogin(http, store);

        await Assert.ThrowsAsync<FishApiException>(() => login.Login("https://example.com/api", "user", "wrong"));

        Assert.Equal("previous-token", await store.Load("https://example.com/api"));
    }

    [Fact]
    public async Task RepeatedLoginReplacesTokenAndKeepsPrivatePermissions()
    {
        var store = new CredentialStore(_directory);
        const string server = "https://example.com/api";
        await store.Save(server, "old-token");
        await store.Save(server, "new-token");

        Assert.Equal("new-token", await store.Load(server));
        Assert.Single(Directory.GetFiles(_directory));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(store.GetPath(server)));
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(_directory));
        }
    }

    [Fact]
    public async Task EmptyTokenDoesNotOverwriteSavedLogin()
    {
        var store = new CredentialStore(_directory);
        const string server = "https://example.com/api";
        await store.Save(server, "previous-token");

        await Assert.ThrowsAsync<ArgumentException>(() => store.Save(server, " "));

        Assert.Equal("previous-token", await store.Load(server));
    }

    [Theory]
    [InlineData("file:///tmp/token")]
    [InlineData("https://user:password@example.com/api")]
    [InlineData("https://example.com/api?token=secret")]
    [InlineData("https://example.com/api#fragment")]
    public void InvalidServerCannotBeUsedForCredentials(string server)
    {
        Assert.Throws<ArgumentException>(() => CredentialStore.NormalizeServer(server));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class LoginHandler(HttpStatusCode status, string response) : HttpMessageHandler
    {
        public string? Path { get; private set; }
        public string? Body { get; private set; }
        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.AbsolutePath;
            Authorization = request.Headers.Authorization?.ToString();
            if (request.Content is not null)
            {
                Body = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class ProbeCommand : SyncCommandBase
    {
        public FishApiClient CreateApi(CommandSettings settings, HttpClient http)
        {
            return CreateApiClient(settings, http);
        }

        protected override ValueTask<int> RunAsync()
        {
            return ValueTask.FromResult(0);
        }
    }
}
