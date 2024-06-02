using FishSyncClient.Progress;

namespace FishSyncClient.Syncer;

public class SyncerOptions
{
    public IEnumerable<string> Excludes { get; set; } = Enumerable.Empty<string>();
    public IEnumerable<string> Includes { get; set; } = ["**"];
    public IProgress<FileProgressEvent>? FileProgress { get; set; }
    public IProgress<SyncFileByteProgress>? ByteProgress { get; set; }
    public CancellationToken CancellationToken { get; set; }
}