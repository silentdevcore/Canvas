using Canvas.Importer;
using Canvas.Importer.Objects;
using Canvas.Importer.Parsing;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Canvas.WebApi.Services;

public sealed class PdfViewerFormExtractionService
{
    private const long MultilineFlag = 1 << 12;
    private const long RadioFlag = 1 << 15;
    private const long ComboFlag = 1 << 17;

    public async Task<PdfViewerFormFieldsResponse> ExtractAsync(
        Stream pdfStream,
        string? sourceName = null,
        CancellationToken cancellationToken = default)
    {
        var document = await new PdfImporter().LoadAsync(pdfStream, cancellationToken).ConfigureAwait(false);
        var resolver = new PdfObjectResolver(document.ObjectGraph);
        var fields = new List<PdfViewerFormFieldResponse>();

        if (resolver.TryResolve<PdfDictionary>(document.Catalog.Dictionary["AcroForm"], out var acroForm) &&
            resolver.TryResolve<PdfArray>(acroForm["Fields"], out var rootFields))
        {
            foreach (var fieldObject in rootFields.Items)
            {
                ExtractField(fieldObject, resolver, new InheritedFormField(), fields);
            }
        }

        return new PdfViewerFormFieldsResponse(sourceName, fields);
    }

    public async Task<byte[]> FillAsync(
        Stream pdfStream,
        JsonElement fields,
        bool flatten = false,
        CancellationToken cancellationToken = default)
    {
        if (flatten)
        {
            throw new NotSupportedException("Backend form flattening is not supported yet.");
        }

        if (fields.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("fields must be an array.", nameof(fields));
        }

        using var memory = new MemoryStream();
        await pdfStream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        var pdfBytes = memory.ToArray();
        var document = await new PdfImporter()
            .LoadAsync(new MemoryStream(pdfBytes), cancellationToken)
            .ConfigureAwait(false);
        var resolver = new PdfObjectResolver(document.ObjectGraph);
        var updatesByName = fields
            .EnumerateArray()
            .Select(PdfViewerFormFieldUpdate.FromJson)
            .Where(static update => update is not null)
            .Cast<PdfViewerFormFieldUpdate>()
            .ToDictionary(static update => update.Name, StringComparer.Ordinal);
        var appendedObjects = new List<(PdfObjectId Id, PdfObject Value)>();

        if (resolver.TryResolve<PdfDictionary>(document.Catalog.Dictionary["AcroForm"], out var acroForm) &&
            resolver.TryResolve<PdfArray>(acroForm["Fields"], out var rootFields))
        {
            foreach (var fieldObject in rootFields.Items)
            {
                FillField(fieldObject, resolver, new InheritedFormField(), updatesByName, appendedObjects);
            }
        }

        if (appendedObjects.Count == 0)
        {
            return pdfBytes;
        }

        return AppendIncrementalUpdate(pdfBytes, document.ObjectGraph.Trailer, appendedObjects);
    }

    private static void ExtractField(
        PdfObject fieldObject,
        PdfObjectResolver resolver,
        InheritedFormField inherited,
        List<PdfViewerFormFieldResponse> fields)
    {
        if (!resolver.TryResolve<PdfDictionary>(fieldObject, out var field))
        {
            return;
        }

        var current = inherited.Merge(field, resolver);
        if (resolver.TryResolve<PdfArray>(field["Kids"], out var kids))
        {
            foreach (var kid in kids.Items)
            {
                ExtractField(kid, resolver, current, fields);
            }

            if (field["FT"] is null || field["Subtype"] is not PdfName { Value: "Widget" })
            {
                return;
            }
        }

        if (ResolveName(current.FieldType, resolver) is not { } fieldType)
        {
            return;
        }

        var name = current.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var flags = ResolveInteger(current.Flags, resolver) ?? 0;
        var options = ResolveOptions(current.Options, resolver);
        var value = ResolveValue(current.Value, resolver);
        var kind = ResolveKind(fieldType, flags, options);
        var normalizedValue = NormalizeValue(kind, value);

        fields.Add(new PdfViewerFormFieldResponse(
            Name: name,
            Kind: kind,
            Value: normalizedValue,
            OriginalValue: normalizedValue,
            Options: options,
            Multiline: fieldType == "Tx" && HasFlag(flags, MultilineFlag)));
    }

    private static void FillField(
        PdfObject fieldObject,
        PdfObjectResolver resolver,
        InheritedFormField inherited,
        IReadOnlyDictionary<string, PdfViewerFormFieldUpdate> updatesByName,
        List<(PdfObjectId Id, PdfObject Value)> appendedObjects)
    {
        if (!resolver.TryResolve<PdfDictionary>(fieldObject, out var field))
        {
            return;
        }

        var current = inherited.Merge(field, resolver);
        if (resolver.TryResolve<PdfArray>(field["Kids"], out var kids))
        {
            foreach (var kid in kids.Items)
            {
                FillField(kid, resolver, current, updatesByName, appendedObjects);
            }

            if (field["FT"] is null || field["Subtype"] is not PdfName { Value: "Widget" })
            {
                return;
            }
        }

        if (ResolveName(current.FieldType, resolver) is not { } fieldType ||
            string.IsNullOrWhiteSpace(current.Name) ||
            !updatesByName.TryGetValue(current.Name, out var update) ||
            field.OriginalId is not { } objectId)
        {
            return;
        }

        var flags = ResolveInteger(current.Flags, resolver) ?? 0;
        var kind = ResolveKind(fieldType, flags, ResolveOptions(current.Options, resolver));
        var updatedField = new PdfDictionary(new Dictionary<string, PdfObject>(field.Values, StringComparer.Ordinal));
        ApplyValueUpdate(updatedField, kind, update.Value);
        appendedObjects.Add((objectId, updatedField));
    }

    private static void ApplyValueUpdate(PdfDictionary field, string kind, JsonElement value)
    {
        switch (kind)
        {
            case "checkbox":
                var checkboxValue = value.ValueKind == JsonValueKind.True ||
                    (value.ValueKind == JsonValueKind.String && value.GetString() is { } text && text != "Off" && text.Length > 0);
                var name = checkboxValue ? "Yes" : "Off";
                field["V"] = new PdfName(name);
                field["AS"] = new PdfName(name);
                break;
            case "list":
                field["V"] = value.ValueKind == JsonValueKind.Array
                    ? new PdfArray(value.EnumerateArray().Select(static item => new PdfString(Encoding.Latin1.GetBytes(item.GetString() ?? ""), false)))
                    : new PdfString(Encoding.Latin1.GetBytes(ReadStringValue(value)), false);
                break;
            case "radio":
            case "dropdown":
            case "text":
                field["V"] = new PdfString(Encoding.Latin1.GetBytes(ReadStringValue(value)), false);
                break;
        }
    }

    private static string ResolveKind(string fieldType, long flags, IReadOnlyList<string> options)
    {
        return fieldType switch
        {
            "Tx" => "text",
            "Btn" when HasFlag(flags, RadioFlag) => "radio",
            "Btn" => "checkbox",
            "Ch" when HasFlag(flags, ComboFlag) => "dropdown",
            "Ch" => "list",
            _ => "unsupported"
        };
    }

    private static object NormalizeValue(string kind, string value)
    {
        return kind switch
        {
            "checkbox" => !string.IsNullOrWhiteSpace(value) && value != "Off",
            "list" => string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : new[] { value },
            _ => value
        };
    }

    private static bool HasFlag(long flags, long flag) => (flags & flag) == flag;

    private static string? ResolveName(PdfObject? value, PdfObjectResolver resolver)
    {
        return value is null
            ? null
            : resolver.Resolve(value) is PdfName name
                ? name.Value
                : null;
    }

    private static long? ResolveInteger(PdfObject? value, PdfObjectResolver resolver)
    {
        return value is null
            ? null
            : resolver.Resolve(value) switch
            {
                PdfInteger integer => integer.Value,
                PdfNumber number => (long)number.Value,
                _ => null
            };
    }

    private static string? ResolveString(PdfObject? value, PdfObjectResolver resolver)
    {
        return value is null
            ? null
            : resolver.Resolve(value) switch
            {
                PdfString text => text.ToLatin1String(),
                PdfName name => name.Value,
                _ => null
            };
    }

    private static string ResolveValue(PdfObject? value, PdfObjectResolver resolver)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var resolved = resolver.Resolve(value);
        if (resolved is PdfArray array)
        {
            return array.Items
                .Select(item => ResolveString(item, resolver))
                .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? string.Empty;
        }

        return ResolveString(resolved, resolver) ?? string.Empty;
    }

    private static IReadOnlyList<string> ResolveOptions(PdfObject? value, PdfObjectResolver resolver)
    {
        if (value is null || resolver.Resolve(value) is not PdfArray array)
        {
            return [];
        }

        return array.Items
            .Select(item => ResolveOption(item, resolver))
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray();
    }

    private static string? ResolveOption(PdfObject item, PdfObjectResolver resolver)
    {
        var resolved = resolver.Resolve(item);
        if (resolved is PdfArray { Items.Count: > 0 } pair)
        {
            return ResolveString(pair.Items[^1], resolver);
        }

        return ResolveString(resolved, resolver);
    }

    private static string ReadStringValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => value.ToString(),
            _ => ""
        };
    }

    private static byte[] AppendIncrementalUpdate(byte[] basePdfBytes, PdfDictionary? trailer, IReadOnlyList<(PdfObjectId Id, PdfObject Value)> appendedObjects)
    {
        if (trailer is null || trailer["Root"] is null)
        {
            throw new InvalidOperationException("PDF trailer is missing /Root for incremental update.");
        }

        var existingStartXref = FindStartXrefOffset(basePdfBytes);
        using var stream = new MemoryStream();
        stream.Write(basePdfBytes, 0, basePdfBytes.Length);
        if (basePdfBytes.Length > 0 && basePdfBytes[^1] != (byte)'\n')
        {
            WriteAscii(stream, "\n");
        }

        var offsets = new SortedDictionary<PdfObjectId, long>(Comparer<PdfObjectId>.Create(static (left, right) =>
            left.Number != right.Number ? left.Number.CompareTo(right.Number) : left.Generation.CompareTo(right.Generation)));
        foreach (var appendedObject in appendedObjects
            .GroupBy(static entry => entry.Id)
            .Select(static group => group.Last())
            .OrderBy(static entry => entry.Id.Number)
            .ThenBy(static entry => entry.Id.Generation))
        {
            offsets[appendedObject.Id] = stream.Position;
            WriteIndirectObject(stream, appendedObject.Id, appendedObject.Value);
        }

        var xrefOffset = stream.Position;
        WriteAscii(stream, "xref\n");
        foreach (var subsection in GroupSubsections(offsets))
        {
            WriteAscii(stream, $"{subsection.Start} {subsection.Entries.Count}\n");
            foreach (var entry in subsection.Entries)
            {
                WriteAscii(stream, $"{entry.Offset:0000000000} {entry.Generation:00000} n \n");
            }
        }

        var size = Math.Max((trailer["Size"] as PdfInteger)?.Value ?? 0, offsets.Keys.Max(static id => (long)id.Number) + 1);
        var trailerDictionary = new PdfDictionary();
        trailerDictionary["Size"] = new PdfInteger(size);
        trailerDictionary["Root"] = trailer["Root"]!;
        if (trailer["Info"] is { } info)
        {
            trailerDictionary["Info"] = info;
        }

        if (trailer["ID"] is { } id)
        {
            trailerDictionary["ID"] = id;
        }

        trailerDictionary["Prev"] = new PdfInteger(existingStartXref);
        WriteAscii(stream, "trailer\n");
        WriteObject(stream, trailerDictionary);
        WriteAscii(stream, "\nstartxref\n");
        WriteAscii(stream, xrefOffset.ToString(CultureInfo.InvariantCulture));
        WriteAscii(stream, "\n%%EOF\n");
        return stream.ToArray();
    }

    private static long FindStartXrefOffset(ReadOnlySpan<byte> bytes)
    {
        var marker = Encoding.ASCII.GetBytes("startxref");
        for (var index = bytes.Length - marker.Length; index >= 0; index--)
        {
            if (!bytes[index..(index + marker.Length)].SequenceEqual(marker))
            {
                continue;
            }

            var tokenizer = new Canvas.Importer.Tokenizer.PdfTokenizer(bytes[(index + marker.Length)..]);
            var token = tokenizer.ReadToken();
            if (long.TryParse(token.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        throw new InvalidOperationException("Unable to find startxref in PDF bytes.");
    }

    private static IReadOnlyList<(int Start, List<(long Offset, int Generation)> Entries)> GroupSubsections(SortedDictionary<PdfObjectId, long> offsets)
    {
        var subsections = new List<(int Start, List<(long Offset, int Generation)> Entries)>();
        List<(long Offset, int Generation)>? currentEntries = null;
        var currentStart = -1;
        var expectedNumber = -1;

        foreach (var entry in offsets)
        {
            if (currentEntries is null || entry.Key.Number != expectedNumber)
            {
                currentEntries = [];
                currentStart = entry.Key.Number;
                subsections.Add((currentStart, currentEntries));
            }

            currentEntries.Add((entry.Value, entry.Key.Generation));
            expectedNumber = entry.Key.Number + 1;
        }

        return subsections;
    }

    private static void WriteIndirectObject(Stream stream, PdfObjectId id, PdfObject value)
    {
        WriteAscii(stream, $"{id.Number} {id.Generation} obj\n");
        WriteObject(stream, value);
        WriteAscii(stream, "\nendobj\n");
    }

    private static void WriteObject(Stream stream, PdfObject value)
    {
        switch (value)
        {
            case PdfName name:
                WriteAscii(stream, "/");
                WriteAscii(stream, name.Value);
                break;
            case PdfInteger integer:
                WriteAscii(stream, integer.Value.ToString(CultureInfo.InvariantCulture));
                break;
            case PdfNumber number:
                WriteAscii(stream, number.Value.ToString("0.###", CultureInfo.InvariantCulture));
                break;
            case PdfString text:
                WriteAscii(stream, "<");
                foreach (var current in text.GetDecodedBytes().Span)
                {
                    WriteAscii(stream, current.ToString("X2", CultureInfo.InvariantCulture));
                }

                WriteAscii(stream, ">");
                break;
            case PdfBoolean boolean:
                WriteAscii(stream, boolean.Value ? "true" : "false");
                break;
            case PdfNull:
                WriteAscii(stream, "null");
                break;
            case PdfReference reference:
                WriteAscii(stream, $"{reference.Id.Number} {reference.Id.Generation} R");
                break;
            case PdfArray array:
                WriteAscii(stream, "[");
                for (var index = 0; index < array.Items.Count; index++)
                {
                    if (index > 0)
                    {
                        WriteAscii(stream, " ");
                    }

                    WriteObject(stream, array.Items[index]);
                }

                WriteAscii(stream, "]");
                break;
            case PdfDictionary dictionary:
                WriteAscii(stream, "<<");
                foreach (var entry in dictionary.Values)
                {
                    WriteAscii(stream, " /");
                    WriteAscii(stream, entry.Key);
                    WriteAscii(stream, " ");
                    WriteObject(stream, entry.Value);
                }

                WriteAscii(stream, " >>");
                break;
            default:
                throw new NotSupportedException($"PDF object type '{value.GetType().Name}' cannot be serialized for form update.");
        }
    }

    private static void WriteAscii(Stream stream, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    private sealed record InheritedFormField(
        PdfObject? FieldType = null,
        PdfObject? NameObject = null,
        PdfObject? Value = null,
        PdfObject? Flags = null,
        PdfObject? Options = null)
    {
        public string? Name { get; private init; }

        public InheritedFormField Merge(PdfDictionary field, PdfObjectResolver resolver)
        {
            var nameObject = field["T"] ?? NameObject;
            return new InheritedFormField(
                field["FT"] ?? FieldType,
                nameObject,
                field["V"] ?? Value,
                field["Ff"] ?? Flags,
                field["Opt"] ?? Options)
            {
                Name = ResolveString(nameObject, resolver) ?? Name
            };
        }
    }

    private sealed record PdfViewerFormFieldUpdate(string Name, JsonElement Value)
    {
        public static PdfViewerFormFieldUpdate? FromJson(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty("name", out var nameElement) ||
                nameElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(nameElement.GetString()) ||
                !element.TryGetProperty("value", out var value))
            {
                return null;
            }

            return new PdfViewerFormFieldUpdate(nameElement.GetString()!, value.Clone());
        }
    }
}

public sealed record PdfViewerFormFieldsResponse(
    string? SourceName,
    IReadOnlyList<PdfViewerFormFieldResponse> Fields);

public sealed record PdfViewerFormFieldResponse(
    string Name,
    string Kind,
    object Value,
    object OriginalValue,
    IReadOnlyList<string> Options,
    bool Multiline);
