namespace FishSyncClient.Files;

public readonly struct SyncFileChecksum
{
    public SyncFileChecksum(string algName, string checksum) => 
        (AlgorithmName, ChecksumHexString) = (algName, checksum);

    public readonly string AlgorithmName;
    public readonly string ChecksumHexString;
}