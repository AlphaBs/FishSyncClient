using FishSyncClient.FileComparers;
using FishSyncClient.Files;

namespace FishSyncClient.Syncer;

public class SequentialFileSyncer : IFishFileSyncer
{
    public async ValueTask<FishFileSyncResult> Sync(
        IReadOnlyCollection<SyncFilePair> pairs,
        IFileComparer comparer,
        IProgress<FishFileProgressEventArgs>? fileProgress = null,
        IProgress<ByteProgress>? byteProgress = null,
        CancellationToken cancellationToken = default)
    {
        var updated = new List<SyncFilePair>();
        var identical = new List<SyncFilePair>();

        long totalBytes = pairs.Select(pair => pair.Source.Metadata?.Size ?? 0).Sum();
        long progressedBytes = 0;

        int fileProgressed = 0;
        foreach (var pair in pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            fileProgress?.Report(new FishFileProgressEventArgs(
                FishFileProgressEventType.Start, fileProgressed, pairs.Count, pair.Source.Path.SubPath));

            var areEqual = await comparer.AreEqual(pair, cancellationToken);
            if (areEqual)
            {
                var progressReporter = new SyncProgress<ByteProgress>(progress => 
                {
                    totalBytes += progress.TotalBytes;
                    progressedBytes += progress.ProgressedBytes;
                    byteProgress?.Report(new ByteProgress
                    {
                        TotalBytes = totalBytes,
                        ProgressedBytes = progressedBytes
                    });
                });
                await StreamProgressHelper.SyncFilePair(pair, progressReporter, cancellationToken);
                identical.Add(pair);
            }
            else
            {
                progressedBytes += (pair.Source.Metadata?.Size ?? 0);
                byteProgress?.Report(new ByteProgress
                {
                    TotalBytes = totalBytes,
                    ProgressedBytes = progressedBytes
                });
                updated.Add(pair);
            }

            fileProgressed++;
            fileProgress?.Report(new FishFileProgressEventArgs(
                FishFileProgressEventType.Done, fileProgressed, pairs.Count, pair.Source.Path.SubPath));
        }

        return new FishFileSyncResult(updated.ToArray(), identical.ToArray());
    }
}