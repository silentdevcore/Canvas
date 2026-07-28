using System.Text.Json;

namespace PXA.Observability.WebhookRelay;

public static class AlertmanagerPayloadSanitizer
{
    private static readonly string[] AllowedLabels =
    [
        "alertname",
        "severity",
        "service",
        "environment",
    ];

    private static readonly string[] AllowedAnnotations =
    [
        "summary",
        "description",
        "dashboard_path",
        "runbook_id",
    ];

    public static byte[] Sanitize(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("alerts", out var alerts) ||
            alerts.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("The Alertmanager payload must contain an alerts array.");
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            WriteBoundedString(writer, "status", root, "status", 32);
            writer.WritePropertyName("alerts");
            writer.WriteStartArray();

            var count = 0;
            foreach (var alert in alerts.EnumerateArray())
            {
                if (++count > 100)
                    throw new InvalidDataException("The Alertmanager payload exceeds 100 alerts.");
                if (alert.ValueKind != JsonValueKind.Object)
                    throw new InvalidDataException("Each alert must be an object.");

                writer.WriteStartObject();
                WriteBoundedString(writer, "status", alert, "status", 32);
                WriteBoundedString(writer, "startsAt", alert, "startsAt", 64);
                WriteBoundedString(writer, "endsAt", alert, "endsAt", 64);
                WriteAllowlistedObject(writer, alert, "labels", AllowedLabels, 160);
                WriteAllowlistedObject(writer, alert, "annotations", AllowedAnnotations, 512);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static void WriteAllowlistedObject(
        Utf8JsonWriter writer,
        JsonElement source,
        string propertyName,
        IEnumerable<string> allowedProperties,
        int maximumLength)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartObject();
        if (source.TryGetProperty(propertyName, out var values) &&
            values.ValueKind == JsonValueKind.Object)
        {
            foreach (var allowedProperty in allowedProperties)
                WriteBoundedString(
                    writer,
                    allowedProperty,
                    values,
                    allowedProperty,
                    maximumLength);
        }

        writer.WriteEndObject();
    }

    private static void WriteBoundedString(
        Utf8JsonWriter writer,
        string outputName,
        JsonElement source,
        string sourceName,
        int maximumLength)
    {
        if (!source.TryGetProperty(sourceName, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text))
            return;
        writer.WriteString(
            outputName,
            text.Length <= maximumLength ? text : text[..maximumLength]);
    }
}
