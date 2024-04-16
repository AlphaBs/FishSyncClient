using FishSyncClient.Files;

namespace FishSyncClient.Server.BucketSyncActions;

public interface IBucketSyncActionCollectionHandler
{
    Task Handle(ISyncFileCollection files, IEnumerable<BucketSyncAction> actions, CancellationToken cancellationToken);
}