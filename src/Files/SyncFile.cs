namespace FishSyncClient;

public record SyncFile
{
    public SyncFile(RootedPath path)
    {
        Path = path;
    }

    public RootedPath Path { get; }
    public SyncFileMetadata? Metadata { get; set; }

    public override string ToString()
    {
        return Path.ToString();
    }
}