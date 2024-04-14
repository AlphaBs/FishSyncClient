using FishSyncClient.Common;

namespace FishSyncClient.Push;

public interface IBucketSyncActionHandler
{
    bool CanHandle(BucketSyncAction action);
    ValueTask Handle(
        Stream content,
        BucketSyncAction action, 
        IProgress<ByteProgress>? progress, 
        CancellationToken cancellationToken);
}