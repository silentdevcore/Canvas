using Canvas.Core.Contracts;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Graphics.Colors;

namespace Canvas.Infrastructure.Converters;

/// <summary>
/// Converts an existing PDF into a <see cref="DesignExportDto"/> using a multi-pass
/// per-page processor: letter → paragraph grouping, table grid detection from vector
/// paths, vector shape extraction, header/footer routing, and annotation link extraction.
/// </summary>
public static class PdfImporter
{
    private const double PtToPx       = 1.0; // Canvas unit = 1 PDF point (A4 = 595 × 842)
    private const double ParaGapFactor = 1.6;
    private const double HeaderFrac    = 0.92;
    private const double FooterFrac    = 0.08;

    // ── Public entry point ────────────────────────────────────────────────────

    public static DesignExportDto Import(Stream stream, string? name = null)
    {
        using var pdf = PdfDocument.Open(stream);

        bool multiPage = pdf.NumberOfPages > 1;
        double canvasW = 595, canvasH = 842;
        string? pageBackground = null;

        var pages          = new List<PageDto>();
        var sharedByContent = new Dictionary<string, ElementDto>(StringComparer.Ordinal);

        foreach (var pdfPage in pdf.GetPages())
        {
            canvasW = pdfPage.Width  * PtToPx;
            canvasH = pdfPage.Height * PtToPx;

            int seq      = 0;
            var elements = new List<ElementDto>();

            // Pass A — filled rectangles (text background colours)
            var filledRects = ExtractFilledRects(pdfPage);

            // Detect full-page background colour
            pageBackground = null;
            foreach (var fr in filledRects)
            {
                if ((fr.Right  - fr.Left)   >= pdfPage.Width  * 0.85 &&
                    (fr.Top    - fr.Bottom) >= pdfPage.Height * 0.85 &&
                    fr.HexColor != "#FFFFFF")
                {
                    pageBackground = fr.HexColor;
                    break;
                }
            }

            // Pass B — table grid detection
            var tables = DetectTables(pdfPage);

            var allLetters = pdfPage.Letters;
            var inTable    = LettersInTables(allLetters, tables);

            // Shapes (behind text) — non-table stroked / filled paths
            elements.AddRange(ExtractShapes(pdfPage, tables, ref seq));

            // Pass D — table elements
            foreach (var tbl in tables)
                elements.Add(BuildTableElement(tbl, allLetters, pdfPage, ref seq));

            // Pass C — paragraph text elements
            var freeLetters = allLetters.Where(l => !inTable.Contains(l)).ToList();
            var paragraphs  = GroupIntoParagraphs(freeLetters, pdfPage.Height);

            foreach (var para in paragraphs)
            {
                var dto = BuildParagraphElement(para, filledRects, pdfPage, ref seq);
                if (dto is null) continue;

                if (multiPage && IsHeaderOrFooter(para, pdfPage.Height))
                {
                    string key = para.DominantText.Trim();
                    if (!string.IsNullOrWhiteSpace(key))
                        sharedByContent.TryAdd(key, dto);
                }
                else
                {
                    elements.Add(dto);
                }
            }

            // Images
            foreach (var img in pdfPage.GetImages())
            {
                try
                {
                    var bb = img.Bounds;
                    double ix = bb.Left   * PtToPx;
                    double iy = (pdfPage.Height - bb.Top) * PtToPx;
                    double iw = bb.Width  * PtToPx;
                    double ih = bb.Height * PtToPx;
                    if (iw < 1 && ih < 1) continue; // zero-size XObject — skip

                    string dataUri;
                    if (img.TryGetPng(out byte[] png) && png.Length > 0)
                        dataUri = $"data:image/png;base64,{Convert.ToBase64String(png)}";
                    else
                        dataUri = $"data:image/jpeg;base64,{Convert.ToBase64String(img.RawBytes.ToArray())}";

                    elements.Add(new ElementDto
                    {
                        Id      = $"img-{pdfPage.Number}-{seq++}",
                        Type    = "image",
                        X       = Math.Max(0, ix),
                        Y       = Math.Max(0, iy),
                        Width   = Math.Max(10, iw),
                        Height  = Math.Max(10, ih),
                        Content = dataUri,
                        Style   = new Dictionary<string, object> { ["fitMode"] = "contain" },
                    });
                }
                catch { }
            }

            // Link annotations + underline/strikeout
            try
            {
                foreach (var ann in pdfPage.ExperimentalAccess.GetAnnotations())
                {
                    var annType = ann.Type;

                    if (annType == UglyToad.PdfPig.Annotations.AnnotationType.Link)
                    {
                        if (ann.Action is not UglyToad.PdfPig.Actions.UriAction uriAct) continue;
                        if (string.IsNullOrWhiteSpace(uriAct.Uri)) continue;

                        var r = ann.Rectangle;
                        elements.Add(new ElementDto
                        {
                            Id         = $"lnk-{pdfPage.Number}-{seq++}",
                            Type       = "link",
                            X          = Math.Round(r.Left  * PtToPx, 1),
                            Y          = Math.Round((pdfPage.Height - r.Top) * PtToPx, 1),
                            Width      = Math.Round(r.Width  * PtToPx, 1),
                            Height     = Math.Round(r.Height * PtToPx, 1),
                            Href       = uriAct.Uri,
                            LinkTarget = "_blank",
                            Style      = new Dictionary<string, object>
                            {
                                ["color"]          = "#0000EE",
                                ["textDecoration"] = "underline",
                            },
                        });
                    }
                    else if (annType == UglyToad.PdfPig.Annotations.AnnotationType.Underline ||
                             annType == UglyToad.PdfPig.Annotations.AnnotationType.StrikeOut)
                    {
                        var r = ann.Rectangle;
                        string decoration = annType == UglyToad.PdfPig.Annotations.AnnotationType.StrikeOut
                            ? "line-through" : "underline";
                        // Apply to first overlapping text element
                        var overlap = elements.FirstOrDefault(e =>
                            e.X <= r.Right * PtToPx && e.X + e.Width  >= r.Left * PtToPx &&
                            e.Y <= (pdfPage.Height - r.Bottom) * PtToPx &&
                            e.Y + e.Height >= (pdfPage.Height - r.Top) * PtToPx);
                        if (overlap?.Style is not null)
                            overlap.Style["textDecoration"] = decoration;
                    }
                }
            }
            catch { }

            pages.Add(new PageDto { Id = $"page-{pdfPage.Number}", Elements = elements });
        }

        var info = pdf.Information;
        return new DesignExportDto
        {
            Id   = Guid.NewGuid().ToString("N")[..12],
            Name = name ?? info.Title ?? "Imported PDF",
            Pages = pages,
            SharedElements = [.. sharedByContent.Values],
            PageSettings   = new PageSettingsDto
            {
                Width           = canvasW,
                Height          = canvasH,
                Orientation     = canvasW > canvasH ? "landscape" : "portrait",
                BackgroundColor = pageBackground,
                Margins         = new MarginsDto { Top = 0, Right = 0, Bottom = 0, Left = 0 },
                Metadata    = new PdfMetadataDto
                {
                    Title    = info.Title    ?? "",
                    Author   = info.Author   ?? "",
                    Subject  = info.Subject  ?? "",
                    Keywords = info.Keywords ?? "",
                },
            },
        };
    }

    // ── Pass A: Filled rectangles ─────────────────────────────────────────────

    private sealed record FilledRect(double Left, double Bottom, double Right, double Top, string HexColor);

    private static List<FilledRect> ExtractFilledRects(Page page)
    {
        var result = new List<FilledRect>();
        try
        {
            foreach (var path in page.ExperimentalAccess.Paths)
            {
                if (!path.IsFilled || path.IsClipping) continue;
                var bb = path.GetBoundingRectangle();
                if (bb is null) continue;
                var r = bb.Value;
                if (r.Width < 5 || r.Height < 5) continue;
                result.Add(new FilledRect(r.Left, r.Bottom, r.Right, r.Top, ColorToHex(path.FillColor)));
            }
        }
        catch { }
        return result;
    }

    // ── Pass B: Table detection ───────────────────────────────────────────────

    private sealed record TableRegion(
        double PdfLeft, double PdfBottom, double PdfRight, double PdfTop,
        double[] SortedColXs, double[] SortedRowYs);

    private static List<TableRegion> DetectTables(Page page)
    {
        var hLines = new List<(double X1, double X2, double Y)>();
        var vLines = new List<(double X, double Y1, double Y2)>();

        try
        {
            foreach (var path in page.ExperimentalAccess.Paths)
            {
                if (!path.IsStroked) continue;
                var bb = path.GetBoundingRectangle();
                if (bb is null) continue;
                var r = bb.Value;
                if (r.Height < 3.0 && r.Width > 10.0)
                    hLines.Add((r.Left, r.Right, (r.Bottom + r.Top) / 2.0));
                else if (r.Width < 3.0 && r.Height > 10.0)
                    vLines.Add(((r.Left + r.Right) / 2.0, r.Bottom, r.Top));
            }
        }
        catch { return []; }

        if (hLines.Count < 2) return [];

        // Fallback: horizontal-rule-only tables (no vertical lines detected)
        if (vLines.Count < 2)
        {
            double minX  = hLines.Min(l => l.X1);
            double maxX  = hLines.Max(l => l.X2);
            double minY  = hLines.Min(l => l.Y);
            double maxY  = hLines.Max(l => l.Y);
            double[] rowYs = hLines.Select(l => l.Y).Distinct().OrderBy(y => y).ToArray();
            return [new TableRegion(minX, minY, maxX, maxY, [minX, maxX], rowYs)];
        }

        var tables = new List<TableRegion>();
        var usedH  = new HashSet<int>();

        for (int hi = 0; hi < hLines.Count; hi++)
        {
            if (usedH.Contains(hi)) continue;
            var (hx1, hx2, _) = hLines[hi];

            var clusterH = new List<int> { hi };
            for (int hj = hi + 1; hj < hLines.Count; hj++)
            {
                var (jx1, jx2, _) = hLines[hj];
                if (Math.Abs(jx1 - hx1) < 15 && Math.Abs(jx2 - hx2) < 15)
                    clusterH.Add(hj);
            }
            if (clusterH.Count < 2) continue;

            double tblLeft  = clusterH.Min(i => hLines[i].X1);
            double tblRight = clusterH.Max(i => hLines[i].X2);
            double tblBot   = clusterH.Min(i => hLines[i].Y);
            double tblTop   = clusterH.Max(i => hLines[i].Y);

            var clusterV = new List<int>();
            for (int vi = 0; vi < vLines.Count; vi++)
            {
                var (vx, vy1, vy2) = vLines[vi];
                if (vx >= tblLeft - 5 && vx <= tblRight + 5 &&
                    vy1 <= tblTop  + 5 && vy2 >= tblBot  - 5)
                    clusterV.Add(vi);
            }
            if (clusterV.Count < 2) continue;

            foreach (var i in clusterH) usedH.Add(i);

            double[] colXs = clusterV.Select(i => vLines[i].X).Distinct().OrderBy(x => x).ToArray();
            double[] rowYs = clusterH.Select(i => hLines[i].Y).Distinct().OrderBy(y => y).ToArray();

            tables.Add(new TableRegion(tblLeft, tblBot, tblRight, tblTop, colXs, rowYs));
        }

        return tables;
    }

    private static HashSet<Letter> LettersInTables(IReadOnlyList<Letter> letters, List<TableRegion> tables)
    {
        var result = new HashSet<Letter>(ReferenceEqualityComparer.Instance);
        foreach (var tbl in tables)
        foreach (var l in letters)
        {
            var pt = l.StartBaseLine;
            if (pt.X >= tbl.PdfLeft - 5 && pt.X <= tbl.PdfRight  + 5 &&
                pt.Y >= tbl.PdfBottom - 5 && pt.Y <= tbl.PdfTop  + 5)
                result.Add(l);
        }
        return result;
    }

    private static ElementDto BuildTableElement(
        TableRegion tbl, IReadOnlyList<Letter> allLetters, Page page, ref int seq)
    {
        int rows = Math.Max(1, tbl.SortedRowYs.Length - 1);
        int cols = Math.Max(1, tbl.SortedColXs.Length - 1);

        var cellData = new string[rows][];
        for (int r = 0; r < rows; r++)
        {
            cellData[r] = new string[cols];
            double cellBot = tbl.SortedRowYs[r];
            double cellTop = tbl.SortedRowYs[r + 1];
            for (int c = 0; c < cols; c++)
            {
                double cellL = tbl.SortedColXs[c];
                double cellR = tbl.SortedColXs[c + 1];
                var sb = new System.Text.StringBuilder();
                foreach (var l in allLetters)
                {
                    var pt = l.StartBaseLine;
                    if (pt.X >= cellL && pt.X <= cellR &&
                        pt.Y >= cellBot && pt.Y <= cellTop)
                        sb.Append(l.Value);
                }
                cellData[r][c] = sb.ToString().Trim();
            }
        }

        double[] colWidths = new double[cols];
        for (int c = 0; c < cols; c++)
            colWidths[c] = (tbl.SortedColXs[c + 1] - tbl.SortedColXs[c]) * PtToPx;

        return new ElementDto
        {
            Id           = $"tbl-{page.Number}-{seq++}",
            Type         = "table",
            X            = Math.Round(tbl.PdfLeft   * PtToPx, 1),
            Y            = Math.Round((page.Height - tbl.PdfTop) * PtToPx, 1),
            Width        = Math.Round((tbl.PdfRight - tbl.PdfLeft) * PtToPx, 1),
            Height       = Math.Round((tbl.PdfTop   - tbl.PdfBottom) * PtToPx, 1),
            CellData     = cellData,
            ColumnWidths = colWidths,
            Style        = new Dictionary<string, object>
            {
                ["rows"] = rows, ["columns"] = cols,
                ["borderWidth"] = 1, ["borderColor"] = "#000000", ["cellPadding"] = 4,
            },
        };
    }

    // ── Shape extraction (non-table vector paths) ─────────────────────────────

    private static List<ElementDto> ExtractShapes(Page page, List<TableRegion> tables, ref int seq)
    {
        var shapes = new List<ElementDto>();
        try
        {
            foreach (var path in page.ExperimentalAccess.Paths)
            {
                bool hasFill   = path.IsFilled && !path.IsClipping;
                bool hasStroke = path.IsStroked;
                if (!hasFill && !hasStroke) continue;

                var bb = path.GetBoundingRectangle();
                if (bb is null) continue;
                var r = bb.Value;
                if (r.Width < 2 && r.Height < 2) continue;

                // Skip paths already inside a detected table
                if (tables.Any(t =>
                    r.Left >= t.PdfLeft - 10 && r.Right  <= t.PdfRight  + 10 &&
                    r.Bottom >= t.PdfBottom - 10 && r.Top <= t.PdfTop  + 10))
                    continue;

                double x = r.Left   * PtToPx;
                double y = (page.Height - r.Top) * PtToPx;
                double w = r.Width  * PtToPx;
                double h = r.Height * PtToPx;

                bool isHLine = r.Height < 3.0 && r.Width > 10.0;
                bool isVLine = r.Width  < 3.0 && r.Height > 10.0;

                if (isHLine || isVLine)
                {
                    // Thin divider: render as a 1-2 px filled rect
                    shapes.Add(new ElementDto
                    {
                        Id     = $"ln-{page.Number}-{seq++}",
                        Type   = "rect",
                        X      = Math.Round(x, 1),
                        Y      = Math.Round(y, 1),
                        Width  = Math.Max(1, Math.Round(w, 1)),
                        Height = Math.Max(1, Math.Round(h, 1)),
                        Style  = new Dictionary<string, object>
                        {
                            ["backgroundColor"] = ColorToHex(path.StrokeColor ?? path.FillColor),
                            ["borderWidth"] = 0,
                        },
                    });
                }
                else if (w > 5 && h > 5)
                {
                    string fillHex   = hasFill   ? ColorToHex(path.FillColor)   : "transparent";
                    string strokeHex = hasStroke ? ColorToHex(path.StrokeColor) : "transparent";
                    int    lineW     = hasStroke  ? (int)Math.Max(1, Math.Round((double)path.LineWidth * PtToPx)) : 0;
                    // Skip invisible white shapes with no stroke
                    if (fillHex == "#FFFFFF" && !hasStroke) continue;

                    shapes.Add(new ElementDto
                    {
                        Id     = $"sh-{page.Number}-{seq++}",
                        Type   = "shape",
                        X      = Math.Round(x, 1),
                        Y      = Math.Round(y, 1),
                        Width  = Math.Round(w, 1),
                        Height = Math.Round(h, 1),
                        Style  = new Dictionary<string, object>
                        {
                            ["backgroundColor"] = fillHex,
                            ["borderWidth"]     = lineW,
                            ["borderColor"]     = strokeHex,
                            ["borderStyle"]     = "solid",
                        },
                    });
                }
            }
        }
        catch { }
        return shapes;
    }

    // ── Pass C: Letter → paragraph grouping ──────────────────────────────────

    private sealed record LetterRun(
        string Text, string Color, string FontFamily,
        double FontSizePx, bool Bold, bool Italic);

    private sealed class ParagraphLine
    {
        public List<LetterRun> Runs  { get; } = [];
        public double PdfBaselineY   { get; init; }
        public double PdfLeft        { get; set; } = double.MaxValue;
        public double PdfRight       { get; set; } = double.MinValue;
        public double PdfTop         { get; set; } = double.MinValue;
        public double PdfBottom      { get; set; } = double.MaxValue;
        public double DominantSizePx { get; set; }
        public TextOrientation Orientation { get; init; }

        // For letterSpacing calculation
        public double TotalAdvancePt { get; set; }
        public double TotalVisualPt  { get; set; }
        public int    LetterCount    { get; set; }
    }

    private sealed class ParagraphBlock
    {
        public List<ParagraphLine> Lines  { get; } = [];
        public double PdfLeft  => Lines.Count > 0 ? Lines.Min(l => l.PdfLeft)  : 0;
        public double PdfRight => Lines.Count > 0 ? Lines.Max(l => l.PdfRight) : 0;
        public double PdfTop   => Lines.Count > 0 ? Lines.Max(l => l.PdfTop)   : 0;
        public double PdfBot   => Lines.Count > 0 ? Lines.Min(l => l.PdfBottom): 0;
        public double DominantSizePx => Lines.Count > 0 ? Lines.Max(l => l.DominantSizePx) : 12;
        public TextOrientation Orientation => Lines.Count > 0 ? Lines[0].Orientation : TextOrientation.Horizontal;
        public string DominantText => string.Join(" ", Lines.SelectMany(l => l.Runs).Select(r => r.Text));
    }

    private static List<ParagraphBlock> GroupIntoParagraphs(List<Letter> letters, double pageHeight)
    {
        if (letters.Count == 0) return [];

        var lineMap = new SortedDictionary<double, ParagraphLine>(Comparer<double>.Create((a, b) => b.CompareTo(a)));

        foreach (var letter in letters)
        {
            double baseY = Math.Round(letter.StartBaseLine.Y / 2.0) * 2.0;
            TextOrientation orient = letter.TextOrientation;

            if (!lineMap.TryGetValue(baseY, out var line))
            {
                line = new ParagraphLine { PdfBaselineY = baseY, Orientation = orient };
                lineMap[baseY] = line;
            }

            string color      = ColorToHex(letter.Color);
            string fontFamily = CleanFontName(letter.FontName);
            double sizePx     = Math.Max(6, letter.PointSize * PtToPx);
            // Use Font.IsBold / Font.IsItalic (direct flags) with name-based fallback
            bool bold         = letter.Font.IsBold   || IsBold(letter.FontName);
            bool italic       = letter.Font.IsItalic || IsItalic(letter.FontName);

            var lastRun = line.Runs.Count > 0 ? line.Runs[^1] : null;
            if (lastRun is not null &&
                lastRun.Color == color && lastRun.FontFamily == fontFamily &&
                Math.Abs(lastRun.FontSizePx - sizePx) < 0.5 &&
                lastRun.Bold == bold && lastRun.Italic == italic)
            {
                line.Runs[^1] = lastRun with { Text = lastRun.Text + letter.Value };
            }
            else
            {
                line.Runs.Add(new LetterRun(letter.Value, color, fontFamily, sizePx, bold, italic));
            }

            // Bounding box
            var gr = letter.GlyphRectangle;
            if (gr.Left   < line.PdfLeft)   line.PdfLeft   = gr.Left;
            if (gr.Right  > line.PdfRight)  line.PdfRight  = gr.Right;
            if (gr.Top    > line.PdfTop)    line.PdfTop    = gr.Top;
            if (gr.Bottom < line.PdfBottom) line.PdfBottom = gr.Bottom;

            // Tracking for letterSpacing
            line.TotalAdvancePt += letter.Width;
            line.TotalVisualPt  += gr.Width;
            line.LetterCount    ++;
        }

        foreach (var line in lineMap.Values)
        {
            line.DominantSizePx = line.Runs.Count > 0
                ? line.Runs.Max(r => r.FontSizePx)
                : 12;
        }

        var paragraphs = new List<ParagraphBlock>();
        ParagraphBlock? current = null;

        foreach (var line in lineMap.Values)
        {
            if (current is null)
            {
                current = new ParagraphBlock();
                current.Lines.Add(line);
                continue;
            }

            var prevLine    = current.Lines[^1];
            double lineH    = Math.Max(prevLine.DominantSizePx / PtToPx, 4.0);
            double gapPdf   = prevLine.PdfBottom - line.PdfTop;
            bool sameOrient = line.Orientation == current.Orientation;

            if (sameOrient && gapPdf < lineH * ParaGapFactor)
            {
                current.Lines.Add(line);
            }
            else
            {
                paragraphs.Add(current);
                current = new ParagraphBlock();
                current.Lines.Add(line);
            }
        }
        if (current is not null && current.Lines.Count > 0)
            paragraphs.Add(current);

        return paragraphs;
    }

    // ── Pass C → ElementDto ───────────────────────────────────────────────────

    private static ElementDto? BuildParagraphElement(
        ParagraphBlock para, List<FilledRect> filledRects, Page page, ref int seq)
    {
        if (para.Lines.Count == 0) return null;

        string text = para.DominantText.Trim();
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Canvas coordinates — use actual bounding box width (no expansion)
        double canvasX = Math.Round(para.PdfLeft  * PtToPx, 1);
        double canvasY = Math.Round((page.Height - para.PdfTop) * PtToPx, 1);
        double canvasW = Math.Round(Math.Max(para.PdfRight - para.PdfLeft, 1) * PtToPx, 1);
        double canvasH = Math.Round((para.PdfTop  - para.PdfBot) * PtToPx + 4, 1);

        // Background colour from any filled rect that overlaps this paragraph
        string? bgColor = null;
        foreach (var fr in filledRects)
        {
            if (fr.Right > para.PdfLeft && fr.Left < para.PdfRight &&
                fr.Top   > para.PdfBot  && fr.Bottom < para.PdfTop &&
                fr.HexColor != "#FFFFFF" && fr.HexColor != "#000000")
            {
                bgColor = fr.HexColor;
                break;
            }
        }

        double rotation = OrientationToDegrees(para.Orientation);

        var allRuns = para.Lines.SelectMany(l => l.Runs).ToList();
        var domRun  = allRuns.OrderByDescending(r => r.FontSizePx).First();

        bool uniformStyle = allRuns.All(r =>
            r.Color      == domRun.Color &&
            r.FontFamily == domRun.FontFamily &&
            Math.Abs(r.FontSizePx - domRun.FontSizePx) < 0.5 &&
            r.Bold   == domRun.Bold &&
            r.Italic == domRun.Italic);

        var style = new Dictionary<string, object>
        {
            ["fontSize"]   = Math.Round(domRun.FontSizePx, 1),
            ["fontFamily"] = domRun.FontFamily,
            ["color"]      = domRun.Color,
            ["fontWeight"] = domRun.Bold   ? (object)"bold"   : "normal",
            ["fontStyle"]  = domRun.Italic ? (object)"italic" : "normal",
        };
        if (bgColor is not null) style["backgroundColor"] = bgColor;
        if (rotation != 0)      style["rotation"]        = rotation;

        // letterSpacing from average advance-vs-glyph width difference
        double totalAdv = para.Lines.Sum(l => l.TotalAdvancePt);
        double totalVis = para.Lines.Sum(l => l.TotalVisualPt);
        int    lCount   = para.Lines.Sum(l => l.LetterCount);
        if (lCount > 0)
        {
            double lsPx = ((totalAdv - totalVis) / lCount) * PtToPx;
            if (lsPx > 0.3) style["letterSpacing"] = $"{lsPx:F1}px";
        }

        // lineHeight from baseline-to-baseline distance
        if (para.Lines.Count > 1)
        {
            double totalBtoB = 0;
            for (int i = 0; i < para.Lines.Count - 1; i++)
                totalBtoB += para.Lines[i].PdfBaselineY - para.Lines[i + 1].PdfBaselineY;
            double avgBtoB = totalBtoB / (para.Lines.Count - 1);
            double lhRatio = (avgBtoB * PtToPx) / domRun.FontSizePx;
            if (lhRatio > 0.8 && Math.Abs(lhRatio - 1.4) > 0.15)
                style["lineHeight"] = Math.Round(lhRatio, 2);
        }

        double elemH = Math.Max(canvasH, domRun.FontSizePx * 1.4);

        if (uniformStyle)
        {
            return new ElementDto
            {
                Id      = $"p-{page.Number}-{seq++}",
                Type    = "text",
                X       = canvasX,
                Y       = canvasY,
                Width   = canvasW,
                Height  = elemH,
                Content = text,
                Style   = style,
            };
        }
        else
        {
            return new ElementDto
            {
                Id          = $"p-{page.Number}-{seq++}",
                Type        = "richtext",
                X           = canvasX,
                Y           = canvasY,
                Width       = canvasW,
                Height      = elemH,
                HtmlContent = BuildHtml(para.Lines),
                Style       = style,
            };
        }
    }

    // ── Pass E: Header/footer check ───────────────────────────────────────────

    private static bool IsHeaderOrFooter(ParagraphBlock para, double pageHeight)
    {
        double midY = (para.PdfTop + para.PdfBot) / 2.0;
        return midY > pageHeight * HeaderFrac || midY < pageHeight * FooterFrac;
    }

    // ── HTML builder ──────────────────────────────────────────────────────────

    private static string BuildHtml(IEnumerable<ParagraphLine> lines)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<p style=\"margin:0;padding:0;line-height:1.3\">");
        bool first = true;
        foreach (var line in lines)
        {
            if (!first) sb.Append("<br/>");
            first = false;
            foreach (var run in line.Runs)
            {
                if (string.IsNullOrEmpty(run.Text)) continue;
                string esc = System.Net.WebUtility.HtmlEncode(run.Text);
                sb.Append($"<span style=\"font-size:{run.FontSizePx:F1}px;" +
                          $"font-family:{run.FontFamily};color:{run.Color};" +
                          $"font-weight:{(run.Bold ? "bold" : "normal")};" +
                          $"font-style:{(run.Italic ? "italic" : "normal")}\">" +
                          $"{esc}</span>");
            }
        }
        sb.Append("</p>");
        return sb.ToString();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ColorToHex(IColor? color)
    {
        if (color is null) return "#000000";
        try
        {
            var (r, g, b) = color.ToRGBValues();
            return $"#{ToByte(r)}{ToByte(g)}{ToByte(b)}";
        }
        catch { return "#000000"; }
    }

    private static string ToByte(double v)
        => Math.Clamp((int)Math.Round(v * 255), 0, 255).ToString("X2");

    private static double OrientationToDegrees(TextOrientation o) => o switch
    {
        TextOrientation.Rotate90  => 90,
        TextOrientation.Rotate180 => 180,
        TextOrientation.Rotate270 => 270,
        _ => 0,
    };

    private static bool IsBold(string? name)
        => name is not null &&
           (name.Contains("Bold",   StringComparison.OrdinalIgnoreCase) ||
            name.Contains("-Bd",    StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Heavy",  StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Black",  StringComparison.OrdinalIgnoreCase));

    private static bool IsItalic(string? name)
        => name is not null &&
           (name.Contains("Italic",  StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Oblique", StringComparison.OrdinalIgnoreCase));

    private static string CleanFontName(string? name)
    {
        if (name is null) return "Arial";
        int plus = name.IndexOf('+');
        string clean = plus >= 0 ? name[(plus + 1)..] : name;
        int dash = clean.IndexOf('-');
        return dash > 0 ? clean[..dash] : clean;
    }
}
