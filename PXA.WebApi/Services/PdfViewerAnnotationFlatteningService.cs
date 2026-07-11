using System.Text.Json;
using Canvas.Pdf;
using Canvas.Importer;
using Canvas.Importer.Analysis;
using Canvas.Importer.Document;
using Canvas.Importer.Generation;
using Canvas.Importer.Graphics;
using CanvasPdfColor = Canvas.Pdf.PdfColor;
using CanvasPdfDocument = Canvas.Pdf.PdfDocument;

#pragma warning disable PXA0002 // Flattening still uses the Canvas regenerator until the PDF engine boundary is renamed.

namespace PXA.WebApi.Services;

public sealed class PdfViewerAnnotationFlatteningService
{
    private readonly CanvasPdfGeneratorBridge _bridge = new();
    private readonly BoundingBoxCalculator _bounds = new();

    public async Task<byte[]> FlattenAsync(
        Stream pdfStream,
        JsonElement annotations,
        CancellationToken cancellationToken = default)
    {
        if (annotations.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("annotations must be an array.", nameof(annotations));

        var sidecarAnnotations = annotations
            .EnumerateArray()
            .Select(PdfViewerAnnotation.FromJson)
            .Where(static annotation => annotation is not null)
            .Cast<PdfViewerAnnotation>()
            .ToArray();

        var document = await new PdfImporter().LoadAsync(pdfStream, cancellationToken).ConfigureAwait(false);
        await using var output = new MemoryStream();
        await _bridge.RegenerateAsync(
            document,
            output,
            canvasDocument => ApplyAnnotations(canvasDocument, sidecarAnnotations),
            cancellationToken).ConfigureAwait(false);

        return output.ToArray();
    }

    public async Task<byte[]> RedactAsync(
        Stream pdfStream,
        JsonElement annotations,
        CancellationToken cancellationToken = default)
    {
        if (annotations.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("annotations must be an array.", nameof(annotations));

        var redactions = annotations
            .EnumerateArray()
            .Select(PdfViewerAnnotation.FromJson)
            .Where(static annotation => annotation?.Type == "redaction")
            .Cast<PdfViewerAnnotation>()
            .ToArray();

        if (redactions.Length == 0)
            throw new ArgumentException("At least one redaction annotation is required.", nameof(annotations));

        var document = await new PdfImporter().LoadAsync(pdfStream, cancellationToken).ConfigureAwait(false);
        ApplyRedactionsToModel(document, redactions);

        await using var output = new MemoryStream();
        await _bridge.RegenerateAsync(
            document,
            output,
            canvasDocument =>
            {
                ApplyAnnotations(canvasDocument, redactions);
                ApplyRedactionAuditMetadata(canvasDocument, redactions);
            },
            cancellationToken).ConfigureAwait(false);

        return output.ToArray();
    }

    public async Task<byte[]> EmbedNativeAnnotationsAsync(
        Stream pdfStream,
        JsonElement annotations,
        CancellationToken cancellationToken = default)
    {
        if (annotations.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("annotations must be an array.", nameof(annotations));

        var sidecarAnnotations = annotations
            .EnumerateArray()
            .Select(PdfViewerAnnotation.FromJson)
            .Where(static annotation => annotation is not null)
            .Cast<PdfViewerAnnotation>()
            .Where(static annotation => annotation.Type is "note" or "freeText" or "stamp" or "highlight" or "underline" or "strikeout" or "rectangle" or "circle" or "redaction")
            .ToArray();

        if (sidecarAnnotations.Length == 0)
            throw new ArgumentException("At least one supported annotation is required.", nameof(annotations));

        var document = await new PdfImporter().LoadAsync(pdfStream, cancellationToken).ConfigureAwait(false);
        await using var output = new MemoryStream();
        await _bridge.RegenerateAsync(
            document,
            output,
            canvasDocument => ApplyNativeAnnotations(canvasDocument, sidecarAnnotations),
            cancellationToken).ConfigureAwait(false);

        return output.ToArray();
    }

    private void ApplyRedactionsToModel(PdfDocumentModel document, IReadOnlyCollection<PdfViewerAnnotation> redactions)
    {
        foreach (var pageWithRedactions in redactions.GroupBy(static item => item.PageNumber))
        {
            var page = document.Pages.ElementAtOrDefault(pageWithRedactions.Key - 1);
            if (page is null)
                continue;

            var redactionBounds = pageWithRedactions
                .Select(annotation => ToImporterRectangle(page, annotation))
                .ToArray();

            foreach (var element in page.GraphicsObjects)
            {
                RedactElement(element, redactionBounds);
            }
        }
    }

    private void RedactElement(PdfGraphicsElement element, IReadOnlyCollection<PdfRectangle> redactionBounds)
    {
        if (element.IsDeleted)
            return;

        if (element is PdfGroupElement group)
        {
            foreach (var child in group.Children)
            {
                RedactElement(child, redactionBounds);
            }

            if (group.Children.Count > 0 && group.Children.All(static child => child.IsDeleted))
            {
                group.IsDeleted = true;
            }

            return;
        }

        var bounds = element.Bounds ?? _bounds.ComputeElementBounds(element);
        if (redactionBounds.Any(redaction => redaction.Intersects(bounds)))
        {
            element.IsDeleted = true;
        }
    }

    private static void ApplyAnnotations(CanvasPdfDocument document, IReadOnlyCollection<PdfViewerAnnotation> annotations)
    {
        foreach (var annotation in annotations.OrderBy(static item => item.PageNumber))
        {
            var page = document.Pages.ElementAtOrDefault(annotation.PageNumber - 1);
            if (page is null)
                continue;

            DrawAnnotation(page, annotation);
        }
    }

    private static void ApplyRedactionAuditMetadata(CanvasPdfDocument document, IReadOnlyCollection<PdfViewerAnnotation> redactions)
    {
        var audit = new
        {
            version = 1,
            generatedAt = DateTimeOffset.UtcNow,
            count = redactions.Count,
            items = redactions
                .OrderBy(static item => item.PageNumber)
                .ThenBy(static item => item.YPct)
                .ThenBy(static item => item.XPct)
                .Select(static item => new
                {
                    pageNumber = item.PageNumber,
                    xPct = Math.Round(item.XPct, 3),
                    yPct = Math.Round(item.YPct, 3),
                    widthPct = Math.Round(item.WidthPct, 3),
                    heightPct = Math.Round(item.HeightPct, 3),
                    reason = item.RedactionReason,
                    author = item.Author,
                    createdAt = item.CreatedAt,
                }),
        };

        document.Info.CustomProperties["RedactionAudit"] = JsonSerializer.Serialize(audit);
        document.Info.Keywords = MergeKeywords(document.Info.Keywords, "redacted", "redaction-audit");
        document.Info.ModificationDate = DateTimeOffset.UtcNow;
    }

    private static string MergeKeywords(string? current, params string[] additions)
    {
        var keywords = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var keyword in (current ?? string.Empty).Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            keywords.Add(keyword);
        }

        foreach (var addition in additions)
        {
            keywords.Add(addition);
        }

        return string.Join("; ", keywords);
    }

    private static void DrawAnnotation(PdfPage page, PdfViewerAnnotation annotation)
    {
        var x = Percent(annotation.XPct, page.Width);
        var topY = Percent(annotation.YPct, page.Height);
        var width = Math.Max(1, Percent(annotation.WidthPct, page.Width));
        var height = Math.Max(1, Percent(annotation.HeightPct, page.Height));
        var color = ParseColor(annotation.Color);
        var strokeWidth = Math.Clamp(annotation.StrokeWidth, 1, 16);

        switch (annotation.Type)
        {
            case "highlight":
                if (DrawMarkupQuads(page, annotation, color))
                    break;

                page.DrawRectangleFromTop(x, topY, width, height, lineWidth: 0.1, fill: true, strokeColor: color, fillColor: Soften(color, annotation.Opacity));
                break;
            case "redaction":
                page.DrawRectangleFromTop(x, topY, width, height, lineWidth: strokeWidth, fill: true, strokeColor: CanvasPdfColor.Black, fillColor: CanvasPdfColor.Black);
                break;
            case "underline":
                if (DrawMarkupQuads(page, annotation, color))
                    break;

                page.DrawLineFromTop(x, topY + height - 1.5, x + width, topY + height - 1.5, lineWidth: strokeWidth, strokeColor: color);
                break;
            case "strikeout":
                if (DrawMarkupQuads(page, annotation, color))
                    break;

                page.DrawLineFromTop(x, topY + height / 2, x + width, topY + height / 2, lineWidth: strokeWidth, strokeColor: color);
                break;
            case "line":
                page.DrawLineFromTop(x, topY, x + width, topY + height, lineWidth: strokeWidth, strokeColor: color);
                DrawLineEnding(page, annotation.LineEndingStart, x, topY, x + width, topY + height, strokeWidth, color);
                DrawLineEnding(page, annotation.LineEndingEnd, x + width, topY + height, x, topY, strokeWidth, color);
                break;
            case "rectangle":
                page.DrawRectangleFromTop(
                    x,
                    topY,
                    width,
                    height,
                    lineWidth: strokeWidth,
                    fill: annotation.FillEnabled,
                    strokeColor: color,
                    fillColor: ParseColor(annotation.FillColor));
                break;
            case "circle":
                var radius = Math.Max(1, Math.Min(width, height) / 2);
                page.DrawCircleFromTop(
                    x + width / 2,
                    topY + height / 2,
                    radius,
                    lineWidth: strokeWidth,
                    fill: annotation.FillEnabled,
                    strokeColor: color,
                    fillColor: ParseColor(annotation.FillColor));
                break;
            case "ink":
                DrawInk(page, annotation, color);
                break;
            case "stamp":
                DrawStamp(page, annotation, x, topY, width, height, color);
                break;
            case "image":
                DrawImage(page, annotation, x, topY, width, height);
                break;
            case "note":
                DrawNote(page, annotation, x, topY, width, height, color);
                break;
            case "freeText":
                DrawFreeText(page, annotation, x, topY, width, height, color);
                break;
        }
    }

    private static void ApplyNativeAnnotations(CanvasPdfDocument document, IReadOnlyCollection<PdfViewerAnnotation> annotations)
    {
        foreach (var annotation in annotations.OrderBy(static item => item.PageNumber))
        {
            var page = document.Pages.ElementAtOrDefault(annotation.PageNumber - 1);
            if (page is null)
                continue;

            AddNativeAnnotation(page, annotation);
        }
    }

    private static void AddNativeAnnotation(PdfPage page, PdfViewerAnnotation annotation)
    {
        var x = Percent(annotation.XPct, page.Width);
        var topY = Percent(annotation.YPct, page.Height);
        var width = Math.Max(1, Percent(annotation.WidthPct, page.Width));
        var height = Math.Max(1, Percent(annotation.HeightPct, page.Height));
        var y = page.Height - topY - height;
        var color = ParseColor(annotation.Color);
        var opacity = Math.Clamp(annotation.Opacity, 10, 100) / 100d;

        switch (annotation.Type)
        {
            case "note":
                page.AddStickyNoteAnnotation(x, y, width, height, annotation.Text, color, opacity);
                break;
            case "freeText":
            case "stamp":
                page.AddFreeTextAnnotation(x, y, width, height, annotation.Text, color, opacity);
                break;
            case "highlight":
                page.AddHighlightAnnotation(x, y, width, height, annotation.Text, color, opacity, ToPdfMarkupQuads(page, annotation));
                break;
            case "underline":
                page.AddUnderlineAnnotation(x, y, width, height, annotation.Text, color, opacity, ToPdfMarkupQuads(page, annotation));
                break;
            case "strikeout":
                page.AddStrikeOutAnnotation(x, y, width, height, annotation.Text, color, opacity, ToPdfMarkupQuads(page, annotation));
                break;
            case "rectangle":
                page.AddSquareAnnotation(x, y, width, height, annotation.Text, color, opacity);
                break;
            case "circle":
                page.AddCircleAnnotation(x, y, width, height, annotation.Text, color, opacity);
                break;
            case "redaction":
                page.AddRedactionAnnotation(x, y, width, height, annotation.Text, color, opacity);
                break;
        }
    }

    private static void DrawInk(PdfPage page, PdfViewerAnnotation annotation, CanvasPdfColor color)
    {
        if (annotation.Points.Count < 2)
            return;

        for (var index = 1; index < annotation.Points.Count; index++)
        {
            var previous = annotation.Points[index - 1];
            var current = annotation.Points[index];
            page.DrawLineFromTop(
                Percent(previous.XPct, page.Width),
                Percent(previous.YPct, page.Height),
                Percent(current.XPct, page.Width),
                Percent(current.YPct, page.Height),
                lineWidth: Math.Clamp(annotation.StrokeWidth, 1, 16),
                strokeColor: color);
        }
    }

    private static bool DrawMarkupQuads(PdfPage page, PdfViewerAnnotation annotation, CanvasPdfColor color)
    {
        if (annotation.QuadPoints.Count == 0)
            return false;

        var strokeWidth = Math.Clamp(annotation.StrokeWidth, 1, 16);
        foreach (var quad in annotation.QuadPoints)
        {
            var left = Percent(Math.Min(quad.X1Pct, quad.X3Pct), page.Width);
            var right = Percent(Math.Max(quad.X2Pct, quad.X4Pct), page.Width);
            var top = Percent(Math.Min(quad.Y1Pct, quad.Y2Pct), page.Height);
            var bottom = Percent(Math.Max(quad.Y3Pct, quad.Y4Pct), page.Height);
            var width = Math.Max(1, right - left);
            var height = Math.Max(1, bottom - top);

            if (annotation.Type == "highlight")
            {
                page.DrawRectangleFromTop(left, top, width, height, lineWidth: 0.1, fill: true, strokeColor: color, fillColor: Soften(color, annotation.Opacity));
                continue;
            }

            if (annotation.Type == "underline")
            {
                page.DrawLineFromTop(left, bottom - 1.5, right, bottom - 1.5, lineWidth: strokeWidth, strokeColor: color);
                continue;
            }

            if (annotation.Type == "strikeout")
            {
                page.DrawLineFromTop(left, top + height / 2, right, top + height / 2, lineWidth: strokeWidth, strokeColor: color);
            }
        }

        return true;
    }

    private static void DrawLineEnding(
        PdfPage page,
        string? ending,
        double tipX,
        double tipTopY,
        double tailX,
        double tailTopY,
        double strokeWidth,
        CanvasPdfColor color)
    {
        if (string.IsNullOrWhiteSpace(ending) || ending == "none")
            return;

        var size = Math.Max(5, strokeWidth * 3);
        if (ending == "circle")
        {
            page.DrawCircleFromTop(tipX, tipTopY, size / 2, lineWidth: 0.5, fill: true, strokeColor: color, fillColor: color);
            return;
        }

        if (ending == "square")
        {
            page.DrawRectangleFromTop(tipX - size / 2, tipTopY - size / 2, size, size, lineWidth: 0.5, fill: true, strokeColor: color, fillColor: color);
            return;
        }

        if (ending != "arrow")
            return;

        var dx = tipX - tailX;
        var dy = tipTopY - tailTopY;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= 0.001)
            return;

        var ux = dx / length;
        var uy = dy / length;
        var px = -uy;
        var py = ux;
        var baseX = tipX - ux * size;
        var baseTopY = tipTopY - uy * size;
        var half = size * 0.45;

        page.DrawPolygon(
        [
            ToPdfPoint(page, tipX, tipTopY),
            ToPdfPoint(page, baseX + px * half, baseTopY + py * half),
            ToPdfPoint(page, baseX - px * half, baseTopY - py * half)
        ], lineWidth: 0.5, fill: true, strokeColor: color, fillColor: color);
    }

    private static Canvas.Pdf.PdfPoint ToPdfPoint(PdfPage page, double x, double topY) => new(x, page.Height - topY);

    private static IReadOnlyList<PdfMarkupQuadPoint>? ToPdfMarkupQuads(PdfPage page, PdfViewerAnnotation annotation)
    {
        if (annotation.QuadPoints.Count == 0)
            return null;

        return annotation.QuadPoints
            .Select(point => new PdfMarkupQuadPoint(
                Percent(point.X1Pct, page.Width),
                page.Height - Percent(point.Y1Pct, page.Height),
                Percent(point.X2Pct, page.Width),
                page.Height - Percent(point.Y2Pct, page.Height),
                Percent(point.X3Pct, page.Width),
                page.Height - Percent(point.Y3Pct, page.Height),
                Percent(point.X4Pct, page.Width),
                page.Height - Percent(point.Y4Pct, page.Height)))
            .ToArray();
    }

    private static PdfRectangle ToImporterRectangle(PdfPageModel page, PdfViewerAnnotation annotation)
    {
        var mediaBox = page.MediaBox ?? new PdfRectangle(0, 0, PdfPageSizes.A4Width, PdfPageSizes.A4Height);
        var x = Percent(annotation.XPct, mediaBox.Width);
        var topY = Percent(annotation.YPct, mediaBox.Height);
        var width = Math.Max(1, Percent(annotation.WidthPct, mediaBox.Width));
        var height = Math.Max(1, Percent(annotation.HeightPct, mediaBox.Height));
        return new PdfRectangle(mediaBox.X + x, mediaBox.Y + mediaBox.Height - topY - height, width, height);
    }

    private static void DrawStamp(PdfPage page, PdfViewerAnnotation annotation, double x, double topY, double width, double height, CanvasPdfColor color)
    {
        page.DrawRoundedRectangleFromTop(x, topY, width, height, cornerRadius: 4, lineWidth: 2, fill: false, strokeColor: color);
        DrawTextIfAny(page, annotation.Text, x + 6, topY + Math.Max(2, height / 2 - 6), 12, color, bold: true);
    }

    private static void DrawImage(PdfPage page, PdfViewerAnnotation annotation, double x, double topY, double width, double height)
    {
        var imageBytes = DecodeDataUrl(annotation.ImageDataUrl);
        if (imageBytes.Length == 0)
            return;

        page.DrawImageFromTop(imageBytes, x, topY, width, height, Math.Clamp(annotation.Opacity, 10, 100) / 100d);
    }

    private static void DrawNote(PdfPage page, PdfViewerAnnotation annotation, double x, double topY, double width, double height, CanvasPdfColor color)
    {
        page.DrawRoundedRectangleFromTop(x, topY, width, height, cornerRadius: 3, lineWidth: 1, fill: true, strokeColor: color, fillColor: Soften(color));
        DrawTextIfAny(page, annotation.Text, x + 5, topY + 5, 9, CanvasPdfColor.Black);
    }

    private static void DrawFreeText(PdfPage page, PdfViewerAnnotation annotation, double x, double topY, double width, double height, CanvasPdfColor color)
    {
        page.DrawRectangleFromTop(x, topY, width, height, lineWidth: 1, fill: false, strokeColor: color);
        DrawTextIfAny(page, annotation.Text, x + 4, topY + 4, 10, color);
    }

    private static void DrawTextIfAny(PdfPage page, string? text, double x, double topY, double fontSize, CanvasPdfColor color, bool bold = false)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        page.DrawTextFromTop(text.Trim(), x, topY, new PdfDrawTextOptions
        {
            FontSize = fontSize,
            FillColor = color,
            Bold = bold,
        });
    }

    private static double Percent(double value, double total) => Math.Clamp(value, 0, 100) / 100d * total;

    private static CanvasPdfColor ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 7 || value[0] != '#')
            return CanvasPdfColor.RedColor;

        try
        {
            return CanvasPdfColor.FromRgb(
                Convert.ToInt32(value[1..3], 16),
                Convert.ToInt32(value[3..5], 16),
                Convert.ToInt32(value[5..7], 16));
        }
        catch (FormatException)
        {
            return CanvasPdfColor.RedColor;
        }
    }

    private static CanvasPdfColor Soften(CanvasPdfColor color, double opacity = 45)
    {
        var weight = Math.Clamp(opacity, 10, 100) / 100d;
        return new CanvasPdfColor(
            1 - (1 - color.Red) * weight,
            1 - (1 - color.Green) * weight,
            1 - (1 - color.Blue) * weight);
    }

    private sealed record PdfViewerAnnotation(
        string Type,
        int PageNumber,
        double XPct,
        double YPct,
        double WidthPct,
        double HeightPct,
        string Text,
        string Color,
        string? ImageDataUrl,
        double Opacity,
        double StrokeWidth,
        bool FillEnabled,
        string? FillColor,
        string LineEndingStart,
        string LineEndingEnd,
        IReadOnlyList<PdfViewerInkPoint> Points,
        string Author,
        DateTimeOffset? CreatedAt,
        string? Reason,
        IReadOnlyList<PdfViewerMarkupQuadPoint> QuadPoints)
    {
        public string RedactionReason => !string.IsNullOrWhiteSpace(Reason)
            ? Reason.Trim()
            : Text.Trim();

        public static PdfViewerAnnotation? FromJson(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
                return null;

            var type = ReadString(element, "type");
            if (string.IsNullOrWhiteSpace(type))
                return null;

            return new PdfViewerAnnotation(
                type,
                Math.Max(1, ReadInt(element, "pageNumber", 1)),
                ReadDouble(element, "xPct"),
                ReadDouble(element, "yPct"),
                ReadDouble(element, "widthPct"),
                ReadDouble(element, "heightPct"),
                ReadString(element, "text") ?? "",
                ReadString(element, "color") ?? "#ef4444",
                ReadString(element, "imageDataUrl"),
                ReadDouble(element, "opacity", 100),
                ReadDouble(element, "strokeWidth", 2),
                ReadBool(element, "fillEnabled"),
                ReadString(element, "fillColor"),
                ReadString(element, "lineEndingStart") ?? "none",
                ReadString(element, "lineEndingEnd") ?? "none",
                ReadPoints(element),
                ReadString(element, "author") ?? "",
                ReadDateTimeOffset(element, "createdAt"),
                ReadString(element, "reason"),
                ReadQuadPoints(element));
        }

        private static IReadOnlyList<PdfViewerInkPoint> ReadPoints(JsonElement element)
        {
            if (!element.TryGetProperty("points", out var pointsElement) || pointsElement.ValueKind != JsonValueKind.Array)
                return [];

            return pointsElement
                .EnumerateArray()
                .Where(static point => point.ValueKind == JsonValueKind.Object)
                .Select(static point => new PdfViewerInkPoint(
                    ReadDouble(point, "xPct"),
                    ReadDouble(point, "yPct")))
                .ToArray();
        }

        private static IReadOnlyList<PdfViewerMarkupQuadPoint> ReadQuadPoints(JsonElement element)
        {
            if (!element.TryGetProperty("quadPoints", out var pointsElement) || pointsElement.ValueKind != JsonValueKind.Array)
                return [];

            return pointsElement
                .EnumerateArray()
                .Where(static point => point.ValueKind == JsonValueKind.Object)
                .Select(static point => new PdfViewerMarkupQuadPoint(
                    ReadDouble(point, "x1Pct"),
                    ReadDouble(point, "y1Pct"),
                    ReadDouble(point, "x2Pct"),
                    ReadDouble(point, "y2Pct"),
                    ReadDouble(point, "x3Pct"),
                    ReadDouble(point, "y3Pct"),
                    ReadDouble(point, "x4Pct"),
                    ReadDouble(point, "y4Pct")))
                .ToArray();
        }

        private static string? ReadString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static int ReadInt(JsonElement element, string propertyName, int fallback)
        {
            return element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result)
                ? result
                : fallback;
        }

        private static bool ReadBool(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value)
                && value.ValueKind is JsonValueKind.True or JsonValueKind.False
                && value.GetBoolean();
        }

        private static double ReadDouble(JsonElement element, string propertyName, double fallback = 0)
        {
            return element.TryGetProperty(propertyName, out var value) && value.TryGetDouble(out var result)
                ? result
                : fallback;
        }

        private static DateTimeOffset? ReadDateTimeOffset(JsonElement element, string propertyName)
        {
            var value = ReadString(element, propertyName);
            return DateTimeOffset.TryParse(value, out var result) ? result : null;
        }
    }

    private sealed record PdfViewerInkPoint(double XPct, double YPct);

    private sealed record PdfViewerMarkupQuadPoint(
        double X1Pct,
        double Y1Pct,
        double X2Pct,
        double Y2Pct,
        double X3Pct,
        double Y3Pct,
        double X4Pct,
        double Y4Pct);

    private static byte[] DecodeDataUrl(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl))
            return [];

        var comma = dataUrl.IndexOf(',');
        var payload = comma >= 0 ? dataUrl[(comma + 1)..] : dataUrl;
        try
        {
            return Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            return [];
        }
    }
}

#pragma warning restore PXA0002
