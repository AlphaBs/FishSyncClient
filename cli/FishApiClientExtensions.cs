using FishBucket;
using FishBucket.ApiClient;
using FishBucket.SyncClient;
using FishSyncClient.Files;

namespace FishSyncClient.Cli;

public static class FishApiClientExtensions
{
    public static async Task<BucketSyncResult> Sync(
        this FishApiClient apiClient,
        string id,
        IEnumerable<SyncFile> files,
        CancellationToken cancellationToken)
    {
        var bucketSyncFiles = files.Select(file => new BucketSyncFile
        {
            Path = file.Path.SubPath,
            Checksum = file.Metadata?.Checksum?.ChecksumHexString,
            Size = file.Metadata?.Size ?? 0,
        });
        return await apiClient.Sync(id, bucketSyncFiles, cancellationToken);
    }

    public static async Task<BucketSyncResult> Sync(
        this FishApiClient apiClient,
        string id,
        ISyncFileCollection sources,
        IBucketSyncActionCollectionHandler actionHandler,
        CancellationToken cancellationToken = default)
    {
        var iterationCount = 0;
        BucketSyncResult result;
        while (true)
        {
            result = await apiClient.Sync(id, sources, cancellationToken);
            if (result.IsSuccess)
                break;

            await actionHandler.Handle(sources, result.RequiredActions, cancellationToken);

            iterationCount++;
            if (iterationCount > 10)
                break;
        }

        return result;
    }
}
