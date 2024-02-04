namespace FishSyncClient;

public record FishServerFile : FishFileMetadata
{
    public FishServerFile(
        RootedPath path,
        long size,
        string checksum,
        string checksumAlgorithm,
        DateTimeOffset uploaded,
        Uri location)
        : base(path, size, checksum, checksumAlgorithm)
    {
        Uploaded = uploaded;
        Location = location;
    }

    public DateTimeOffset Uploaded { get; }
    public Uri Location { get; }
}