using FishBucket;
using FishSyncClient.Files;

namespace FishSyncClient.Cli;

public static class CliSyncFiles
{
    public static IEnumerable<SyncFile> EnumerateLocalFiles(string root, PathOptions pathOptions) =>
        RootedPath.FromDirectory(root, pathOptions).Select(CreateLocalFile);

    public static SyncFile CreateLocalFile(RootedPath path)
    {
        var fileInfo = new FileInfo(path.GetFullPath());
        using var fs = File.OpenRead(fileInfo.FullName);
        var checksum = ChecksumAlgorithms.ComputeMD5(fs);
        return new LocalSyncFile(path)
        {
            Metadata = new SyncFileMetadata
            {
                Size = fileInfo.Length,
                Checksum = new SyncFileChecksum(ChecksumAlgorithmNames.MD5, checksum)
            }
        };
    }

    public static SyncFile CreateHttpFile(BucketFile file, HttpClient httpClient, PathOptions pathOptions)
    {
        if (string.IsNullOrEmpty(file.Path) || string.IsNullOrEmpty(file.Location))
            throw new ArgumentException("Bucket file path or location is empty.");

        return new ReadableHttpSyncFile(RootedPath.FromSubPath(file.Path, pathOptions), httpClient)
        {
            Location = new Uri(file.Location),
            Metadata = new SyncFileMetadata
            {
                Size = file.Metadata.Size,
                Checksum = new SyncFileChecksum(ChecksumAlgorithmNames.MD5, file.Metadata.Checksum)
            }
        };
    }
}
