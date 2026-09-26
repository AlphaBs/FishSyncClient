using System.Text.Json.Serialization;
using FishSyncClient.Progress;

namespace FishSyncClient.Syncer;

public class SyncerOptions
{
    /// <summary>Ordered rules. Missing rules are invalid; an empty list excludes every file.</summary>
    [JsonPropertyName("rules")]
    public IReadOnlyList<SyncRule>? Rules { get; set; }

    [JsonPropertyName("context")]
    public SyncContext Context { get; set; } = new();
    public IProgress<FileProgressEvent>? FileProgress { get; set; }
    public IProgress<SyncFileByteProgress>? ByteProgress { get; set; }
    public CancellationToken CancellationToken { get; set; }
}
