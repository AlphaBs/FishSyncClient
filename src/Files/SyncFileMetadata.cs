namespace FishSyncClient.Files;

public sealed record SyncFileMetadata
{
    public long Size { get; init; }

    public SyncFileChecksum? Checksum { get; init; }
}
