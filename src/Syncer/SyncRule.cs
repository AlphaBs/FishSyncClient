using System.Text.Json.Serialization;

namespace FishSyncClient.Syncer;

/// <summary>The first rule matching both the relative path and the condition wins.</summary>
public sealed record SyncRule
{
    [JsonPropertyName("action")]
    public SyncAction Action { get; init; }

    [JsonPropertyName("condition")]
    public SyncCondition Condition { get; init; } = SyncCondition.Always;

    [JsonPropertyName("pattern")]
    public string Pattern { get; init; } = "";
}
