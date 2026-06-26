using Canvas.Importer;
using Canvas.Importer.Objects;
using Canvas.Importer.Parsing;

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
