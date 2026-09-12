using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PXA.Migration.Abstractions;

namespace PXA.Migration.Pdf.Viewer.Nutrient;

public sealed class NutrientInstantJsonConverter
{
    public const string ProviderKey = "nutrient-viewer";
    public const string InstantJsonV1 = "https://pspdfkit.com/instant-json/v1";
    public const int MaximumPayloadBytes = 10 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> AnnotationTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["pspdfkit/comment/note"] = "note",
            ["pspdfkit/text"] = "freeText",
            ["pspdfkit/stamp"] = "stamp",
            ["pspdfkit/image"] = "image",
            ["pspdfkit/markup/redaction"] = "redaction",
            ["pspdfkit/shape/line"] = "line",
            ["pspdfkit/shape/rectangle"] = "rectangle",
            ["pspdfkit/shape/ellipse"] = "circle",
            ["pspdfkit/ink"] = "ink",
            ["pspdfkit/markup/highlight"] = "highlight",
            ["pspdfkit/markup/underline"] = "underline",
            ["pspdfkit/markup/strikeout"] = "strikeout",
        };

    public NutrientInstantJsonResult Convert(
        string instantJson,
        IReadOnlyList<NutrientPageGeometry> pages,
        string? sourceName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instantJson);
        ArgumentNullException.ThrowIfNull(pages);

        if (System.Text.Encoding.UTF8.GetByteCount(instantJson) > MaximumPayloadBytes)
            throw new NutrientInstantJsonException("CANMIGNUTJSON001", "Instant JSON exceeds the 10 MiB migration limit.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(instantJson, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
        }
        catch (JsonException exception)
        {
            throw new NutrientInstantJsonException("CANMIGNUTJSON002", "Instant JSON is not valid JSON.", exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new NutrientInstantJsonException("CANMIGNUTJSON003", "Instant JSON must be a JSON object.");

            var format = ReadString(root, "format");
            if (!string.Equals(format, InstantJsonV1, StringComparison.Ordinal))
                throw new NutrientInstantJsonException("CANMIGNUTJSON004", $"Unsupported Instant JSON format '{format ?? "<missing>"}'.");

            var diagnostics = new List<NutrientViewerDiagnostic>();
            var annotations = new List<PxaViewerAnnotation>();
            var formFields = ReadFormFields(root, diagnostics);
            var formValues = ReadFormValues(root, formFields, diagnostics);
            var idRemap = new Dictionary<string, string>(StringComparer.Ordinal);
            var usedIds = new HashSet<string>(StringComparer.Ordinal);

            if (root.TryGetProperty("annotations", out var sourceAnnotations))
            {
                if (sourceAnnotations.ValueKind != JsonValueKind.Array)
                    throw new NutrientInstantJsonException("CANMIGNUTJSON005", "The annotations property must be an array.");

                var index = 0;
                foreach (var source in sourceAnnotations.EnumerateArray())
                {
                    index++;
                    if (source.ValueKind != JsonValueKind.Object)
                    {
                        diagnostics.Add(Warning("CANMIGNUTJSON010", $"Annotation {index} is not an object and was omitted."));
                        continue;
                    }

                    var nutrientType = ReadString(source, "type") ?? string.Empty;
                    if (ReadBool(source, "isSignature") == true)
                    {
                        diagnostics.Add(Warning("CANMIGNUTJSON030", $"Signature ink annotation {index} was omitted; saved signatures and biometric drawing data are never migrated."));
                        continue;
                    }

                    if (!AnnotationTypes.TryGetValue(nutrientType, out var pxaType))
                    {
                        diagnostics.Add(Warning("CANMIGNUTJSON011", $"Annotation type '{nutrientType}' is not supported and was omitted."));
                        continue;
                    }

                    var pageIndex = ReadInt(source, "pageIndex");
                    if (pageIndex is null || pageIndex < 0 || pageIndex >= pages.Count)
                    {
                        diagnostics.Add(Warning("CANMIGNUTJSON012", $"Annotation {index} has an invalid pageIndex and was omitted."));
                        continue;
                    }

                    if (!TryReadBoundingBox(source, out var bounds))
                    {
                        diagnostics.Add(Warning("CANMIGNUTJSON013", $"Annotation {index} has no valid bounding box and was omitted."));
                        continue;
                    }

                    var geometry = pages[pageIndex.Value];
                    ValidateGeometry(geometry, pageIndex.Value);
                    var normalized = NormalizeBounds(bounds, geometry);
                    var sourceId = ReadString(source, "id") ?? $"nutrient-{index}";
                    var id = UniqueId(sourceId, usedIds);
                    if (!string.Equals(sourceId, id, StringComparison.Ordinal))
                        idRemap[sourceId] = id;

                    if (pxaType == "redaction")
                    {
                        diagnostics.Add(Warning("CANMIGNUTJSON020",
                            $"Redaction annotation '{sourceId}' was converted as a mark only; no source content was removed."));
                    }

                    if (source.TryGetProperty("action", out _) || source.TryGetProperty("additionalActions", out _))
                    {
                        diagnostics.Add(Warning("CANMIGNUTJSON031", $"Annotation '{sourceId}' contains actions. URI, launch, form, and JavaScript actions were omitted."));
                    }

                    var pointLines = ReadInkPointLines(source, geometry, diagnostics, sourceId);
                    var quadPoints = ReadQuadPoints(source, geometry, diagnostics, sourceId);
                    var metadata = ReadSafeMetadata(source);

                    annotations.Add(new PxaViewerAnnotation(
                        id,
                        pxaType,
                        pageIndex.Value + 1,
                        normalized.X,
                        normalized.Y,
                        normalized.Width,
                        normalized.Height,
                        ReadText(source),
                        ReadString(source, "creatorName") ?? string.Empty,
                        ReadDate(source, "createdAt"),
                        ReadNullableDate(source, "updatedAt"),
                        ReadColor(source, "strokeColor") ?? ReadColor(source, "color") ?? "#2563eb",
                        ReadLocked(source),
                        ReadOpacity(source),
                        ReadDouble(source, "strokeWidth") ?? ReadDouble(source, "lineWidth"),
                        ReadColor(source, "fillColor"),
                        ReadLineEnding(source, "start", diagnostics, sourceId),
                        ReadLineEnding(source, "end", diagnostics, sourceId),
                        pointLines?.FirstOrDefault(),
                        pointLines,
                        quadPoints,
                        metadata));
                }
            }

            if (root.TryGetProperty("comments", out var comments) && comments.ValueKind == JsonValueKind.Array && comments.GetArrayLength() > 0)
                diagnostics.Add(Warning("CANMIGNUTJSON032", "Comment threads are not yet represented by the PXA annotation sidecar and require review."));
            if (root.TryGetProperty("attachments", out var attachments) && attachments.ValueKind == JsonValueKind.Object && attachments.EnumerateObject().Any())
                diagnostics.Add(Warning("CANMIGNUTJSON033", "Instant JSON attachments were not copied into the annotation sidecar."));

            var sidecar = new PxaViewerAnnotationSidecar(1, sourceName, DateTimeOffset.UtcNow, annotations);
            var formPayload = new PxaViewerFormValuePayload(1, sourceName, formValues);
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true,
            };

            var sidecarJson = JsonSerializer.Serialize(sidecar, options);
            var formJson = JsonSerializer.Serialize(formPayload, options);
            var trustReport = NutrientViewerTrustEvidence.FromInstantJson(diagnostics);
            return new NutrientInstantJsonResult(
                "Nutrient",
                ProviderKey,
                "PSPDFKit Instant JSON v1",
                diagnostics.Count > 0 ? "reviewRequired" : "compatible",
                "regenerated",
                sidecarJson,
                formJson,
                diagnostics,
                idRemap,
                Sha256(instantJson),
                Sha256(sidecarJson),
                Sha256(sidecarJson + "\n" + formJson),
                NutrientMigrationCapabilityReport.FromDiagnostics(annotations.Count + formValues.Count, diagnostics))
            {
                PdfTrust = trustReport
            };
        }
    }

    private static void ValidateGeometry(NutrientPageGeometry geometry, int pageIndex)
    {
        if (!double.IsFinite(geometry.Width) || geometry.Width <= 0
            || !double.IsFinite(geometry.Height) || geometry.Height <= 0
            || !double.IsFinite(geometry.CropLeft) || !double.IsFinite(geometry.CropTop))
        {
            throw new NutrientInstantJsonException("CANMIGNUTJSON006", $"Page geometry {pageIndex} is invalid.");
        }

        if (geometry.Rotation is not (0 or 90 or 180 or 270))
            throw new NutrientInstantJsonException("CANMIGNUTJSON007", $"Page geometry {pageIndex} has an unsupported rotation.");
    }

    private static PercentageBounds NormalizeBounds(SourceBounds bounds, NutrientPageGeometry page)
    {
        var x = bounds.Left - page.CropLeft;
        var y = bounds.Top - page.CropTop;
        var width = bounds.Width;
        var height = bounds.Height;
        var pageWidth = page.Width;
        var pageHeight = page.Height;

        (x, y, width, height, pageWidth, pageHeight) = page.Rotation switch
        {
            90 => (pageHeight - y - height, x, height, width, pageHeight, pageWidth),
            180 => (pageWidth - x - width, pageHeight - y - height, width, height, pageWidth, pageHeight),
            270 => (y, pageWidth - x - width, height, width, pageHeight, pageWidth),
            _ => (x, y, width, height, pageWidth, pageHeight),
        };

        return new PercentageBounds(
            Percent(x, pageWidth),
            Percent(y, pageHeight),
            Percent(width, pageWidth),
            Percent(height, pageHeight));
    }

    private static PxaViewerInkPoint NormalizePoint(double sourceX, double sourceY, NutrientPageGeometry page)
    {
        var x = sourceX - page.CropLeft;
        var y = sourceY - page.CropTop;
        var pageWidth = page.Width;
        var pageHeight = page.Height;
        (x, y, pageWidth, pageHeight) = page.Rotation switch
        {
            90 => (pageHeight - y, x, pageHeight, pageWidth),
            180 => (pageWidth - x, pageHeight - y, pageWidth, pageHeight),
            270 => (y, pageWidth - x, pageHeight, pageWidth),
            _ => (x, y, pageWidth, pageHeight),
        };
        return new PxaViewerInkPoint(Percent(x, pageWidth), Percent(y, pageHeight));
    }

    private static double Percent(double value, double dimension) =>
        Math.Round(Math.Clamp(value / dimension * 100d, 0d, 100d), 6, MidpointRounding.AwayFromZero);

    private static bool TryReadBoundingBox(JsonElement source, out SourceBounds bounds)
    {
        bounds = default;
        if (!source.TryGetProperty("bbox", out var bbox) || bbox.ValueKind != JsonValueKind.Array)
            return false;

        var values = bbox.EnumerateArray().Select(ReadFiniteNumber).ToArray();
        if (values.Length != 4 || values.Any(static value => value is null)
            || values[2] <= 0 || values[3] <= 0)
            return false;

        bounds = new SourceBounds(values[0]!.Value, values[1]!.Value, values[2]!.Value, values[3]!.Value);
        return true;
    }

    private static double? ReadFiniteNumber(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number)
            ? number
            : null;

    private static string UniqueId(string sourceId, HashSet<string> usedIds)
    {
        var candidate = string.IsNullOrWhiteSpace(sourceId) ? "nutrient-annotation" : sourceId.Trim();
        if (usedIds.Add(candidate))
            return candidate;

        var suffix = 2;
        while (!usedIds.Add($"{candidate}-{suffix}")) suffix++;
        return $"{candidate}-{suffix}";
    }

    private static string ReadText(JsonElement source)
    {
        if (source.TryGetProperty("text", out var text))
        {
            if (text.ValueKind == JsonValueKind.String)
                return text.GetString() ?? string.Empty;
            if (text.ValueKind == JsonValueKind.Object && text.TryGetProperty("value", out var value))
                return value.GetString() ?? string.Empty;
        }

        return ReadString(source, "contents") ?? ReadString(source, "name") ?? string.Empty;
    }

    private static string? ReadString(JsonElement source, string property) =>
        source.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt(JsonElement source, string property) =>
        source.TryGetProperty(property, out var value) && value.TryGetInt32(out var result) ? result : null;

    private static bool? ReadBool(JsonElement source, string property) =>
        source.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static double? ReadDouble(JsonElement source, string property) =>
        source.TryGetProperty(property, out var value) ? ReadFiniteNumber(value) : null;

    private static double? ReadOpacity(JsonElement source)
    {
        var value = ReadDouble(source, "opacity");
        if (value is null) return null;
        return Math.Round(Math.Clamp(value.Value <= 1 ? value.Value * 100 : value.Value, 0, 100), 3);
    }

    private static DateTimeOffset ReadDate(JsonElement source, string property) =>
        DateTimeOffset.TryParse(ReadString(source, property), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
            ? value
            : DateTimeOffset.UnixEpoch;

    private static DateTimeOffset? ReadNullableDate(JsonElement source, string property) =>
        DateTimeOffset.TryParse(ReadString(source, property), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
            ? value
            : null;

    private static bool? ReadLocked(JsonElement source)
    {
        if (ReadBool(source, "locked") is { } direct) return direct;
        if (!source.TryGetProperty("flags", out var flags) || flags.ValueKind != JsonValueKind.Array) return null;
        return flags.EnumerateArray().Any(value => value.ValueKind == JsonValueKind.String
            && value.GetString() is "locked" or "lockedContents" or "readOnly");
    }

    private static IReadOnlyList<IReadOnlyList<PxaViewerInkPoint>>? ReadInkPointLines(
        JsonElement source,
        NutrientPageGeometry geometry,
        ICollection<NutrientViewerDiagnostic> diagnostics,
        string sourceId)
    {
        if (!source.TryGetProperty("lines", out var lines) || lines.ValueKind != JsonValueKind.Object
            || !lines.TryGetProperty("points", out var pointLines) || pointLines.ValueKind != JsonValueKind.Array)
            return null;

        var result = new List<IReadOnlyList<PxaViewerInkPoint>>();
        var totalPoints = 0;
        foreach (var sourceLine in pointLines.EnumerateArray().Take(100))
        {
            if (sourceLine.ValueKind != JsonValueKind.Array) continue;
            var line = new List<PxaViewerInkPoint>();
            foreach (var point in sourceLine.EnumerateArray())
            {
                if (++totalPoints > 10_000)
                {
                    diagnostics.Add(Warning("CANMIGNUTJSON034", $"Ink annotation '{sourceId}' exceeded the 10,000-point limit and was truncated."));
                    return result;
                }
                if (point.ValueKind != JsonValueKind.Array) continue;
                var coordinates = point.EnumerateArray().Select(ReadFiniteNumber).ToArray();
                if (coordinates.Length < 2 || coordinates[0] is null || coordinates[1] is null) continue;
                line.Add(NormalizePoint(coordinates[0]!.Value, coordinates[1]!.Value, geometry));
            }
            if (line.Count > 0) result.Add(line);
        }

        if (result.Count == 0)
            diagnostics.Add(Warning("CANMIGNUTJSON035", $"Ink annotation '{sourceId}' contains no valid point lines."));
        return result.Count == 0 ? null : result;
    }

    private static IReadOnlyList<PxaViewerMarkupQuad>? ReadQuadPoints(
        JsonElement source,
        NutrientPageGeometry geometry,
        ICollection<NutrientViewerDiagnostic> diagnostics,
        string sourceId)
    {
        if (!source.TryGetProperty("rects", out var rects) || rects.ValueKind != JsonValueKind.Array) return null;
        var result = new List<PxaViewerMarkupQuad>();
        foreach (var rect in rects.EnumerateArray().Take(1_000))
        {
            var values = rect.ValueKind == JsonValueKind.Array
                ? rect.EnumerateArray().Select(ReadFiniteNumber).ToArray()
                : [];
            if (values.Length != 4 || values.Any(value => value is null) || values[2] <= 0 || values[3] <= 0) continue;
            var normalized = NormalizeBounds(new SourceBounds(values[0]!.Value, values[1]!.Value, values[2]!.Value, values[3]!.Value), geometry);
            result.Add(new PxaViewerMarkupQuad(
                normalized.X, normalized.Y,
                normalized.X + normalized.Width, normalized.Y,
                normalized.X, normalized.Y + normalized.Height,
                normalized.X + normalized.Width, normalized.Y + normalized.Height));
        }
        if (result.Count == 0)
            diagnostics.Add(Warning("CANMIGNUTJSON036", $"Markup annotation '{sourceId}' contains no valid text rectangles."));
        return result.Count == 0 ? null : result;
    }

    private static string? ReadLineEnding(
        JsonElement source,
        string end,
        ICollection<NutrientViewerDiagnostic> diagnostics,
        string sourceId)
    {
        if (!source.TryGetProperty("lineCaps", out var caps) || caps.ValueKind != JsonValueKind.Object
            || ReadString(caps, end) is not { } cap) return null;
        var mapped = cap switch
        {
            "none" => "none",
            "openArrow" or "closedArrow" => "arrow",
            "circle" => "circle",
            "square" => "square",
            _ => null,
        };
        if (mapped is null)
            diagnostics.Add(Warning("CANMIGNUTJSON037", $"Line cap '{cap}' on annotation '{sourceId}' requires review."));
        return mapped;
    }

    private static PxaViewerAnnotationMetadata? ReadSafeMetadata(JsonElement source)
    {
        var subject = ReadString(source, "subject");
        var note = ReadString(source, "note");
        var name = ReadString(source, "name");
        return subject is null && note is null && name is null ? null : new(subject, note, name);
    }

    private static Dictionary<string, NutrientFormFieldMetadata> ReadFormFields(
        JsonElement root,
        ICollection<NutrientViewerDiagnostic> diagnostics)
    {
        var result = new Dictionary<string, NutrientFormFieldMetadata>(StringComparer.Ordinal);
        if (!root.TryGetProperty("formFields", out var fields)) return result;
        if (fields.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(Warning("CANMIGNUTJSON040", "The formFields property is not an array and was ignored."));
            return result;
        }

        foreach (var field in fields.EnumerateArray())
        {
            if (field.ValueKind != JsonValueKind.Object || ReadString(field, "name") is not { Length: > 0 } name) continue;
            var options = ReadOptions(field);
            var flags = ReadStringArray(field, "flags");
            if (field.TryGetProperty("additionalActions", out _))
                diagnostics.Add(Warning("CANMIGNUTJSON041", $"Form field '{name}' contains actions or validation logic that was not migrated."));
            result[name] = new NutrientFormFieldMetadata(
                ReadString(field, "type") ?? string.Empty,
                options,
                flags.Contains("required", StringComparer.Ordinal),
                flags.Contains("readOnly", StringComparer.Ordinal),
                ReadBool(field, "multiLine") ?? false,
                ReadJsonValue(field, "defaultValue") ?? ReadJsonValue(field, "defaultValues"));
        }
        return result;
    }

    private static IReadOnlyList<PxaViewerFormValue> ReadFormValues(
        JsonElement root,
        IReadOnlyDictionary<string, NutrientFormFieldMetadata> metadata,
        ICollection<NutrientViewerDiagnostic> diagnostics)
    {
        var result = new List<PxaViewerFormValue>();
        if (!root.TryGetProperty("formFieldValues", out var values)) return result;
        if (values.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(Warning("CANMIGNUTJSON042", "The formFieldValues property is not an array and was ignored."));
            return result;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in values.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || ReadString(item, "name") is not { Length: > 0 } name
                || ReadString(item, "type") is not "pspdfkit/form-field-value")
            {
                diagnostics.Add(Warning("CANMIGNUTJSON043", "An invalid form field value was omitted."));
                continue;
            }
            if (!names.Add(name))
            {
                diagnostics.Add(Warning("CANMIGNUTJSON044", $"Duplicate form value '{name}' was omitted."));
                continue;
            }
            if (!item.TryGetProperty("value", out var sourceValue) || !TryNormalizeFormValue(sourceValue, out var normalizedValue))
            {
                diagnostics.Add(Warning("CANMIGNUTJSON045", $"Form value '{name}' has an unsupported value type and was omitted."));
                continue;
            }

            metadata.TryGetValue(name, out var field);
            var kind = MapFormKind(field?.Type);
            if (kind is "signature" or "button")
            {
                diagnostics.Add(Warning("CANMIGNUTJSON046", $"{kind} field '{name}' was omitted; signatures and button actions are not migrated."));
                continue;
            }
            if (kind == "unsupported")
                diagnostics.Add(Warning("CANMIGNUTJSON047", $"Form value '{name}' has no supported form field metadata and requires review."));

            var value = ConvertFormValue(kind, normalizedValue, field, diagnostics, name);
            result.Add(new PxaViewerFormValue(
                name,
                kind,
                value,
                value,
                field?.Options ?? [],
                field?.MultiLine,
                field?.Required,
                field?.ReadOnly,
                field?.DefaultValue));
        }
        return result;
    }

    private static object? ConvertFormValue(
        string kind,
        object? value,
        NutrientFormFieldMetadata? field,
        ICollection<NutrientViewerDiagnostic> diagnostics,
        string name)
    {
        var list = value as IReadOnlyList<string>;
        return kind switch
        {
            "checkbox" => ConvertCheckboxValue(list, field, diagnostics, name),
            "radio" or "dropdown" => list?.FirstOrDefault() ?? value as string ?? string.Empty,
            "list" => list ?? (value is string text ? new[] { text } : Array.Empty<string>()),
            "text" => value as string ?? list?.FirstOrDefault() ?? string.Empty,
            _ => value,
        };
    }

    private static bool ConvertCheckboxValue(
        IReadOnlyList<string>? values,
        NutrientFormFieldMetadata? field,
        ICollection<NutrientViewerDiagnostic> diagnostics,
        string name)
    {
        if ((field?.Options.Count ?? 0) > 1 || (values?.Count ?? 0) > 1)
            diagnostics.Add(Warning("CANMIGNUTJSON048", $"Checkbox group '{name}' was reduced to a boolean value and requires review."));
        return values?.Any(value => !string.Equals(value, "Off", StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static string MapFormKind(string? type) => type switch
    {
        "pspdfkit/form-field/text" => "text",
        "pspdfkit/form-field/checkbox" => "checkbox",
        "pspdfkit/form-field/radio" => "radio",
        "pspdfkit/form-field/combobox" => "dropdown",
        "pspdfkit/form-field/listbox" => "list",
        "pspdfkit/form-field/signature" => "signature",
        "pspdfkit/form-field/button" => "button",
        _ => "unsupported",
    };

    private static bool TryNormalizeFormValue(JsonElement value, out object? result)
    {
        result = value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Array when value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String)
                => value.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray(),
            _ => MissingFormValue.Instance,
        };
        return result is not MissingFormValue;
    }

    private static IReadOnlyList<string> ReadOptions(JsonElement source)
    {
        if (!source.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Array) return [];
        return options.EnumerateArray()
            .Where(option => option.ValueKind == JsonValueKind.Object)
            .Select(option => ReadString(option, "value") ?? ReadString(option, "label"))
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement source, string property) =>
        source.TryGetProperty(property, out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!).ToArray()
            : [];

    private static object? ReadJsonValue(JsonElement source, string property) =>
        source.TryGetProperty(property, out var value) && TryNormalizeFormValue(value, out var result) ? result : null;

    private static string? ReadColor(JsonElement source, string property)
    {
        if (!source.TryGetProperty(property, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.String)
            return value.GetString();
        if (value.ValueKind != JsonValueKind.Array)
            return null;

        var channels = value.EnumerateArray().Select(ReadFiniteNumber).ToArray();
        if (channels.Length < 3 || channels.Take(3).Any(static channel => channel is null))
            return null;
        var rgb = channels.Take(3).Select(static channel => Math.Clamp((int)Math.Round(channel!.Value), 0, 255)).ToArray();
        return $"#{rgb[0]:X2}{rgb[1]:X2}{rgb[2]:X2}";
    }

    private static NutrientViewerDiagnostic Warning(string code, string message) => new(code, "Warning", message);

    private static string Sha256(string value) => System.Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private readonly record struct SourceBounds(double Left, double Top, double Width, double Height);
    private readonly record struct PercentageBounds(double X, double Y, double Width, double Height);
    private sealed record NutrientFormFieldMetadata(
        string Type,
        IReadOnlyList<string> Options,
        bool Required,
        bool ReadOnly,
        bool MultiLine,
        object? DefaultValue);
    private sealed class MissingFormValue
    {
        public static readonly MissingFormValue Instance = new();
    }
}

public sealed record NutrientPageGeometry(
    double Width,
    double Height,
    int Rotation = 0,
    double CropLeft = 0,
    double CropTop = 0);

public sealed record NutrientInstantJsonResult(
    string Provider,
    string ProviderKey,
    string DetectedProduct,
    string DocumentFidelity,
    string SourcePreservation,
    string AnnotationSidecarJson,
    string FormValuePayloadJson,
    IReadOnlyList<NutrientViewerDiagnostic> Diagnostics,
    IReadOnlyDictionary<string, string> IdRemap,
    string SourceChecksum,
    string NormalizedChecksum,
    string GeneratedChecksum,
    NutrientMigrationCapabilityReport CapabilityReport)
{
    public PdfTrustMigrationReport? PdfTrust { get; init; }
}

public sealed record NutrientViewerDiagnostic(string Code, string Severity, string Message);

public sealed record NutrientMigrationCapabilityReport(
    int Migrated,
    int Approximated,
    int Omitted,
    int ReviewRequired,
    int Unsupported)
{
    public static NutrientMigrationCapabilityReport FromDiagnostics(
        int migrated,
        IEnumerable<NutrientViewerDiagnostic> diagnostics)
    {
        var items = diagnostics.ToArray();
        var approximated = items.Count(item => item.Code is "CANMIGNUTWEB033" or "CANMIGNUTJSON020");
        var omitted = items.Count(item => item.Code is "CANMIGNUTWEB030" or "CANMIGNUTWEB031" or "CANMIGNUTWEB032"
            || item.Code is "CANMIGNUTJSON011" or "CANMIGNUTJSON030" or "CANMIGNUTJSON031" or "CANMIGNUTJSON033" or "CANMIGNUTJSON046");
        var unsupported = items.Count(item => item.Code is "CANMIGNUTJSON032" or "CANMIGNUTJSON034");
        var review = Math.Max(0, items.Count(item => string.Equals(item.Severity, "Warning", StringComparison.OrdinalIgnoreCase))
            - approximated - omitted - unsupported);
        return new(Math.Max(0, migrated), approximated, omitted, review, unsupported);
    }
}

public sealed record PxaViewerAnnotationSidecar(
    int Version,
    string? SourceName,
    DateTimeOffset ExportedAt,
    IReadOnlyList<PxaViewerAnnotation> Annotations);

public sealed record PxaViewerAnnotation(
    string Id,
    string Type,
    int PageNumber,
    double XPct,
    double YPct,
    double WidthPct,
    double HeightPct,
    string Text,
    string Author,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string Color,
    bool? Locked,
    double? Opacity,
    double? StrokeWidth,
    string? FillColor,
    string? LineEndingStart,
    string? LineEndingEnd,
    IReadOnlyList<PxaViewerInkPoint>? Points,
    IReadOnlyList<IReadOnlyList<PxaViewerInkPoint>>? PointLines,
    IReadOnlyList<PxaViewerMarkupQuad>? QuadPoints,
    PxaViewerAnnotationMetadata? Metadata);

public sealed record PxaViewerInkPoint(double XPct, double YPct);

public sealed record PxaViewerMarkupQuad(
    double X1Pct, double Y1Pct,
    double X2Pct, double Y2Pct,
    double X3Pct, double Y3Pct,
    double X4Pct, double Y4Pct);

public sealed record PxaViewerAnnotationMetadata(string? Subject, string? Note, string? SourceName);

public sealed record PxaViewerFormValuePayload(int Version, string? SourceName, IReadOnlyList<PxaViewerFormValue> Fields);

public sealed record PxaViewerFormValue(
    string Name,
    string Kind,
    object? Value,
    object? OriginalValue,
    IReadOnlyList<string> Options,
    bool? Multiline,
    bool? Required,
    bool? ReadOnly,
    object? DefaultValue);

public sealed class NutrientInstantJsonException : Exception
{
    public NutrientInstantJsonException(string code, string message, Exception? innerException = null)
        : base(message, innerException) => Code = code;

    public string Code { get; }
}
