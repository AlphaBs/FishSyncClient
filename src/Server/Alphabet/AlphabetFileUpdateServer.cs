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
            Version = metadata.Files?.LastUpdate.ToString("O"),
            Files = ToFishServerFiles(metadata.Files ?? new(), options).ToArray(),
            SyncExcludes = excludePatterns.ToArray(),
            SyncIncludes = metadata.Launcher?.IncludeFiles ?? Array.Empty<string>()
        };
    }

    public static IEnumerable<ServerSyncFile> ToFishServerFiles(UpdateFileCollection updateFiles, PathOptions options)
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

            yield return new ServerSyncFile(RootedPath.FromSubPath(file.Path, options))
            {
                Metadata = new SyncFileMetadata
                {
                    Size = file.Size,
                    Checksum = file.Hash,
                    ChecksumAlgorithm = checksumAlgorithm,
                },
                Uploaded = DateTimeOffset.MinValue,
                Location = new Uri(file.Url)
            };
        }
    }
}