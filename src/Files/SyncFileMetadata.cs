namespace FishSyncClient.Files;

public record SyncFileMetadata
{
    public long Size { get; set; }

    /// <summary>
    /// lower cased, hex string, without 0x
    /// </summary>
    public string? Checksum { get; set; }
    public string? ChecksumAlgorithm { get; set; }

    public void SetChecksum(string algName, byte[] checksum) 
    {
        ChecksumAlgorithm = algName;
        Checksum = HashHelper.ToHexString(checksum);
    }
}