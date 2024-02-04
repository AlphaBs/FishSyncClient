namespace FishSyncClient.Syncer;

public class FishPathSyncResult
{
    public FishPathSyncResult(FishPath[] added, FishPath[] duplicated, FishPath[] deleted)
    {
        AddedPaths = added;
        DuplicatedPaths = duplicated;
        DeletedPaths = deleted;
    }

    public FishPath[] AddedPaths { get; }
    public FishPath[] DuplicatedPaths { get; }
    public FishPath[] DeletedPaths { get; }
}