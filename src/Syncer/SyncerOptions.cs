using FishSyncClient.Progress;

namespace FishSyncClient.Syncer;

public sealed record SyncerOptions
{
    /// <summary>Ordered rules. Must contain at least one rule and cover every source and target path.</summary>
    public IReadOnlyList<SyncRule>? Rules { get; init; }

    public SyncContext Context { get; init; } = new();
    public IProgress<FileProgressEvent>? FileProgress { get; init; }
    public IProgress<SyncFileByteProgress>? ByteProgress { get; init; }
    public CancellationToken CancellationToken { get; init; }
}
