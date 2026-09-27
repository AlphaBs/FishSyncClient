using FishSyncClient.Progress;

namespace FishSyncClient.Syncer;

public sealed record SyncerOptions
{
    /// <summary>Ordered rules. Missing rules are invalid; an empty list excludes every file.</summary>
    public IReadOnlyList<SyncRule>? Rules { get; init; }

    public SyncContext Context { get; init; } = new();
    public IProgress<FileProgressEvent>? FileProgress { get; init; }
    public IProgress<SyncFileByteProgress>? ByteProgress { get; init; }
    public CancellationToken CancellationToken { get; init; }
}
