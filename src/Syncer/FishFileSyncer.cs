using FishSyncClient.FileComparers;
using FishSyncClient.Files;

namespace FishSyncClient.Syncer;

public class FishFileSyncer
{
    public async ValueTask<FishFileSyncResult> Sync(
        IEnumerable<SyncFilePair> pairs,
        IFileComparer comparer,
        IProgress<FishFileProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var updated = new List<SyncFilePair>();
        var identical = new List<SyncFilePair>();

        var pairArr = pairs.ToArray();
        for (int i = 0; i < pairArr.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pair = pairArr[i];
            progress?.Report(new FishFileProgressEventArgs(i + 1, pairArr.Length, pair.Source.Path));

            var areEqual = await comparer.AreEqual(pair, cancellationToken);
            if (areEqual)
                identical.Add(pair);
            else
                updated.Add(pair);
        }

        if (pairArr.Any())
            progress?.Report(new FishFileProgressEventArgs(pairArr.Length, pairArr.Length, pairArr.Last().Source.Path));
        return new FishFileSyncResult(updated.ToArray(), identical.ToArray());
    }
}

public class FishFileSyncResult
{
    public FishFileSyncResult(SyncFilePair[] updated, SyncFilePair[] identical)
    {
        UpdatedFiles = updated;
        IdenticalFiles = identical;
    }

    public SyncFilePair[] UpdatedFiles { get; }
    public SyncFilePair[] IdenticalFiles { get; }
}