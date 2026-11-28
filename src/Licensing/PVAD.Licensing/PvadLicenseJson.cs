using System.Text.Json;
using System.Text.Json.Serialization;

namespace PVAD.Licensing;

internal static class PvadLicenseJson
{
    internal const int MaximumEnvelopeBytes = 64 * 1024;
    internal const int MaximumPayloadBytes = 32 * 1024;
    internal const string Algorithm = "ECDSA_P256_SHA256_P1363";
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = false,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 12,
        Converters = { new JsonStringEnumConverter<PvadLicenseModel>(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    internal static T Read<T>(ReadOnlyMemory<byte> bytes, int maximumBytes)
    {
        if (bytes.IsEmpty || bytes.Length > maximumBytes)
            throw Error("PVADLIC003", "The license data exceeds its size limit or is empty.");

        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 12 });
            CheckProperties(document.RootElement);
            return JsonSerializer.Deserialize<T>(bytes.Span, Options)
                ?? throw Error("PVADLIC004", "The license document is malformed.");
        }
        catch (JsonException)
        {
            throw Error("PVADLIC004", "The license document is malformed.");
        }
    }

    internal static byte[] ReadBounded(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        Span<byte> block = stackalloc byte[4096];
        while (true)
        {
            var read = stream.Read(block[..Math.Min(block.Length, MaximumEnvelopeBytes + 1 - (int)buffer.Length)]);
            if (read == 0)
                return buffer.ToArray();
            buffer.Write(block[..read]);
            if (buffer.Length > MaximumEnvelopeBytes)
                throw Error("PVADLIC003", "The license data exceeds its size limit.");
        }
    }

    private static void CheckProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw Error("PVADLIC004", "Duplicate license properties are not allowed.");
                CheckProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in value.EnumerateArray())
                CheckProperties(element);
        }
    }

    internal static PvadLicenseException Error(string code, string message) => new(code, message);
}
