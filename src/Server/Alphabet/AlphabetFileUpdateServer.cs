using System.Text.Json;
using FishSyncClient.Server.Alphabet;

namespace FishSyncClient.Server;

public class AlphabetFileUpdateServer
{
    public static async Task<LauncherMetadata> GetLauncherMetadata(HttpClient httpClient, Uri host)
    {
        var res = await httpClient.GetAsync(host);
        var resStream = await res.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<LauncherMetadata>(resStream) 
            ?? new LauncherMetadata();
    }

    public static FishServerSyncIndex ToFishServerSyncIndex(LauncherMetadata metadata, PathOptions options)
    {
        var excludeFiles = metadata.Launcher?.WhitelistFiles ?? Enumerable.Empty<string>();
        var excludeDirs = metadata.Launcher?.WhitelistDirs ?? Enumerable.Empty<string>();
        var excludePatterns = excludeDirs.Select(dir => dir + "/**").Concat(excludeFiles);

        return new FishServerSyncIndex
        {
            Name = metadata.Launcher?.Name,
            Version = metadata.Files?.LastUpdate.ToString("O"),
            Files = ToFishServerFiles(metadata.Files ?? new(), options).ToArray(),
            PathSyncExcludes = excludePatterns.ToArray(),
            FileSyncIncludes = metadata.Launcher?.IncludeFiles ?? Array.Empty<string>()
        };
    }

    public static IEnumerable<FishServerFile> ToFishServerFiles(UpdateFileCollection updateFiles, PathOptions options)
    {
        if (updateFiles.Files == null)
            yield break;

        var checksumAlgorithm = string.IsNullOrEmpty(updateFiles.HashAlgorithm) 
            ? "md5" 
            : updateFiles.HashAlgorithm;

        foreach (var file in updateFiles.Files)
        {
            if (string.IsNullOrEmpty(file.Path))
                continue;

            yield return new FishServerFile(
                path: RootedPath.FromSubPath(file.Path, options),
                size: file.Size,
                checksum: file.Hash,
                checksumAlgorithm: checksumAlgorithm,
                uploaded: DateTimeOffset.MinValue,
                location: new Uri(file.Url)
            );
        }
    }
}