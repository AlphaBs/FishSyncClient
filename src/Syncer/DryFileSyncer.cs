using FishSyncClient.FileComparers;
using FishSyncClient.Files;

namespace FishSyncClient.Syncer;

public class DryFileSyncer : IFishFileSyncer
{
    public async ValueTask<FishFileSyncResult> Sync(
        IReadOnlyCollection<SyncFilePair> pairs, 
        IFileComparer comparer, 
        IProgress<FishFileProgressEventArgs>? fileProgress = null, 
        IProgress<ByteProgress>? byteProgress = null, 
        CancellationToken cancellationToken = default)
    {
        var identicalFiles = new List<SyncFilePair>();
        var updatedFiles = new List<SyncFilePair>();

        foreach (var pair in pairs)
        {
            var areEqual = await comparer.AreEqual(pair, cancellationToken);
            if (areEqual)
                identicalFiles.Add(pair);
            else
                updatedFiles.Add(pair);
        }

        return new FishFileSyncResult(updatedFiles, identicalFiles);
    }
}