using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Progress;

namespace FishSyncClient.Syncer;

public interface ISyncFilePairSyncer
{
    Task<SyncFilePairCollectionCompareResult> CompareFilePairs(
        IEnumerable<SyncFilePair> pairs, 
        IFileComparer comparer, 
        IProgress<FileProgressEvent>? fileProgress,
        IProgress<SyncFileByteProgress>? byteProgress,
        CancellationToken cancellationToken);

    Task SyncFilePairs(
        IEnumerable<SyncFilePair> pairs, 
        IProgress<FileProgressEvent>? fileProgress,
        IProgress<SyncFileByteProgress>? byteProgress,
        CancellationToken cancellationToken);

    Task<SyncFilePairCollectionCompareResult> CompareAndSyncFilePairs(
        IEnumerable<SyncFilePair> pairs, 
        IFileComparer comparer, 
        IProgress<FileProgressEvent>? fileProgress,
        IProgress<SyncFileByteProgress>? byteProgress,
        CancellationToken cancellationToken);
}

public record SyncFilePairCollectionCompareResult(
    IReadOnlyCollection<SyncFilePair> UpdatedFiles,
    IReadOnlyCollection<SyncFilePair> IdenticalFiles
);