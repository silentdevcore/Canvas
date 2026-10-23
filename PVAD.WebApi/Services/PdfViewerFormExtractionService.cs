using PVAD.Importer;
using PVAD.Importer.Document;
using PVAD.Importer.Graphics;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Saving;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PVAD.WebApi.Services;

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
        var flattenCommands = new List<FormFlattenCommand>();
        var flattenedWidgetIds = new HashSet<PdfObjectId>();

        if (resolver.TryResolve<PdfDictionary>(document.Catalog.Dictionary["AcroForm"], out var acroForm) &&
            resolver.TryResolve<PdfArray>(acroForm["Fields"], out var rootFields))
        {
            foreach (var fieldObject in rootFields.Items)
            {
                FillField(fieldObject, resolver, new InheritedFormField(), updatesByName, appendedObjects, flatten ? flattenCommands : null, flattenedWidgetIds);
            }
        }

        if (flatten && flattenCommands.Count > 0)
        {
            AppendFlattenObjects(document, resolver, flattenCommands, flattenedWidgetIds, appendedObjects);
        }

        if (appendedObjects.Count == 0)
        {
            return pdfBytes;
        }

        return new PdfIncrementalUpdateWriter().Append(
            pdfBytes,
            appendedObjects.Select(static update => new PdfIncrementalObjectUpdate(update.Id, update.Value))).Bytes;
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
        List<(PdfObjectId Id, PdfObject Value)> appendedObjects,
        List<FormFlattenCommand>? flattenCommands,
        HashSet<PdfObjectId> flattenedWidgetIds)
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
                FillField(kid, resolver, current, updatesByName, appendedObjects, flattenCommands, flattenedWidgetIds);
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

        if (flattenCommands is not null &&
            TryReadRectangle(field["Rect"], resolver, out var rect) &&
            ResolvePageObjectId(field["P"], resolver) is { } pageObjectId)
        {
            flattenCommands.Add(new FormFlattenCommand(pageObjectId, rect, kind, update.Value.Clone()));
            flattenedWidgetIds.Add(objectId);
        }
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

    private static void AppendFlattenObjects(
        PdfDocumentModel document,
        PdfObjectResolver resolver,
        IReadOnlyList<FormFlattenCommand> commands,
        IReadOnlySet<PdfObjectId> widgetIds,
        List<(PdfObjectId Id, PdfObject Value)> appendedObjects)
    {
        var nextObjectNumber = document.ObjectGraph.Objects.Keys.Select(static id => id.Number).DefaultIfEmpty().Max() + 1;
        var fontObjectId = new PdfObjectId(nextObjectNumber++, 0);
        appendedObjects.Add((fontObjectId, new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Type"] = new PdfName("Font"),
            ["Subtype"] = new PdfName("Type1"),
            ["BaseFont"] = new PdfName("Helvetica"),
            ["Encoding"] = new PdfName("WinAnsiEncoding")
        })));

        foreach (var pageGroup in commands.GroupBy(static command => command.PageObjectId))
        {
            var page = document.Pages.FirstOrDefault(candidate => candidate.OriginalReference == pageGroup.Key);
            if (page is null)
            {
                continue;
            }

            var streamObjectId = new PdfObjectId(nextObjectNumber++, 0);
            var contentBytes = Encoding.ASCII.GetBytes(BuildFlattenContentStream(pageGroup));
            appendedObjects.Add((streamObjectId, new PdfStreamObject(new PdfDictionary(), contentBytes)));

            var updatedPage = new PdfDictionary(new Dictionary<string, PdfObject>(page.PageDictionary.Values, StringComparer.Ordinal));
            updatedPage["Contents"] = AppendContentReference(page.PageDictionary["Contents"], streamObjectId);
            updatedPage["Resources"] = BuildFlattenResources(page, resolver, fontObjectId);
            updatedPage["Annots"] = RemoveWidgetAnnotations(page.PageDictionary["Annots"], resolver, widgetIds);
            appendedObjects.Add((pageGroup.Key, updatedPage));
        }

        if (document.Catalog.OriginalReference is { } catalogObjectId)
        {
            var updatedCatalog = new PdfDictionary(new Dictionary<string, PdfObject>(document.Catalog.Dictionary.Values, StringComparer.Ordinal));
            var acroForm = resolver.TryResolve<PdfDictionary>(document.Catalog.Dictionary["AcroForm"], out var existingAcroForm)
                ? new PdfDictionary(new Dictionary<string, PdfObject>(existingAcroForm.Values, StringComparer.Ordinal))
                : new PdfDictionary();
            acroForm["Fields"] = new PdfArray();
            updatedCatalog["AcroForm"] = acroForm;
            appendedObjects.Add((catalogObjectId, updatedCatalog));
        }
    }

    private static string BuildFlattenContentStream(IEnumerable<FormFlattenCommand> commands)
    {
        var builder = new StringBuilder();
        foreach (var command in commands)
        {
            builder.AppendLine("q");
            switch (command.Kind)
            {
                case "checkbox":
                    AppendCheckboxFlattenContent(builder, command);
                    break;
                case "text":
                case "dropdown":
                case "radio":
                case "list":
                    AppendTextFlattenContent(builder, command);
                    break;
            }

            builder.AppendLine("Q");
        }

        return builder.ToString();
    }

    private static void AppendTextFlattenContent(StringBuilder builder, FormFlattenCommand command)
    {
        var text = command.Kind == "list" && command.Value.ValueKind == JsonValueKind.Array
            ? string.Join(", ", command.Value.EnumerateArray().Select(ReadStringValue))
            : ReadStringValue(command.Value);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var startX = command.Rect.Left + 2;
        var startY = Math.Max(command.Rect.Bottom + 2, command.Rect.Top - 12);
        builder.AppendLine("BT");
        builder.AppendLine("/Fflat 10 Tf");
        builder.AppendLine("0 0 0 rg");
        builder.Append(CultureInfo.InvariantCulture, $"{FormatNumber(startX)} {FormatNumber(startY)} Td\n");
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
            {
                builder.AppendLine("0 -12 Td");
            }

            builder.Append(CultureInfo.InvariantCulture, $"({EscapeLiteralString(lines[index])}) Tj\n");
        }

        builder.AppendLine("ET");
    }

    private static void AppendCheckboxFlattenContent(StringBuilder builder, FormFlattenCommand command)
    {
        var checkedValue = command.Value.ValueKind == JsonValueKind.True ||
            (command.Value.ValueKind == JsonValueKind.String && command.Value.GetString() is { } text && text != "Off" && text.Length > 0);
        builder.AppendLine("0 0 0 RG");
        builder.AppendLine("1 w");
        builder.Append(CultureInfo.InvariantCulture, $"{FormatNumber(command.Rect.Left)} {FormatNumber(command.Rect.Bottom)} {FormatNumber(command.Rect.Width)} {FormatNumber(command.Rect.Height)} re S\n");
        if (!checkedValue)
        {
            return;
        }

        var x1 = command.Rect.Left + command.Rect.Width * 0.2;
        var y1 = command.Rect.Bottom + command.Rect.Height * 0.55;
        var x2 = command.Rect.Left + command.Rect.Width * 0.42;
        var y2 = command.Rect.Bottom + command.Rect.Height * 0.25;
        var x3 = command.Rect.Left + command.Rect.Width * 0.82;
        var y3 = command.Rect.Bottom + command.Rect.Height * 0.78;
        builder.AppendLine("2 w");
        builder.Append(CultureInfo.InvariantCulture, $"{FormatNumber(x1)} {FormatNumber(y1)} m {FormatNumber(x2)} {FormatNumber(y2)} l {FormatNumber(x3)} {FormatNumber(y3)} l S\n");
    }

    private static PdfDictionary BuildFlattenResources(PdfPageModel page, PdfObjectResolver resolver, PdfObjectId fontObjectId)
    {
        var resources = resolver.TryResolve<PdfDictionary>(page.PageDictionary["Resources"], out var pageResources)
            ? new PdfDictionary(new Dictionary<string, PdfObject>(pageResources.Values, StringComparer.Ordinal))
            : new PdfDictionary();
        var fonts = resolver.TryResolve<PdfDictionary>(resources["Font"], out var existingFonts)
            ? new PdfDictionary(new Dictionary<string, PdfObject>(existingFonts.Values, StringComparer.Ordinal))
            : new PdfDictionary();
        fonts["Fflat"] = new PdfReference(fontObjectId);
        resources["Font"] = fonts;
        return resources;
    }

    private static PdfObject AppendContentReference(PdfObject? existingContents, PdfObjectId streamObjectId)
    {
        var streamReference = new PdfReference(streamObjectId);
        return existingContents switch
        {
            PdfArray array => new PdfArray(array.Items.Concat([streamReference])),
            PdfObject contents => new PdfArray([contents, streamReference]),
            null => streamReference
        };
    }

    private static PdfObject RemoveWidgetAnnotations(PdfObject? annots, PdfObjectResolver resolver, IReadOnlySet<PdfObjectId> widgetIds)
    {
        if (annots is null || resolver.Resolve(annots) is not PdfArray array)
        {
            return new PdfArray();
        }

        return new PdfArray(array.Items.Where(item => item is not PdfReference reference || !widgetIds.Contains(reference.Id)));
    }

    private static PdfObjectId? ResolvePageObjectId(PdfObject? value, PdfObjectResolver resolver)
    {
        return value switch
        {
            PdfReference reference => reference.Id,
            _ => resolver.Resolve(value ?? PdfNull.Value)?.OriginalId
        };
    }

    private static bool TryReadRectangle(PdfObject? value, PdfObjectResolver resolver, out PdfRectangle rectangle)
    {
        rectangle = default;
        if (value is null || resolver.Resolve(value) is not PdfArray { Items.Count: >= 4 } array)
        {
            return false;
        }

        var x1 = ResolveNumber(array.Items[0], resolver);
        var y1 = ResolveNumber(array.Items[1], resolver);
        var x2 = ResolveNumber(array.Items[2], resolver);
        var y2 = ResolveNumber(array.Items[3], resolver);
        if (x1 is null || y1 is null || x2 is null || y2 is null)
        {
            return false;
        }

        rectangle = new PdfRectangle(x1.Value, y1.Value, x2.Value - x1.Value, y2.Value - y1.Value);
        return true;
    }

    private static double? ResolveNumber(PdfObject? value, PdfObjectResolver resolver)
    {
        return value is null
            ? null
            : resolver.Resolve(value) switch
            {
                PdfInteger integer => integer.Value,
                PdfNumber number => number.Value,
                _ => null
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

    private static string EscapeLiteralString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal);
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
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

    private sealed record FormFlattenCommand(PdfObjectId PageObjectId, PdfRectangle Rect, string Kind, JsonElement Value);
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
