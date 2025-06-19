using FishBucket;
using FishBucket.ApiClient;
using FishBucket.SyncClient;
using FishSyncClient.Files;
using System.Runtime.CompilerServices;

namespace gui;

public static class FishApiClientExtensions
{
    public static async Task<BucketSyncResult> Sync(
        this FishApiClient @this, 
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
        return await @this.Sync(id, bucketSyncFiles, cancellationToken);
    }

    public static async Task<BucketSyncResult> Sync(
        this FishApiClient @this,
        string id,
        ISyncFileCollection sources,
        IBucketSyncActionCollectionHandler actionHandler,
        CancellationToken cancellationToken = default)
    {
        int iterationCount = 0;
        BucketSyncResult result;
        while (true)
        {
            result = await @this.Sync(id, sources, cancellationToken);
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
