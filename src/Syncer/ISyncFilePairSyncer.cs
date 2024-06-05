using FishSyncClient.FileComparers;
using FishSyncClient.Files;

namespace FishSyncClient.Syncer;

public interface ISyncFilePairSyncer
{
    Task<SyncFilePairCollectionCompareResult> CompareFilePairs(IEnumerable<SyncFilePair> pairs, IFileComparer comparer, SyncerOptions options);
    Task SyncFilePairs(IEnumerable<SyncFilePair> pairs, SyncerOptions options);
    Task<SyncFilePairCollectionCompareResult> CompareAndSyncFilePairs(IEnumerable<SyncFilePair> pairs, IFileComparer comparer, SyncerOptions options);
}

public record SyncFilePairCollectionCompareResult(
    IReadOnlyCollection<SyncFilePair> UpdatedFiles,
    IReadOnlyCollection<SyncFilePair> IdenticalFiles
);