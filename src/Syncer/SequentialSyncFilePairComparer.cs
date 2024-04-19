using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Progress;

namespace FishSyncClient.Syncer;

public class SequentialSyncFilePairComparer : ISyncFilePairComparer
{
    public async ValueTask<SyncFilePairCompareResult> ComparePairs(
        IReadOnlyCollection<SyncFilePair> pairs,
        IFileComparer comparer,
        IProgress<FishFileProgressEventArgs>? fileProgress = null,
        IProgress<SyncFileByteProgress>? byteProgress = null,
        CancellationToken cancellationToken = default)
    {
        var updated = new List<SyncFilePair>();
        var identical = new List<SyncFilePair>();

        int fileProgressed = 0;
        foreach (var pair in pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            fileProgress?.Report(new FishFileProgressEventArgs(
                FishFileProgressEventType.StartCompare, fileProgressed, pairs.Count, pair.Source.Path.SubPath));

            var areEqual = await comparer.AreEqual(pair, cancellationToken);
            if (areEqual)
                identical.Add(pair);
            else
                updated.Add(pair);

            fileProgressed++;
            fileProgress?.Report(new FishFileProgressEventArgs(
                FishFileProgressEventType.DoneCompare, fileProgressed, pairs.Count, pair.Source.Path.SubPath));
        }

        return new SyncFilePairCompareResult(updated.ToArray(), identical.ToArray());
    }
}