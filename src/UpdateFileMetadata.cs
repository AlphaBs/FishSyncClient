using System.Text.Json.Serialization;

namespace AlphabetUpdater;

public class UpdateFileMetadata
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; }

    [JsonPropertyName("checksum")]
    public string Checksum { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
