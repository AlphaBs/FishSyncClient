using System.Text.Json.Serialization;

namespace FishSyncClient.Server.Alphabet;

public class LauncherMetadata
{
    [JsonPropertyName("lastInfoUpdate")]
    public DateTime LastInfoUpdate { get; set; }

    [JsonPropertyName("launcher")]
    public LauncherInfo? Launcher { get; set; }

    [JsonPropertyName("files")]
    public UpdateFileCollection? Files { get; set; }
}