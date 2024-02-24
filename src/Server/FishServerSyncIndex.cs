namespace FishSyncClient;

public class FishServerSyncIndex
{
    public string? Version { get; set; }
    public FishServerFile[] Files { get; set; } = Array.Empty<FishServerFile>();
    public string[] SyncExcludes { get; set; } = Array.Empty<string>();
    public string[] SyncIncludes { get; set; } = Array.Empty<string>();
}