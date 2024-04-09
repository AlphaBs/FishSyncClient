using System.Text.Json.Serialization;

namespace FishSyncClient.Server.Alphabet;

public class UpdateFile
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("hash")]
    public string? Hash { get; set; }

    [JsonPropertyName("tags")]
    public string? Tags { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}