using Canvas.Importer;
using Canvas.Importer.Document;
using Canvas.Importer.Graphics;
using Canvas.Importer.Objects;
using Canvas.Importer.Parsing;

#pragma warning disable PXA0002 // WebApi implementation intentionally uses the compatibility importer engine.

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

        var quadPoints = IsMarkupType(viewerType)
            ? ReadQuadPoints(dictionary["QuadPoints"], pageBounds, resolver)
            : [];
        var bounds = quadPoints.Count > 0 ? BoundsFromQuadPoints(quadPoints) : null;
        var xPct = bounds?.XPct ?? ClampPercent(((rect.Left - pageBounds.Left) / pageBounds.Width) * 100d);
        var yPct = bounds?.YPct ?? ClampPercent(((pageBounds.Top - rect.Top) / pageBounds.Height) * 100d);
        var widthPct = bounds?.WidthPct ?? ClampPercent((rect.Width / pageBounds.Width) * 100d);
        var heightPct = bounds?.HeightPct ?? ClampPercent((rect.Height / pageBounds.Height) * 100d);
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
            Opacity: Math.Round(opacity, 2),
            QuadPoints: quadPoints);
    }

    private static bool IsMarkupType(string viewerType) => viewerType is "highlight" or "underline" or "strikeout";

    private static IReadOnlyList<PdfViewerMarkupQuadPointResponse> ReadQuadPoints(
        PdfObject? value,
        PdfRectangle pageBounds,
        PdfObjectResolver resolver)
    {
        if (value is null ||
            pageBounds.Width <= 0 ||
            pageBounds.Height <= 0 ||
            resolver.Resolve(value) is not PdfArray array ||
            array.Items.Count < 8)
        {
            return [];
        }

        var points = new List<PdfViewerMarkupQuadPointResponse>();
        for (var index = 0; index + 7 < array.Items.Count; index += 8)
        {
            var x1 = ReadNumber(array.Items[index], resolver);
            var y1 = ReadNumber(array.Items[index + 1], resolver);
            var x2 = ReadNumber(array.Items[index + 2], resolver);
            var y2 = ReadNumber(array.Items[index + 3], resolver);
            var x3 = ReadNumber(array.Items[index + 4], resolver);
            var y3 = ReadNumber(array.Items[index + 5], resolver);
            var x4 = ReadNumber(array.Items[index + 6], resolver);
            var y4 = ReadNumber(array.Items[index + 7], resolver);
            if (x1 is null || y1 is null || x2 is null || y2 is null || x3 is null || y3 is null || x4 is null || y4 is null)
            {
                continue;
            }

            points.Add(new PdfViewerMarkupQuadPointResponse(
                XPct(x1.Value, pageBounds),
                YPct(y1.Value, pageBounds),
                XPct(x2.Value, pageBounds),
                YPct(y2.Value, pageBounds),
                XPct(x3.Value, pageBounds),
                YPct(y3.Value, pageBounds),
                XPct(x4.Value, pageBounds),
                YPct(y4.Value, pageBounds)));
        }

        return points;
    }

    private static PdfViewerAnnotationBounds? BoundsFromQuadPoints(IReadOnlyList<PdfViewerMarkupQuadPointResponse> quadPoints)
    {
        if (quadPoints.Count == 0)
            return null;

        var xs = quadPoints.SelectMany(static point => new[] { point.X1Pct, point.X2Pct, point.X3Pct, point.X4Pct }).ToArray();
        var ys = quadPoints.SelectMany(static point => new[] { point.Y1Pct, point.Y2Pct, point.Y3Pct, point.Y4Pct }).ToArray();
        var left = ClampPercent(xs.Min());
        var right = ClampPercent(xs.Max());
        var top = ClampPercent(ys.Min());
        var bottom = ClampPercent(ys.Max());
        return new PdfViewerAnnotationBounds(
            Math.Round(left, 4),
            Math.Round(top, 4),
            Math.Round(Math.Max(0.0001, right - left), 4),
            Math.Round(Math.Max(0.0001, bottom - top), 4));
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

    private static double XPct(double x, PdfRectangle pageBounds) =>
        Math.Round(ClampPercent(((x - pageBounds.Left) / pageBounds.Width) * 100d), 4);

    private static double YPct(double y, PdfRectangle pageBounds) =>
        Math.Round(ClampPercent(((pageBounds.Top - y) / pageBounds.Height) * 100d), 4);

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
    double Opacity,
    IReadOnlyList<PdfViewerMarkupQuadPointResponse>? QuadPoints = null);

public sealed record PdfViewerMarkupQuadPointResponse(
    double X1Pct,
    double Y1Pct,
    double X2Pct,
    double Y2Pct,
    double X3Pct,
    double Y3Pct,
    double X4Pct,
    double Y4Pct);

internal sealed record PdfViewerAnnotationBounds(
    double XPct,
    double YPct,
    double WidthPct,
    double HeightPct);

#pragma warning restore PXA0002
