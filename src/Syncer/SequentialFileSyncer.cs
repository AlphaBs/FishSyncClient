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
        RootedPath lastFilePath = new(); 
        foreach (var pair in pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new FishFileProgressEventArgs(progressed, pairs.Count, lastFilePath = pair.Source.Path));

            var areEqual = await comparer.AreEqual(pair, cancellationToken);
            if (areEqual)
                identical.Add(pair);
            else
                updated.Add(pair);

            progressed++;
        }

        if (progressed > 0)
            progress?.Report(new FishFileProgressEventArgs(pairs.Count, pairs.Count, lastFilePath));
        return new FishFileSyncResult(updated.ToArray(), identical.ToArray());
    }
}