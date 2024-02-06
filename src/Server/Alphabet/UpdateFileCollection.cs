using System.Text.Json.Serialization;

namespace FishSyncClient.Server.Alphabet;

public class UpdateFileCollection
{
    [JsonPropertyName("lastUpdate")]
    public DateTimeOffset LastUpdate { get; set; }

    [JsonPropertyName("hashAlgorithm")]
    public string? HashAlgorithm { get; set; }
    
    [JsonPropertyName("files")]
    public UpdateFile[]? Files { get; set; }
}