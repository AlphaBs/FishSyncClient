using FishSyncClient.FileComparers;
using FishSyncClient.Files;

namespace FishSyncClient.Syncer;

public class SequentialFileSyncer : IFishFileSyncer
{
    public async ValueTask<FishFileSyncResult> Sync(
        IReadOnlyCollection<SyncFilePair> pairs,
        IFileComparer comparer,
        IProgress<FishFileProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var updated = new List<SyncFilePair>();
        var identical = new List<SyncFilePair>();

        int progressed = 0;
        foreach (var pair in pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new FishFileProgressEventArgs(
                FishFileProgressEventType.Start, progressed, pairs.Count, pair.Source.Path));

            var areEqual = await comparer.AreEqual(pair, cancellationToken);
            if (areEqual)
                identical.Add(pair);
            else
                updated.Add(pair);

            progressed++;
            progress?.Report(new FishFileProgressEventArgs(
                FishFileProgressEventType.Done, progressed, pairs.Count, pair.Source.Path));
        }

        return new FishFileSyncResult(updated.ToArray(), identical.ToArray());
    }
}