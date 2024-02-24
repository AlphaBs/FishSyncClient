namespace FishSyncClient;

public class FishServerSyncIndex
{
    public string? Version { get; set; }
    public ServerSyncFile[] Files { get; set; } = Array.Empty<ServerSyncFile>();
    public string[] SyncExcludes { get; set; } = Array.Empty<string>();
    public string[] SyncIncludes { get; set; } = Array.Empty<string>();
}