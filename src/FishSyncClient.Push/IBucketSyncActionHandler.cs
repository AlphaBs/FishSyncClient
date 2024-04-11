namespace FishSyncClient.Push;

public interface IBucketSyncActionHandler
{
    bool CanHandle(BucketSyncAction action);
    ValueTask Handle(BucketSyncAction action);
}