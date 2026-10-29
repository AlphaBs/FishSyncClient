using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace FishSyncClient.Cli;

public sealed record CliUpdateManifest(
    int SchemaVersion,
    string? Version,
    Dictionary<string, CliUpdateArtifact>? Files);

public sealed record CliUpdateArtifact(string? Url);

public sealed record CliUpdateCheckState(DateTimeOffset LastCheckedUtc);

public sealed class CliUpdateService(
    HttpClient http,
    string cacheDirectory,
    TimeProvider? timeProvider = null)
{
    private static readonly Uri Origin = new("https://update.snowfrost.kr");
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public static void CheckForUpdates(TextWriter output)
    {
        var os = GetPlatform();
        var arch = GetArchitecture();
        if (os is null || arch is null)
        {
            return;
        }

        var currentVersion = typeof(CliUpdateService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        var applicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(applicationData))
        {
            return;
        }
        var cacheDirectory = Path.Combine(applicationData, "FishSyncClientCli");
        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3)
        };
        new CliUpdateService(http, cacheDirectory)
            .CheckAndNotifyAsync(output, os, arch, currentVersion)
            .GetAwaiter()
            .GetResult();
    }

    public async Task CheckAndNotifyAsync(
        TextWriter output,
        string os,
        string arch,
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(cacheDirectory);
            var statePath = Path.Combine(cacheDirectory, $"update-check-{os}-{arch}.json");
            // 다른 CLI 프로세스가 확인 중이면 잠금을 기다리지 않고 동기화를 진행한다.
            await using var stateFile = new FileStream(
                statePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            var now = _timeProvider.GetUtcNow();
            var state = await ReadState(stateFile, cancellationToken);
            if (state is not null
                && now >= state.LastCheckedUtc
                && now - state.LastCheckedUtc < CheckInterval)
            {
                return;
            }

            // 네트워크 오류가 나더라도 한 시간 동안 재조회하지 않도록 시도 시각을 먼저 저장한다.
            stateFile.Position = 0;
            stateFile.SetLength(0);
            await JsonSerializer.SerializeAsync(
                stateFile,
                new CliUpdateCheckState(now),
                CliJsonContext.Default.CliUpdateCheckState,
                cancellationToken);
            await stateFile.FlushAsync(cancellationToken);

            var update = await GetLatest(os, arch, cancellationToken);
            if (update is null || string.Equals(update.Version, currentVersion, StringComparison.Ordinal))
            {
                return;
            }
            output.WriteLine($"A new version of FishSyncClient CLI is available: {update.Version}. Please update.");
            output.WriteLine($"Download: {update.Download.AbsoluteUri}");
        }
        catch (Exception error) when (error is HttpRequestException
            or IOException
            or UnauthorizedAccessException
            or JsonException
            or OperationCanceledException
            or NotSupportedException)
        {
            // 업데이트 서버나 로컬 캐시의 문제로 파일 동기화를 중단하지 않는다.
        }
    }

    public static int ShowLatest(TextWriter output)
    {
        var os = GetPlatform();
        var arch = GetArchitecture();
        if (os is null || arch is null)
        {
            output.WriteLine("No release is available for this platform.");
            return 1;
        }
        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3)
        };
        return new CliUpdateService(http, "")
            .ShowLatestAsync(output, os, arch)
            .GetAwaiter()
            .GetResult();
    }

    public async Task<int> ShowLatestAsync(
        TextWriter output,
        string os,
        string arch,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 수동 확인은 캐시 상태와 관계없이 최신 릴리스를 조회한다.
            var update = await GetLatest(os, arch, cancellationToken);
            if (update is null)
            {
                output.WriteLine("No release is available for this platform.");
                return 1;
            }
            output.WriteLine($"Latest version: {update.Version}");
            output.WriteLine($"Download: {update.Download.AbsoluteUri}");
            return 0;
        }
        catch (Exception error) when (error is HttpRequestException
            or IOException
            or JsonException
            or OperationCanceledException
            or NotSupportedException)
        {
            output.WriteLine("Could not check for updates. Please try again.");
            return 1;
        }
    }

    private async Task<AvailableRelease?> GetLatest(
        string os,
        string arch,
        CancellationToken cancellationToken)
    {
        var path = $"/v1/apps/FishSyncClientCli/{Uri.EscapeDataString(os)}/{Uri.EscapeDataString(arch)}/latest";
        using var response = await http.GetAsync(new Uri(Origin, path), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        var manifest = await response.Content.ReadFromJsonAsync(
            CliJsonContext.Default.CliUpdateManifest,
            cancellationToken);
        if (manifest?.SchemaVersion != 1
            || string.IsNullOrWhiteSpace(manifest.Version)
            || manifest.Files is null
            || !manifest.Files.TryGetValue("zip", out var zip)
            || string.IsNullOrWhiteSpace(zip?.Url)
            || !Uri.TryCreate(Origin, zip.Url, out var download)
            || download.Scheme != "https"
            || download.Host != Origin.Host
            || !download.IsDefaultPort
            || download.UserInfo.Length != 0)
        {
            return null;
        }
        return new AvailableRelease(manifest.Version, download);
    }

    private sealed record AvailableRelease(string Version, Uri Download);

    private static async Task<CliUpdateCheckState?> ReadState(
        Stream stream,
        CancellationToken cancellationToken)
    {
        if (stream.Length == 0)
        {
            return null;
        }
        try
        {
            return await JsonSerializer.DeserializeAsync(
                stream,
                CliJsonContext.Default.CliUpdateCheckState,
                cancellationToken);
        }
        catch (JsonException)
        {
            // 손상된 캐시는 이번 확인 시각으로 복구한다.
            return null;
        }
    }

    private static string? GetPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return "windows";
        }
        if (OperatingSystem.IsMacOS())
        {
            return "darwin";
        }
        return OperatingSystem.IsLinux() ? "linux" : null;
    }

    private static string? GetArchitecture() => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 => "x64",
        Architecture.Arm64 => "aarch64",
        _ => null
    };
}
