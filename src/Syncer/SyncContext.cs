using System.Text.Json.Serialization;

namespace FishSyncClient.Syncer;

/// <summary>Version state supplied by the caller for one sync operation.</summary>
public sealed record SyncContext
{
    [JsonPropertyName("isNewVersion")]
    public bool IsNewVersion { get; init; }

    [JsonPropertyName("isForced")]
    public bool IsForced { get; init; }
}
