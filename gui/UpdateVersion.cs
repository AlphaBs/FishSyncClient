using System.Text.Json.Serialization;

namespace gui;

internal class UpdateVersion
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("download")]
    public string? Download { get; set; }
}
