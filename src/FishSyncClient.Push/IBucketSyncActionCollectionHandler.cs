namespace FishSyncClient.Push;

public interface IBucketSyncActionCollectionHandler
{
    Task Handle(IReadOnlyCollection<BucketSyncAction> actions, CancellationToken cancellationToken);
}