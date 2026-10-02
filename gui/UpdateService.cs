using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FishSyncClient.Gui;

public sealed record AvailableUpdate(string Version, Uri Download);

public sealed class UpdateService(HttpClient http)
{
    private static readonly Uri Origin = new("https://update.snowfrost.kr");
    public static string CurrentVersion => typeof(UpdateService).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(x => x.Key == "ClientVersion").Value!;

    public static string? Platform => OperatingSystem.IsWindows() ? "windows" :
        OperatingSystem.IsMacOS() ? "darwin" : OperatingSystem.IsLinux() ? "linux" : null;
    public static string? ArchitectureName => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 => "x64",
        Architecture.Arm64 => "aarch64",
        _ => null
    };

    public async Task<AvailableUpdate?> Check(string os, string arch, string currentVersion, CancellationToken ct)
    {
        var path = $"/v1/apps/FishSyncClientGui/{Uri.EscapeDataString(os)}/{Uri.EscapeDataString(arch)}/latest";
        using var response = await http.GetAsync(new Uri(Origin, path), ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var manifest = await response.Content.ReadFromJsonAsync<Manifest>(cancellationToken: ct);
        if (manifest?.SchemaVersion != 1 || !IsNewer(manifest.Version, currentVersion) ||
            manifest.Files == null || !manifest.Files.TryGetValue("zip", out var zip) || zip == null || string.IsNullOrWhiteSpace(zip.Url) ||
            !Uri.TryCreate(Origin, zip.Url, out var download) || download.Scheme != "https" ||
            download.Host != Origin.Host || !download.IsDefaultPort || download.UserInfo.Length != 0)
            return null;
        return new(manifest.Version!, download);
    }

    // Supports the existing YYYYMMDD releases and numeric dotted release versions.
    public static bool IsNewer(string? latest, string current)
    {
        static long[]? Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var parts = text.TrimStart('v', 'V').Split('.');
            var values = new long[parts.Length];
            for (var i = 0; i < parts.Length; i++)
                if (!long.TryParse(parts[i], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out values[i])) return null;
            return values;
        }
        var left = Parse(latest);
        var right = Parse(current);
        if (left == null || right == null) return false;
        for (var i = 0; i < Math.Max(left.Length, right.Length); i++)
        {
            var comparison = (i < left.Length ? left[i] : 0).CompareTo(i < right.Length ? right[i] : 0);
            if (comparison != 0) return comparison > 0;
        }
        return false;
    }

    private sealed record Manifest(int SchemaVersion, string? Version, Dictionary<string, Artifact>? Files);
    private sealed record Artifact(string? Url);
}
