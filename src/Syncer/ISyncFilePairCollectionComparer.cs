using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Progress;

namespace FishSyncClient.Syncer;

public interface ISyncFilePairCollectionComparer
{
    ValueTask<SyncFilePairCollectionCompareResult> ComparePairs(
        IReadOnlyCollection<SyncFilePair> pairs,
        IFileComparer comparer,
        IProgress<FileProgressEvent>? fileProgress = null,
        IProgress<SyncFileByteProgress>? byteProgress = null,
        CancellationToken cancellationToken = default);
}

public record SyncFilePairCollectionCompareResult(
    IReadOnlyCollection<SyncFilePair> UpdatedFiles,
    IReadOnlyCollection<SyncFilePair> IdenticalFiles
);