using System.Text.Json;
using System.Text.Json.Serialization;

namespace gui;

public class StringOrArrayToListConverter : JsonConverter<List<string?>>
{
    public override List<string?> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = new List<string?>();

        if (reader.TokenType == JsonTokenType.String)
        {
            // Single string value, add it to the list
            result.Add(reader.GetString());
        }
        else if (reader.TokenType == JsonTokenType.StartArray)
        {
            // Array of strings, iterate and add each string to the list
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    result.Add(reader.GetString());
                }
                else
                {
                    throw new JsonException("Expected string inside array");
                }
            }
        }
        else
        {
            throw new JsonException("Expected string or array of strings");
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, List<string?> value, JsonSerializerOptions options)
    {
        if (value.Count == 1)
        {
            // If there's only one item, write it as a single string
            writer.WriteStringValue(value[0]);
        }
        else
        {
            // Otherwise, write as an array
            writer.WriteStartArray();
            foreach (var item in value)
            {
                writer.WriteStringValue(item);
            }
            writer.WriteEndArray();
        }
    }
}
