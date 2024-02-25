namespace FishSyncClient;

public class FishServerSyncIndex
{
    public string? Version { get; set; }
    public ServerSyncFile[] Files { get; set; } = Array.Empty<ServerSyncFile>();
    public IEnumerable<string> SyncExcludes { get; set; } = Enumerable.Empty<string>();
    public IEnumerable<string> SyncIncludes { get; set; } = Enumerable.Empty<string>();
}