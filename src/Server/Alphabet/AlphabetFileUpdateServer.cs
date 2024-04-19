using System.Text.Json;
using FishSyncClient.Files;
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

    public static PullIndex ToPullIndex(LauncherMetadata metadata, HttpClient httpClient, PathOptions options)
    {
        var excludeFiles = metadata.Launcher?.WhitelistFiles ?? Enumerable.Empty<string>();
        var excludeDirs = metadata.Launcher?.WhitelistDirs ?? Enumerable.Empty<string>();
        var excludePatterns = excludeDirs.Select(dir => dir + "/**").Concat(excludeFiles);

        return new PullIndex
        {
            Version = metadata.Files?.LastUpdate.ToString("O"),
            Files = ToFishServerFiles(metadata.Files ?? new(), httpClient, options).ToArray(),
            Excludes = excludePatterns.ToArray(),
            Includes = metadata.Launcher?.IncludeFiles ?? ["**"]
        };
    }

    public static IEnumerable<ReadableHttpSyncFile> ToFishServerFiles(UpdateFileCollection updateFiles, HttpClient httpClient, PathOptions options)
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

            yield return new ReadableHttpSyncFile(RootedPath.FromSubPath(file.Path, options), httpClient)
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