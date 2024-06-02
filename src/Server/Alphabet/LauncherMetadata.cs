using FishSyncClient.Files;
using FishSyncClient.Syncer;
using System.Net.Http;
using System.Text.Json.Serialization;

namespace FishSyncClient.Server.Alphabet;

public class LauncherMetadata
{
    [JsonPropertyName("lastInfoUpdate")]
    public DateTime LastInfoUpdate { get; set; }

    [JsonPropertyName("launcher")]
    public LauncherInfo? Launcher { get; set; }

    [JsonPropertyName("files")]
    public UpdateFileCollection? Files { get; set; }

    public SyncerOptions ConvertToSyncerOptions()
    {
        var excludeFiles = Launcher?.WhitelistFiles ?? Enumerable.Empty<string>();
        var excludeDirs = Launcher?.WhitelistDirs ?? Enumerable.Empty<string>();
        var excludePatterns = excludeDirs.Select(dir => dir + "/**").Concat(excludeFiles);

        return new SyncerOptions
        {
            //Version = Files?.LastUpdate.ToString("O"),
            Excludes = excludePatterns.ToArray(),
            Includes = Launcher?.IncludeFiles ?? ["**"]
        };
    }

    public IEnumerable<SyncFile> GetSyncFiles(HttpClient httpClient, PathOptions options)
    {
        if (Files == null)
            return [];
        return Files.ToSyncFiles(httpClient, options);
    }
}