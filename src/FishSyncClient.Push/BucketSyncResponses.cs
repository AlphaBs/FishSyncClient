using System.Text.Json.Serialization;

namespace FishSyncClient.Push;

public record BucketSyncResult
(
    [property:JsonPropertyName("isSuccess")]
    bool IsSuccess,

    [property:JsonPropertyName("requiredActions")]
    IReadOnlyList<BucketSyncAction> Actions,

    [property:JsonPropertyName("updatedAt")]
    DateTimeOffset UpdatedAt
);

public record BucketSyncAction
(
    [property:JsonPropertyName("path")]
    string Path,

    [property:JsonPropertyName("action")]
    SyncAction Action
);

public record SyncAction
(
    [property:JsonPropertyName("type")] 
    string Type,

    [property:JsonPropertyName("parameters")] 
    IReadOnlyDictionary<string, string>? Parameters
);