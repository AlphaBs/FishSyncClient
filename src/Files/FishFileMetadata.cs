namespace FishSyncClient;

public record FishFileMetadata : FishPath
{
    public FishFileMetadata(
        RootedPath path,
        long size,
        string? checksum,
        string? checksumAlgorithm) : base(path)
    {
        Size = size;
        Checksum = checksum;
        ChecksumAlgorithm = checksumAlgorithm;
    }

    public long Size { get; }

    /// <summary>
    /// lower cased, hex string, without 0x
    /// </summary>
    public string? Checksum { get; }
    public string? ChecksumAlgorithm { get; }
}