namespace FishSyncClient.Files;

public class SyncFileMetadata
{
    public long Size { get; set; }
    public SyncFileChecksum? Checksum { get; set; }
}