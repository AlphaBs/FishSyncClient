using System.Globalization;

namespace FishSyncClient.Cli;

public class FilesCommand : SyncCommandBase
{
    protected override async ValueTask<int> RunAsync()
    {
        var settings = await GetSettings(BucketId);
        using var http = CreateHttpClient();
        var apiClient = await CreateApiClient(settings, http);
        var bucketFiles = await apiClient.GetBucketFiles(settings.BucketId);
        var files = bucketFiles.Files
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToArray();
        if (files.Length == 0)
        {
            Output.WriteLine("No files found.");
            return 0;
        }

        Output.WriteLine("Path\tSize (bytes)\tMD5");
        foreach (var file in files)
        {
            var size = file.Metadata?.Size.ToString(CultureInfo.InvariantCulture) ?? "-";
            var checksum = file.Metadata?.Checksum?.ToHexString() ?? "-";
            Output.WriteLine($"{file.Path}\t{size}\t{checksum}");
        }
        Output.WriteLine($"Files: {files.Length}");
        return 0;
    }
}
