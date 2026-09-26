using System.Text.Json;
using System.Text.Json.Serialization;

namespace FishSyncClient.Syncer;

[JsonConverter(typeof(SyncConditionJsonConverter))]
public enum SyncCondition
{
    Always,
    OnNewVersion
}

internal sealed class SyncConditionJsonConverter : JsonConverter<SyncCondition>
{
    public override SyncCondition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("condition must be a string.");
        return reader.GetString() switch
        {
            "always" => SyncCondition.Always,
            "onNewVersion" => SyncCondition.OnNewVersion,
            _ => throw new JsonException("condition must be always or onNewVersion.")
        };
    }

    public override void Write(Utf8JsonWriter writer, SyncCondition value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            SyncCondition.Always => "always",
            SyncCondition.OnNewVersion => "onNewVersion",
            _ => throw new JsonException("Unknown sync condition.")
        });
}
