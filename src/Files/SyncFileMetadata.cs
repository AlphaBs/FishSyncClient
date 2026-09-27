namespace FishSyncClient.Files;

public class SyncFileMetadata
{
    public long Size { get; set; }

    /// <summary>
    /// Optional expected checksum. Null omits checksum verification; a non-null value must
    /// contain a supported algorithm and a complete hexadecimal digest when compared.
    /// Empty fields or a default SyncFileChecksum value do not represent omission.
    /// </summary>
    public SyncFileChecksum? Checksum { get; set; }
}
