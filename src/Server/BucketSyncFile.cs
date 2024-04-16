namespace FishSyncClient.Server;

public class BucketSyncFile
{
    public string? Path { get; set; }
    public long Size { get; set; }
    public string? Checksum { get; set; }
}