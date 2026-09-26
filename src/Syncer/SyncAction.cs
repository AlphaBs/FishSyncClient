using System.Text.Json;
using System.Text.Json.Serialization;

namespace FishSyncClient.Syncer;

[JsonConverter(typeof(SyncActionJsonConverter))]
public enum SyncAction
{
    /// <summary>Install missing files, update changed files, and report target-only files for deletion.</summary>
    FullSync = 1,
    /// <summary>Install missing files and preserve all existing target files.</summary>
    InstallOnly = 2,
    /// <summary>Do not install, compare, update, or delete files.</summary>
    Exclude = 3
}

internal sealed class SyncActionJsonConverter : JsonConverter<SyncAction>
{
    public override SyncAction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("action must be a string.");
        return reader.GetString() switch
        {
            "fullSync" => SyncAction.FullSync,
            "installOnly" => SyncAction.InstallOnly,
            "exclude" => SyncAction.Exclude,
            _ => throw new JsonException("action must be fullSync, installOnly, or exclude.")
        };
    }

    public override void Write(Utf8JsonWriter writer, SyncAction value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            SyncAction.FullSync => "fullSync",
            SyncAction.InstallOnly => "installOnly",
            SyncAction.Exclude => "exclude",
            _ => throw new JsonException("Unknown sync action.")
        });
}
