namespace FishSyncClient.Syncer;

public interface IFishFileSyncer
{
    ValueTask<FishFileSyncResult> Sync(
        string root, 
        IEnumerable<FishFileMetadata> files, 
        IProgress<FishFileProgressEventArgs>? progress);
}