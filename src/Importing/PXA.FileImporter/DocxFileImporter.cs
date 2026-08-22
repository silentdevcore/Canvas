using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PXA.Core.Contracts;

namespace PXA.FileImporter;

/// <summary>
/// Converts an OOXML .docx file into a <see cref="DesignExportDto"/> that PXA
/// can display and re-export.  Paragraphs are mapped to Text/RichText elements
/// stacked top-to-bottom; inline images are extracted as base64 data-URIs.
/// </summary>
public sealed class DocxFileImporter : IFileImporter
{
    public IReadOnlyList<string> SupportedExtensions { get; } = ["docx"];

    public Task<DesignExportDto> ImportAsync(Stream stream, string? name = null) =>
        Task.FromResult(Import(stream, name));

    private const double PageWidth  = 595;
    private const double PageHeight = 842;
    private const double MarginX    = 48;
    private const double MarginY    = 48;
    private const double TwipToPxa  = 72.0 / 1440.0; // PXA page coordinates are PDF points.

    public static DesignExportDto Import(Stream stream, string? name = null)
    {
        using var doc = WordprocessingDocument.Open(stream, false);
        var mainPart = doc.MainDocumentPart
            ?? throw new InvalidDataException("DOCX has no main document part.");

        var body = mainPart.Document?.Body
            ?? throw new InvalidDataException("DOCX body is missing.");

        var sectionLayouts = body.Descendants<ParagraphProperties>()
            .Select(properties => properties.GetFirstChild<SectionProperties>())
            .Where(properties => properties is not null)
            .Select(properties => ReadLayout(properties!))
            .ToList();
        sectionLayouts.Add(ReadLayout(body.Elements<SectionProperties>().LastOrDefault()));
        var sectionIndex = 0;
        var currentLayout = sectionLayouts[sectionIndex];
        var documentLayout = currentLayout;
        var pageW = currentLayout.Width!.Value;
        var pageH = currentLayout.Height!.Value;
        var mLeft = currentLayout.Margins!.Left;
        var mRight = currentLayout.Margins.Right;
        var mTop = currentLayout.Margins.Top;
        var mBot = currentLayout.Margins.Bottom;
        var contentW = pageW - mLeft - mRight;

        // ── Walk body paragraphs and tables ───────────────────────────────────
        var pages = new List<PageDto>();
        var elements = new List<ElementDto>();
        double y = mTop;
        int seq = 0;

        foreach (var child in body.ChildElements)
        {
            if (child is SectionProperties) continue;

            if (child is Table table)
            {
                var el = ParseTable(table, mLeft, ref y, contentW, ref seq);
                if (el is not null) elements.Add(el);
                continue;
            }

            if (child is Paragraph para)
            {
                var els = ParseParagraph(para, mainPart, mLeft, ref y, contentW, pageH, mBot, ref seq);
                elements.AddRange(els);
                if (para.ParagraphProperties?.GetFirstChild<SectionProperties>() is not null)
                {
                    pages.Add(new PageDto { Id = $"page-{pages.Count + 1}", Layout = currentLayout, Elements = elements });
                    elements = [];
                    currentLayout = sectionLayouts[Math.Min(++sectionIndex, sectionLayouts.Count - 1)];
                    pageW = currentLayout.Width!.Value;
                    pageH = currentLayout.Height!.Value;
                    mLeft = currentLayout.Margins!.Left;
                    mRight = currentLayout.Margins.Right;
                    mTop = currentLayout.Margins.Top;
                    mBot = currentLayout.Margins.Bottom;
                    contentW = pageW - mLeft - mRight;
                    y = mTop;
                }
            }
        }
        if (elements.Count > 0 || pages.Count == 0)
            pages.Add(new PageDto { Id = $"page-{pages.Count + 1}", Layout = currentLayout, Elements = elements });

        // ── Document metadata ─────────────────────────────────────────────────
        var coreProps = doc.PackageProperties;
        string docName = name ?? coreProps.Title ?? "Imported DOCX";

        return new DesignExportDto
        {
            Id    = Guid.NewGuid().ToString("N")[..12],
            Name  = docName,
            Pages = pages,
            SharedElements = [],
            PageSettings  = new PageSettingsDto
            {
                Width       = documentLayout.Width!.Value,
                Height      = documentLayout.Height!.Value,
                Orientation = documentLayout.Orientation!,
                Margins     = documentLayout.Margins,
                Metadata    = new PdfMetadataDto
                {
                    Title   = coreProps.Title   ?? "",
                    Author  = coreProps.Creator  ?? "",
                    Subject = coreProps.Subject  ?? "",
                    Keywords = coreProps.Keywords ?? "",
                },
            },
        };
    }

    private static PageLayoutDto ReadLayout(SectionProperties? section)
    {
        var size = section?.GetFirstChild<PageSize>();
        var margins = section?.GetFirstChild<PageMargin>();
        var width = (size?.Width?.Value ?? 11900U) * TwipToPxa;
        var height = (size?.Height?.Value ?? 16840U) * TwipToPxa;
        var landscape = size?.Orient?.Value == PageOrientationValues.Landscape || width > height;
        return new PageLayoutDto
        {
            Width = Math.Round(width, 1),
            Height = Math.Round(height, 1),
            Orientation = landscape ? "landscape" : "portrait",
            Margins = new MarginsDto
            {
                Left = Math.Round((margins?.Left?.Value ?? 960U) * TwipToPxa, 1),
                Right = Math.Round((margins?.Right?.Value ?? 960U) * TwipToPxa, 1),
                Top = Math.Round((margins?.Top?.Value ?? 960) * TwipToPxa, 1),
                Bottom = Math.Round((margins?.Bottom?.Value ?? 960) * TwipToPxa, 1),
            },
        };
    }

    // ── Paragraph → Text / Image elements ─────────────────────────────────────

    private static List<ElementDto> ParseParagraph(
        Paragraph para, MainDocumentPart mainPart,
        double x, ref double y, double width,
        double pageH, double marginBot, ref int seq)
    {
        var result = new List<ElementDto>();

        // Collect inline images first
        foreach (var drawing in para.Descendants<Drawing>())
        {
            var imgEl = ExtractImage(drawing, mainPart, x, y, ref seq);
            if (imgEl is not null) { result.Add(imgEl); y += imgEl.Height + 6; }
        }

        string text = ExtractParagraphText(para);
        if (string.IsNullOrWhiteSpace(text)) { y += 6; return result; }

        // Typography from the paragraph's first run properties
        var pPr    = para.GetFirstChild<ParagraphProperties>();
        var rPrSrc = para.Descendants<RunProperties>().FirstOrDefault();

        double fontSize   = HalfPtToPx(rPrSrc?.FontSize?.Val?.Value ?? "24");
        bool bold         = rPrSrc?.Bold is not null;
        bool italic       = rPrSrc?.Italic is not null;
        string rawColor   = rPrSrc?.Color?.Val?.Value ?? "000000";
        string color      = rawColor == "auto" ? "#000000" : "#" + rawColor;
        string fontFamily = rPrSrc?.RunFonts?.Ascii?.Value ?? "Arial";

        var justVal = pPr?.Justification?.Val?.Value;
        string align = justVal == JustificationValues.Center  ? "center"
                     : justVal == JustificationValues.Right   ? "right"
                     : justVal == JustificationValues.Both    ? "justify"
                     : "left";

        // Spacing (OpenXML returns twips as StringValue)
        double spaceBefore = 0;
        double spaceAfter  = 0;
        if (pPr?.SpacingBetweenLines is { } spacing)
        {
            if (double.TryParse(spacing.Before?.Value, out var sb)) spaceBefore = sb;
            if (double.TryParse(spacing.After?.Value,  out var sa)) spaceAfter  = sa;
        }

        y += spaceBefore * TwipToPxa;

        double lineH = fontSize * 1.4 + 2;
        string? styleName = pPr?.ParagraphStyleId?.Val?.Value;

        result.Add(new ElementDto
        {
            Id        = $"p-{seq++}",
            Type      = "text",
            X         = Math.Round(x, 1),
            Y         = Math.Round(y, 1),
            Width     = Math.Round(width, 1),
            Height    = Math.Round(lineH, 1),
            Content   = text,
            StyleName = styleName,
            Style     = new Dictionary<string, object>
            {
                ["fontSize"]   = Math.Round(fontSize, 1),
                ["fontFamily"] = fontFamily,
                ["color"]      = color,
                ["fontWeight"] = bold   ? (object)"bold"   : "normal",
                ["fontStyle"]  = italic ? (object)"italic" : "normal",
                ["textAlign"]  = align,
            },
        });

        y += lineH + spaceAfter * TwipToPxa;
        return result;
    }

    // ── Table ─────────────────────────────────────────────────────────────────

    private static ElementDto? ParseTable(Table table, double x, ref double y, double width, ref int seq)
    {
        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count == 0) return null;

        int cols = rows.Max(r => r.Elements<TableCell>().Count());
        var cellData = rows.Select(r =>
            r.Elements<TableCell>()
             .Select(c => ExtractParagraphText(c.GetFirstChild<Paragraph>() ?? new Paragraph()))
             .ToArray()
        ).ToArray();

        double rowHeight = 24;
        double tableH    = rows.Count * rowHeight;

        var dto = new ElementDto
        {
            Id      = $"tbl-{seq++}",
            Type    = "table",
            X       = Math.Round(x, 1),
            Y       = Math.Round(y, 1),
            Width   = Math.Round(width, 1),
            Height  = Math.Round(tableH, 1),
            CellData = cellData,
            Style   = new Dictionary<string, object>
            {
                ["rows"]        = rows.Count,
                ["columns"]     = cols,
                ["borderWidth"] = 1,
                ["borderColor"] = "#000000",
                ["cellPadding"] = 4,
            },
        };

        y += tableH + 8;
        return dto;
    }

    // ── Image extraction ──────────────────────────────────────────────────────

    private static ElementDto? ExtractImage(Drawing drawing, MainDocumentPart mainPart, double x, double y, ref int seq)
    {
        try
        {
            var blip = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().FirstOrDefault();
            if (blip?.Embed?.Value is null) return null;

            var imgPart = (ImagePart)mainPart.GetPartById(blip.Embed.Value);
            string mime = imgPart.ContentType;

            using var ms = new MemoryStream();
            imgPart.GetStream().CopyTo(ms);
            string b64 = Convert.ToBase64String(ms.ToArray());

            // Try to get size from the extent
            var extent = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Extents>().FirstOrDefault();
            double w = extent?.Cx is { } cx ? cx / 914400.0 * 96 : 200;
            double h = extent?.Cy is { } cy ? cy / 914400.0 * 96 : 150;

            return new ElementDto
            {
                Id      = $"img-{seq++}",
                Type    = "image",
                X       = Math.Round(x, 1),
                Y       = Math.Round(y, 1),
                Width   = Math.Round(w, 1),
                Height  = Math.Round(h, 1),
                Content = $"data:{mime};base64,{b64}",
                Style   = new Dictionary<string, object> { ["fitMode"] = "contain" },
            };
        }
        catch { return null; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ExtractParagraphText(Paragraph para)
    {
        var sb = new StringBuilder();
        foreach (var run in para.Elements<Run>())
            sb.Append(run.GetFirstChild<Text>()?.Text ?? "");
        return sb.ToString();
    }

    private static double HalfPtToPx(string halfPt)
    {
        if (!double.TryParse(halfPt, out double v)) return 12;
        return v / 2.0 * 96.0 / 72.0; // half-points → points → px at 96dpi
    }

}
