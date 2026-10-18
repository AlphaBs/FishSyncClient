using KeySharp;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace FishSyncClient.Gui;

public interface IPasswordStore
{
    bool IsSupported { get; }
    Task<string?> Load(string server, string username);
    Task Save(string server, string username, string password);
    Task Delete(string server, string username);
}

public sealed class PasswordStore : IPasswordStore
{
    private const string Package = "FishSyncClient.Gui";
    private const string Service = "Login";

    // 네이티브 작업과 오류 조회가 다른 요청과 섞이지 않도록 직렬화한다.
    private static readonly object KeyringLock = new();

    public bool IsSupported
    {
        get
        {
            var architecture = RuntimeInformation.ProcessArchitecture;
            if (OperatingSystem.IsWindows())
            {
                return architecture == Architecture.X64;
            }
            return OperatingSystem.IsMacOS()
                && architecture is Architecture.X64 or Architecture.Arm64;
        }
    }

    public Task<string?> Load(string server, string username) => Task.Run(() =>
    {
        EnsureSupported();
        var key = GetKey(server, username);
        lock (KeyringLock)
        {
            try
            {
                return Keyring.GetPassword(Package, Service, key);
            }
            catch (KeyringException ex) when (ex.Type == ErrorType.NotFound)
            {
                return null;
            }
        }
    });

    public Task Save(string server, string username, string password) => Task.Run(() =>
    {
        EnsureSupported();
        var key = GetKey(server, username);
        lock (KeyringLock)
        {
            Keyring.SetPassword(Package, Service, key, password);
        }
    });

    public Task Delete(string server, string username) => Task.Run(() =>
    {
        EnsureSupported();
        var key = GetKey(server, username);
        lock (KeyringLock)
        {
            try
            {
                Keyring.DeletePassword(Package, Service, key);
            }
            catch (KeyringException ex) when (ex.Type == ErrorType.NotFound)
            {
                // 이미 없는 항목은 삭제가 완료된 것으로 처리한다.
            }
        }
    });

    private void EnsureSupported()
    {
        if (!IsSupported)
        {
            throw new PlatformNotSupportedException(
                "비밀번호 저장은 Windows x64와 macOS x64·ARM64에서 지원합니다.");
        }
    }

    public static string GetKey(string server, string username)
    {
        var uri = new Uri(server.Trim(), UriKind.Absolute);
        if (uri.Scheme is not ("https" or "http") || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("올바른 서버 API 주소를 입력해 주세요.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        // 길이 제한과 구분자 충돌을 피하고 서버 경로 및 계정의 대소문자는 보존한다.
        var identity = System.Text.Json.JsonSerializer.Serialize(new[] { uri.AbsoluteUri.TrimEnd('/'), username.Trim() });
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return "FishSyncClient.Gui/" + Convert.ToHexString(hash);
    }
}
