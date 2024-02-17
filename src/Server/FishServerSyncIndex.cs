namespace FishSyncClient;

public class FishServerSyncIndex
{
    public string? Name { get; set; }
    public string? Version { get; set; }
    public FishServerFile[] Files { get; set; } = Array.Empty<FishServerFile>();
    public string[] PathSyncExcludes { get; set; } = Array.Empty<string>();
    public string[] FileSyncIncludes { get; set; } = Array.Empty<string>();
}