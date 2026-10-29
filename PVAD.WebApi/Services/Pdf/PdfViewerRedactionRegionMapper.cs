using System.Text.Json;
using PVAD.Importer;
using PVAD.Importer.Graphics;
using PVAD.Importer.Redaction;

namespace PVAD.WebApi.Services.Pdf;

/// <summary>
/// Converts Viewer redaction marks, which are percentages of the page with a top-left
/// origin, into the PDF user-space regions the redaction services work in.
/// </summary>
public sealed class PdfViewerRedactionRegionMapper
{
    /// <summary>
    /// Reads the page geometry needed to resolve percentages. This parses the document a
    /// second time, since the redaction services parse it again themselves; the alternative
    /// is a region type carrying two coordinate conventions, which is a worse trade.
    /// </summary>
    public async Task<IReadOnlyList<PdfRedactionRegion>> MapAsync(
        byte[] pdfBytes,
        JsonElement annotations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (annotations.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("The sidecar annotations must be an array.", nameof(annotations));
        }

        var marks = annotations
            .EnumerateArray()
            .Where(static element => element.ValueKind == JsonValueKind.Object)
            .Where(static element =>
                element.TryGetProperty("type", out var type) &&
                type.ValueKind == JsonValueKind.String &&
                string.Equals(type.GetString(), "redaction", StringComparison.Ordinal))
            .Select(ReadMark)
            .ToArray();
        if (marks.Length == 0)
        {
            throw new ArgumentException("At least one redaction annotation is required.", nameof(annotations));
        }

        await using var stream = new MemoryStream(pdfBytes, writable: false);
        var document = await new PdfImporter().LoadAsync(stream, cancellationToken).ConfigureAwait(false);

        var regions = new List<PdfRedactionRegion>(marks.Length);
        foreach (var mark in marks)
        {
            var page = document.Pages.ElementAtOrDefault(mark.PageNumber - 1);
            regions.Add(new PdfRedactionRegion(
                mark.PageNumber,
                MapViewerRectangle(
                    page?.CropBox ?? page?.MediaBox ?? new PdfRectangle(0, 0, 595.276, 841.89),
                    page?.Rotate ?? 0,
                    mark.XPct,
                    mark.YPct,
                    mark.WidthPct,
                    mark.HeightPct)));
        }

        return regions;
    }

    private static Mark ReadMark(JsonElement element) => new(
        Math.Max(1, ReadInt(element, "pageNumber")),
        ReadDouble(element, "xPct"),
        ReadDouble(element, "yPct"),
        ReadDouble(element, "widthPct"),
        ReadDouble(element, "heightPct"));

    private static double Percent(double value, double total) => Math.Clamp(value, 0, 100) / 100d * total;

    public static PdfRectangle MapViewerRectangle(
        PdfRectangle box,
        int rotationDegrees,
        double xPct,
        double yPct,
        double widthPct,
        double heightPct)
    {
        var rotation = ((rotationDegrees % 360) + 360) % 360;
        if (rotation is not (0 or 90 or 180 or 270))
        {
            throw new ArgumentException("PDF page rotation must resolve to 0, 90, 180, or 270 degrees.", nameof(rotationDegrees));
        }

        var displayWidth = rotation is 90 or 270 ? box.Height : box.Width;
        var displayHeight = rotation is 90 or 270 ? box.Width : box.Height;
        var x = Percent(xPct, displayWidth);
        var topY = Percent(yPct, displayHeight);
        var width = Math.Max(1, Percent(widthPct, displayWidth));
        var height = Math.Max(1, Percent(heightPct, displayHeight));

        return rotation switch
        {
            0 => new PdfRectangle(box.X + x, box.Y + box.Height - topY - height, width, height),
            90 => new PdfRectangle(box.X + topY, box.Y + x, height, width),
            180 => new PdfRectangle(box.X + box.Width - x - width, box.Y + topY, width, height),
            270 => new PdfRectangle(
                box.X + box.Width - topY - height,
                box.Y + box.Height - x - width,
                height,
                width),
            _ => throw new InvalidOperationException()
        };
    }

    private static int ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : 1;

    private static double ReadDouble(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var parsed)
            ? parsed
            : 0d;

    private sealed record Mark(int PageNumber, double XPct, double YPct, double WidthPct, double HeightPct);
}
