using FishSyncClient.FileComparers;
using FishSyncClient.Files;

namespace FishSyncClient.Syncer;

public interface IFishFileSyncer
{
    ValueTask<FishFileSyncResult> Sync(
        IReadOnlyCollection<SyncFilePair> pairs,
        IFileComparer comparer,
        IProgress<FishFileProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default);
}