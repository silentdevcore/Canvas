using Canvas.Importer;
using Canvas.Importer.Document;
using Canvas.Importer.Graphics;
using Canvas.Importer.Objects;
using Canvas.Importer.Parsing;

namespace Canvas.WebApi.Services;

public sealed class PdfViewerNativeAnnotationExtractionService
{
    public async Task<PdfViewerAnnotationSidecarResponse> ExtractAsync(
        Stream pdfStream,
        string? sourceName = null,
        CancellationToken cancellationToken = default)
    {
        var document = await new PdfImporter().LoadAsync(pdfStream, cancellationToken).ConfigureAwait(false);
        var resolver = new PdfObjectResolver(document.ObjectGraph);
        var annotations = new List<PdfViewerAnnotationResponse>();

        for (var pageIndex = 0; pageIndex < document.Pages.Count; pageIndex++)
        {
            var page = document.Pages[pageIndex];
            foreach (var annotation in ExtractPageAnnotations(page, pageIndex + 1, resolver))
            {
                annotations.Add(annotation);
            }
        }

        return new PdfViewerAnnotationSidecarResponse(
            Version: 1,
            SourceName: sourceName,
            ExportedAt: DateTimeOffset.UtcNow,
            Annotations: annotations);
    }

    private static IEnumerable<PdfViewerAnnotationResponse> ExtractPageAnnotations(
        PdfPageModel page,
        int pageNumber,
        PdfObjectResolver resolver)
    {
        if (!resolver.TryResolve<PdfArray>(page.PageDictionary["Annots"], out var annots))
        {
            yield break;
        }

        var pageBounds = page.CropBox ?? page.MediaBox ?? new PdfRectangle(0, 0, 595, 842);
        var index = 0;
        foreach (var item in annots.Items)
        {
            index++;
            if (!resolver.TryResolve<PdfDictionary>(item, out var dictionary))
            {
                continue;
            }

            var annotation = ConvertAnnotation(dictionary, pageNumber, index, pageBounds, resolver);
            if (annotation is not null)
            {
                yield return annotation;
            }
        }
    }

    private static PdfViewerAnnotationResponse? ConvertAnnotation(
        PdfDictionary dictionary,
        int pageNumber,
        int index,
        PdfRectangle pageBounds,
        PdfObjectResolver resolver)
    {
        var type = ResolveSubtype(dictionary["Subtype"], resolver);
        var viewerType = type switch
        {
            "Text" => "note",
            "FreeText" => "freeText",
            "Highlight" => "highlight",
            "Underline" => "underline",
            "StrikeOut" => "strikeout",
            "Square" => "rectangle",
            "Circle" => "circle",
            "Redact" => "redaction",
            _ => null
        };

        if (viewerType is null ||
            !TryReadRectangle(dictionary["Rect"], resolver, out var rect) ||
            pageBounds.Width <= 0 ||
            pageBounds.Height <= 0)
        {
            return null;
        }

        var xPct = ClampPercent(((rect.Left - pageBounds.Left) / pageBounds.Width) * 100d);
        var yPct = ClampPercent(((pageBounds.Top - rect.Top) / pageBounds.Height) * 100d);
        var widthPct = ClampPercent((rect.Width / pageBounds.Width) * 100d);
        var heightPct = ClampPercent((rect.Height / pageBounds.Height) * 100d);
        var color = TryReadColor(dictionary["C"], resolver) ?? DefaultColor(viewerType);
        var opacity = ReadNumber(dictionary["CA"], resolver) is { } alpha
            ? Math.Clamp(alpha * 100d, 10d, 100d)
            : 100d;
        var text = ReadString(dictionary["Contents"], resolver) ?? "";
        var author = ReadString(dictionary["T"], resolver) ?? "PDF";

        return new PdfViewerAnnotationResponse(
            Id: $"native-{pageNumber}-{index}",
            Type: viewerType,
            PageNumber: pageNumber,
            XPct: Math.Round(xPct, 4),
            YPct: Math.Round(yPct, 4),
            WidthPct: Math.Round(widthPct, 4),
            HeightPct: Math.Round(heightPct, 4),
            Text: text,
            Author: author,
            CreatedAt: DateTimeOffset.UtcNow,
            Color: color,
            Locked: false,
            Opacity: Math.Round(opacity, 2));
    }

    private static string? ResolveSubtype(PdfObject? value, PdfObjectResolver resolver)
    {
        return resolver.Resolve(value ?? PdfNull.Value) is PdfName name ? name.Value : null;
    }

    private static bool TryReadRectangle(PdfObject? value, PdfObjectResolver resolver, out PdfRectangle rectangle)
    {
        rectangle = default;
        if (value is null || resolver.Resolve(value) is not PdfArray { Items.Count: >= 4 } array)
        {
            return false;
        }

        var x1 = ReadNumber(array.Items[0], resolver);
        var y1 = ReadNumber(array.Items[1], resolver);
        var x2 = ReadNumber(array.Items[2], resolver);
        var y2 = ReadNumber(array.Items[3], resolver);
        if (x1 is null || y1 is null || x2 is null || y2 is null)
        {
            return false;
        }

        rectangle = new PdfRectangle(x1.Value, y1.Value, x2.Value - x1.Value, y2.Value - y1.Value);
        return true;
    }

    private static double? ReadNumber(PdfObject? value, PdfObjectResolver resolver)
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

    private static string? ReadString(PdfObject? value, PdfObjectResolver resolver)
    {
        return value is null
            ? null
            : resolver.Resolve(value) is PdfString text
                ? text.ToLatin1String()
                : null;
    }

    private static string? TryReadColor(PdfObject? value, PdfObjectResolver resolver)
    {
        if (value is null || resolver.Resolve(value) is not PdfArray { Items.Count: >= 3 } array)
        {
            return null;
        }

        var red = ReadNumber(array.Items[0], resolver);
        var green = ReadNumber(array.Items[1], resolver);
        var blue = ReadNumber(array.Items[2], resolver);
        if (red is null || green is null || blue is null)
        {
            return null;
        }

        return $"#{ToHex(red.Value)}{ToHex(green.Value)}{ToHex(blue.Value)}";
    }

    private static string ToHex(double component)
    {
        var value = (int)Math.Round(Math.Clamp(component, 0d, 1d) * 255d);
        return value.ToString("X2", System.Globalization.CultureInfo.InvariantCulture).ToLowerInvariant();
    }

    private static double ClampPercent(double value) => Math.Clamp(value, 0d, 100d);

    private static string DefaultColor(string viewerType)
    {
        return viewerType switch
        {
            "highlight" => "#fef08a",
            "underline" => "#2563eb",
            "strikeout" => "#dc2626",
            "redaction" => "#111827",
            _ => "#facc15"
        };
    }
}

public sealed record PdfViewerAnnotationSidecarResponse(
    int Version,
    string? SourceName,
    DateTimeOffset ExportedAt,
    IReadOnlyList<PdfViewerAnnotationResponse> Annotations);

public sealed record PdfViewerAnnotationResponse(
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
    string Color,
    bool Locked,
    double Opacity);
