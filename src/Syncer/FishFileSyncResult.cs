namespace FishSyncClient.Syncer;

public class FishFileSyncResult
{
    public FishFileSyncResult(FishFileMetadata[] updated, FishFileMetadata[] identical)
    {
        UpdatedFiles = updated;
        IdenticalFiles = identical;
    }

    public FishFileMetadata[] UpdatedFiles { get; }
    public FishFileMetadata[] IdenticalFiles { get; }
}