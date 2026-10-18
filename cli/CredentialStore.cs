using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FishSyncClient.Cli;

public sealed class CredentialStore
{
    private readonly string _directory;

    public CredentialStore(string? directory = null)
    {
        _directory = directory ?? GetDefaultDirectory();
    }

    public static string NormalizeServer(string server)
    {
        if (!Uri.TryCreate(server.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http")
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("올바른 서버 API 주소를 입력해 주세요.");
        }

        return uri.AbsoluteUri.TrimEnd('/');
    }

    public string GetPath(string server)
    {
        var normalizedServer = NormalizeServer(server);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedServer));
        var filename = Convert.ToHexString(hash)
            .ToLowerInvariant() + ".json";
        return Path.Combine(_directory, filename);
    }

    public async Task<string?> Load(string server)
    {
        var path = GetPath(server);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        var credential = await JsonSerializer.DeserializeAsync<Credential>(stream);
        if (credential?.Server != NormalizeServer(server))
        {
            throw new IOException("저장된 로그인 정보의 서버 주소가 일치하지 않습니다. 다시 로그인해 주세요.");
        }

        return credential.Token;
    }

    public async Task Save(string server, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("서버가 빈 로그인 토큰을 반환했습니다.");
        }

        var path = GetPath(server);
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(_directory);
        }
        else
        {
            var directoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            Directory.CreateDirectory(_directory, directoryMode);
            File.SetUnixFileMode(_directory, directoryMode);
        }

        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        try
        {
            await using (var stream = new FileStream(temporaryPath, options))
            {
                var credential = new Credential(NormalizeServer(server), token);
                await JsonSerializer.SerializeAsync(stream, credential);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string GetDefaultDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "FishSyncClient", "credentials");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(home, "Library", "Application Support", "FishSyncClient", "credentials");
        }

        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(configHome) || !Path.IsPathFullyQualified(configHome))
        {
            configHome = Path.Combine(home, ".config");
        }

        return Path.Combine(configHome, "fishsyncclient", "credentials");
    }

    private sealed record Credential(string Server, string Token);
}
