namespace FishSyncClient.Files;

public struct SyncFilePair
{
    public SyncFilePair(SyncFile source, SyncFile target) =>
        (Source, Target) = (source, target);

    public SyncFile Source;
    public SyncFile Target;
}