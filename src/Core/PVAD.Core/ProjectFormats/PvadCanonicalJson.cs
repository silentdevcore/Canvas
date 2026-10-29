using System.Text.Json;
using System.Text.Json.Nodes;

namespace PVAD.Core.ProjectFormats;

internal static class PvadCanonicalJson
{
    internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 128,
        PropertyNameCaseInsensitive = false,
    };

    public static byte[] Serialize<T>(T value)
    {
        var node = JsonSerializer.SerializeToNode(value, SerializerOptions)
            ?? throw new PvadProjectFormatException("PXAPROJECT009", "The project JSON could not be serialized.");
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
        WriteNode(writer, node);
        writer.Flush();
        return stream.ToArray();
    }

    public static T Deserialize<T>(ReadOnlySpan<byte> bytes, int maximumDepth)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions(SerializerOptions)
            {
                MaxDepth = maximumDepth,
            }) ?? throw new JsonException("The JSON value is null.");
        }
        catch (JsonException)
        {
            throw new PvadProjectFormatException("PXAPROJECT009", "The project contains invalid or unsupported JSON.");
        }
    }

    private static void WriteNode(Utf8JsonWriter writer, JsonNode? node)
    {
        switch (node)
        {
            case null:
                writer.WriteNullValue();
                break;
            case JsonObject obj:
                writer.WriteStartObject();
                foreach (var property in obj.OrderBy(item => item.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Key);
                    WriteNode(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonArray array:
                writer.WriteStartArray();
                foreach (var item in array)
                    WriteNode(writer, item);
                writer.WriteEndArray();
                break;
            default:
                node.WriteTo(writer, SerializerOptions);
                break;
        }
    }
}
