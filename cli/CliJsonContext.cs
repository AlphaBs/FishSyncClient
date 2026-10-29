using System.Text.Json.Serialization;

namespace FishSyncClient.Cli;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true)]
[JsonSerializable(typeof(CliConfig))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, string>))]
[JsonSerializable(typeof(CliUpdateManifest))]
[JsonSerializable(typeof(CliUpdateCheckState))]
internal partial class CliJsonContext : JsonSerializerContext
{
}
