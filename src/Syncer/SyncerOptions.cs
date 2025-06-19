using FishSyncClient.PathMatchers;
using FishSyncClient.Progress;

namespace FishSyncClient.Syncer;

public class SyncerOptions
{
    public IPathMatcher TargetPathMatcher { get; set; } = StaticPathMatcher.MatchAll();
    public IProgress<FileProgressEvent>? FileProgress { get; set; }
    public IProgress<SyncFileByteProgress>? ByteProgress { get; set; }
    public CancellationToken CancellationToken { get; set; }
}