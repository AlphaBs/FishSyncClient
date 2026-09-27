namespace FishSyncClient.Syncer;

/// <summary>Version state supplied by the caller for one sync operation.</summary>
public sealed record SyncContext
{
    public bool IsNewVersion { get; init; }

    public bool IsForced { get; init; }
}
