using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Canvas.Core.Contracts;
using PdfToSvg;

namespace Canvas.Infrastructure.Converters;

/// <summary>
/// Converts a PDF to a <see cref="DesignExportDto"/> via a clean 6-stage pipeline:
///   1. SVG Normalization  — resolve CSS classes, transform offsets, tspan nesting
///   2. Shape Classification — classify lines, filter glyph outlines / edge artifacts
///   3. Text Position Correction — fix right-anchored (text-anchor="end") runs
///   4. Table Detection   — cluster H/V lines into grid regions
///   5. Text Layout       — group runs → lines → paragraphs with X-overlap guard
///   6. Canvas Assembly   — emit typed ElementDto records
/// </summary>
public static class SvgPdfImporter
{
    // ── Constants ─────────────────────────────────────────────────────────────
    private const double LineTolerance = 3.0;  // pt — same-baseline grouping
    private const double ParaGapFactor = 1.4;  // max Y gap relative to font size
    private const double ColGapPt      = 35.0; // horizontal gap that separates columns
    private const double HeaderZone    = 0.08; // top 8 % of page → shared element
    private const double FooterZone    = 0.92; // bottom 8 % → shared element

    private static readonly XNamespace SvgNs   = "http://www.w3.org/2000/svg";
    private static readonly XNamespace XLinkNs = "http://www.w3.org/1999/xlink";

    // ── Intermediate types ────────────────────────────────────────────────────

    private sealed record SvgTextRun(
        double X, double Y, double EndX, double FontSize,
        string Text, string FontFamily, string Color,
        bool Bold, bool Italic, string TextAlign);

    private sealed record SvgShape(
        double X, double Y, double W, double H,
        string Fill, string Stroke, double StrokeWidth,
        bool IsLine, double Rx = 0, string Kind = "shape");

    private sealed record SvgImage(double X, double Y, double W, double H, string Href);

    private sealed record TextLine(List<SvgTextRun> Runs)
    {
        public double Y     => Runs[0].Y;
        public double Left  => Runs.Min(r => r.X);
        public double Right => Runs.Max(r => r.EndX);
        public double DomFs => Runs.Max(r => r.FontSize);
    }

    private sealed record TextBlock(List<TextLine> Lines)
    {
        public double Left   => Lines.Min(l => l.Left);
        public double Right  => Lines.Max(l => l.Right);
        public double Top    => Lines[0].Y;
        public double Bottom => Lines[^1].Y;
        public double DomFs  => Lines.Max(l => l.DomFs);
    }

    private sealed record TableRegion(
        double X, double Y, double W, double H,
        double[] ColXs, double[] RowYs);

    // ── Entry point ───────────────────────────────────────────────────────────

    public static DesignExportDto Import(Stream stream, string? name = null)
    {
        using var pdf = PdfDocument.Open(stream);

        double canvasW = 595, canvasH = 842;
        var pages           = new List<PageDto>();
        var sharedByContent = new Dictionary<string, ElementDto>(StringComparer.Ordinal);
        bool multiPage      = pdf.Pages.Count > 1;
        string? pageBackground = null;

        int pageNum = 0;
        foreach (var page in pdf.Pages)
        {
            pageNum++;
            int seq = 0;

            string svgStr = page.ToSvgString();
            var svgDoc    = XDocument.Parse(svgStr, LoadOptions.PreserveWhitespace);
            var svgRoot   = svgDoc.Root!;

            (canvasW, canvasH) = ParseViewBox(svgRoot);

            // ── Stage 1: Normalize SVG tree ───────────────────────────────────
            var cssRules = GetCssRules(svgRoot);
            var rawTexts  = new List<SvgTextRun>();
            var rawShapes = new List<SvgShape>();
            var rawImages = new List<SvgImage>();
            WalkSvg(svgRoot, (0.0, 0.0), cssRules, rawTexts, rawShapes, rawImages);

            // ── Stage 2: Classify & filter shapes ────────────────────────────
            pageBackground = null;
            var lineShapes    = new List<SvgShape>();
            var contentShapes = new List<SvgShape>();

            foreach (var s in rawShapes)
            {
                // Full-page background detection (≥85 % coverage, non-white fill)
                if (!s.IsLine && s.X <= 5 && s.Y <= 5 &&
                    s.W >= canvasW * 0.85 && s.H >= canvasH * 0.85 &&
                    s.Fill is not (null or "transparent" or "#FFFFFF"))
                {
                    pageBackground = s.Fill;
                    continue;
                }

                // White-on-white = invisible → discard
                if (!s.IsLine && s.Fill == "#FFFFFF" && s.Stroke == "transparent") continue;

                // Tall narrow shapes at page margins = decorative borders → discard
                if (!s.IsLine)
                {
                    bool tallThin = s.H > canvasH * 0.35 && s.W < canvasW * 0.08;
                    if (tallThin && (s.X + s.W > canvasW * 0.82 || s.X < canvasW * 0.12))
                        continue;
                }

                if (s.IsLine) lineShapes.Add(s);
                else          contentShapes.Add(s);
            }

            // ── Stage 4: Table detection ──────────────────────────────────────
            var (tableRegions, tableLineElements) = DetectTables(lineShapes, pageNum, ref seq);

            var tableBoxes = tableRegions
                .Select(r => (r.X, r.Y, r.X + r.W, r.Y + r.H))
                .ToList();

            // ── Stage 5: Text layout ──────────────────────────────────────────
            var textBlocks = LayoutText(rawTexts, tableBoxes);

            // ── Stage 6: Assembly ─────────────────────────────────────────────
            var elements = new List<ElementDto>();

            // Shapes (rects, paths, ellipses)
            foreach (var s in contentShapes)
                elements.Add(ShapeToDto(s, pageNum, ref seq));

            // Table grid lines not absorbed into a table
            elements.AddRange(tableLineElements);

            // Images
            foreach (var img in rawImages)
                elements.Add(ImageToDto(img, pageNum, ref seq));

            // Table elements (with routed cell text)
            foreach (var tbl in tableRegions)
                elements.Add(BuildTableElement(tbl, rawTexts, pageNum, ref seq));

            // Text blocks
            foreach (var block in textBlocks)
            {
                var dto = BlockToDto(block, pageNum, ref seq);
                if (dto is null) continue;

                if (multiPage)
                {
                    string key    = (dto.Content ?? dto.HtmlContent ?? "").Trim();
                    bool isHeader = block.Top    < canvasH * HeaderZone;
                    bool isFooter = block.Bottom > canvasH * FooterZone;
                    if ((isHeader || isFooter) && !string.IsNullOrWhiteSpace(key))
                        sharedByContent.TryAdd(key, dto);
                    else
                        elements.Add(dto);
                }
                else
                {
                    elements.Add(dto);
                }
            }

            pages.Add(new PageDto { Id = $"page-{pageNum}", Elements = elements });
        }

        return new DesignExportDto
        {
            Id             = Guid.NewGuid().ToString("N")[..12],
            Name           = name ?? "Imported PDF",
            Pages          = pages,
            SharedElements = [.. sharedByContent.Values],
            PageSettings   = new PageSettingsDto
            {
                Width           = Math.Round(canvasW, 1),
                Height          = Math.Round(canvasH, 1),
                Orientation     = canvasW > canvasH ? "landscape" : "portrait",
                BackgroundColor = pageBackground,
                Margins         = new MarginsDto { Top = 0, Right = 0, Bottom = 0, Left = 0 },
            },
        };
    }

    // ── Stage 1: SVG Tree Walker ──────────────────────────────────────────────

    private static void WalkSvg(
        XElement node, (double Tx, double Ty) offset,
        Dictionary<string, Dictionary<string, string>> css,
        List<SvgTextRun> texts, List<SvgShape> shapes, List<SvgImage> images)
    {
        var off = AccumulateTransform(node, offset);

        foreach (var child in node.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "text":    ExtractText(child,    off, css, texts);  break;
                case "rect":    ExtractRect(child,    off, css, shapes); break;
                case "path":    ExtractPath(child,    off, css, shapes); break;
                case "line":    ExtractLine(child,    off, css, shapes); break;
                case "circle":
                case "ellipse": ExtractEllipse(child, off, css, shapes); break;
                case "image":   ExtractImage(child,   off, images);      break;
                default:        WalkSvg(child, off, css, texts, shapes, images); break;
            }
        }
    }

    // Parse a transform="" attribute and add any translate/matrix offset to the parent.
    private static (double Tx, double Ty) AccumulateTransform(
        XElement el, (double Tx, double Ty) parent)
    {
        string? t = (string?)el.Attribute("transform");
        if (string.IsNullOrWhiteSpace(t)) return parent;

        // translate(tx [, ty])
        var mT = Regex.Match(t,
            @"translate\(\s*([+-]?[\d.]+)(?:\s*[,\s]\s*([+-]?[\d.]+))?\s*\)");
        if (mT.Success)
            return (parent.Tx + N(mT.Groups[1].Value),
                    parent.Ty + (mT.Groups[2].Success ? N(mT.Groups[2].Value) : 0));

        // matrix(a b c d e f) — e = translateX, f = translateY
        var mM = Regex.Match(t,
            @"matrix\(\s*[+-]?[\d.]+\s+[+-]?[\d.]+\s+[+-]?[\d.]+\s+[+-]?[\d.]+\s+([+-]?[\d.]+)\s+([+-]?[\d.]+)\s*\)");
        if (mM.Success)
            return (parent.Tx + N(mM.Groups[1].Value),
                    parent.Ty + N(mM.Groups[2].Value));

        return parent;
    }

    // ── Stage 1: Element extractors ───────────────────────────────────────────

    private static void ExtractText(
        XElement textEl, (double Tx, double Ty) off,
        Dictionary<string, Dictionary<string, string>> css,
        List<SvgTextRun> out_)
    {
        var pEs  = EffectiveStyle(textEl, css);
        string pFf  = CleanFont(EGet(pEs, "font-family", "Arial"));
        double pFs  = ParseFs(EGet(pEs, "font-size",   "12")) is > 0 and var f0 ? f0 : 12;
        string pClr = HexColor(EGet(pEs, "fill",       "#000000"));
        bool pBold  = EGet(pEs, "font-weight", "").Contains("bold",   StringComparison.OrdinalIgnoreCase);
        bool pItal  = EGet(pEs, "font-style",  "").Contains("italic", StringComparison.OrdinalIgnoreCase);
        string pAnc = EGet(pEs, "text-anchor", "start");

        string? textXStr = (string?)textEl.Attribute("x");
        string? textYStr = (string?)textEl.Attribute("y");

        var tspans = textEl.Descendants(SvgNs + "tspan").ToList();

        // Text directly in <text> with no tspan children
        if (tspans.Count == 0)
        {
            string raw = string.Concat(textEl.Nodes().OfType<XText>().Select(t => t.Value));
            if (!string.IsNullOrWhiteSpace(raw) &&
                !string.IsNullOrWhiteSpace(textXStr) && !string.IsNullOrWhiteSpace(textYStr))
            {
                double rx = N(textXStr) + off.Tx;
                double ry = N(textYStr) + off.Ty;
                string al = pAnc == "middle" ? "center" : pAnc == "end" ? "right" : "left";
                double estimW = raw.Length * pFs * 0.55;
                // Stage 3: correct right-anchored position
                double lx = pAnc == "end" ? rx - estimW : rx;
                double ex = pAnc == "end" ? rx           : rx + estimW;
                out_.Add(new SvgTextRun(
                    Math.Round(lx, 1), Math.Round(ry, 1), Math.Round(ex, 1),
                    pFs, raw, pFf, pClr, pBold, pItal, al));
            }
            return;
        }

        // Running cursor for dx/dy relative positioning
        double curX = N(textXStr ?? "0") + off.Tx;
        double curY = N(textYStr ?? "0") + off.Ty;

        foreach (var tspan in tspans)
        {
            string raw = string.Concat(tspan.Nodes().OfType<XText>().Select(t => t.Value));
            if (string.IsNullOrEmpty(raw)) continue;

            string? xAttr  = (string?)tspan.Attribute("x");
            string? yAttr  = (string?)tspan.Attribute("y");
            string? dxAttr = (string?)tspan.Attribute("dx");
            string? dyAttr = (string?)tspan.Attribute("dy");

            // PdfToSvg.NET nests: <tspan x y><tspan class="sN">text</tspan></tspan>
            // The inner tspan has no x/y — walk up to find them.
            if (xAttr == null || yAttr == null)
            {
                var ancestor = tspan.Parent as XElement;
                while (ancestor?.Name.LocalName == "tspan")
                {
                    xAttr ??= (string?)ancestor.Attribute("x");
                    yAttr ??= (string?)ancestor.Attribute("y");
                    ancestor = ancestor.Parent as XElement;
                }
            }

            string[] xVals = (xAttr ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Update running cursor
            if (!string.IsNullOrWhiteSpace(xAttr))
                curX = N(xVals[0]) + off.Tx;
            else if (!string.IsNullOrWhiteSpace(dxAttr))
                curX += N(dxAttr.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]);

            if (!string.IsNullOrWhiteSpace(yAttr))
                curY = N(yAttr.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]) + off.Ty;
            else if (!string.IsNullOrWhiteSpace(dyAttr))
                curY += N(dyAttr.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]);

            // No usable position at all → skip
            if (curX == 0 && curY == 0 &&
                string.IsNullOrWhiteSpace(textXStr) && string.IsNullOrWhiteSpace(textYStr) &&
                string.IsNullOrWhiteSpace(xAttr) && string.IsNullOrWhiteSpace(dxAttr)) continue;

            var tEs   = EffectiveStyle(tspan, css);
            string ff   = CleanFont(EGet(tEs, "font-family", pFf));
            double fs   = ParseFs(EGet(tEs, "font-size",   "")) is > 0 and var fv ? fv : pFs;
            string clr  = HexColor(EGet(tEs, "fill",       pClr));
            bool bold   = EGet(tEs, "font-weight", "").Contains("bold",   StringComparison.OrdinalIgnoreCase);
            bool italic = EGet(tEs, "font-style",  "").Contains("italic", StringComparison.OrdinalIgnoreCase);
            string anc  = EGet(tEs, "text-anchor", pAnc);
            string align = anc == "middle" ? "center" : anc == "end" ? "right" : "left";

            // Stage 3: determine true left/right edges, correcting for text-anchor
            double runX, runEndX;
            if (xVals.Length > 1)
            {
                // Character-level x array: first value = leftmost, last = rightmost
                runX    = N(xVals[0])  + off.Tx;
                runEndX = N(xVals[^1]) + off.Tx + fs * 0.6;
            }
            else if (anc == "end")
            {
                // Single anchor at RIGHT edge → estimate leftward
                double rightEdge  = curX;
                double estimatedW = raw.Length * fs * 0.55;
                runX    = rightEdge - estimatedW;
                runEndX = rightEdge;
            }
            else
            {
                runX    = curX;
                runEndX = curX + raw.Length * fs * 0.55;
            }

            out_.Add(new SvgTextRun(
                Math.Round(runX, 1), Math.Round(curY, 1), Math.Round(runEndX, 1),
                fs, raw, ff, clr, bold, italic, align));
        }
    }

    private static void ExtractRect(
        XElement el, (double Tx, double Ty) off,
        Dictionary<string, Dictionary<string, string>> css,
        List<SvgShape> out_)
    {
        double x = DA(el, "x") + off.Tx, y = DA(el, "y") + off.Ty;
        double w = DA(el, "width"), h = DA(el, "height"), rx = DA(el, "rx");
        if (w < 0.5 && h < 0.5) return;

        var es = EffectiveStyle(el, css);
        string fill   = HexColor(EGet(es, "fill",         "none"));
        string stroke = HexColor(EGet(es, "stroke",       "none"));
        double sw     = N(EGet(es,       "stroke-width",  "0"));
        if (fill == "transparent" && stroke == "transparent") return;

        bool isH = h < 3 && w > 10;
        bool isV = w < 3 && h > 10;
        out_.Add(new SvgShape(x, y, w, h, fill, stroke, sw, isH || isV, rx));
    }

    private static void ExtractPath(
        XElement el, (double Tx, double Ty) off,
        Dictionary<string, Dictionary<string, string>> css,
        List<SvgShape> out_)
    {
        string? d = (string?)el.Attribute("d");
        if (string.IsNullOrWhiteSpace(d)) return;

        var es = EffectiveStyle(el, css);
        string fill   = HexColor(EGet(es, "fill",         "none"));
        string stroke = HexColor(EGet(es, "stroke",       "none"));
        double sw     = N(EGet(es,       "stroke-width",  "0"));
        if (fill == "transparent" && stroke == "transparent") return;

        if (!TryParseBounds(d, out double minX, out double minY, out double maxX, out double maxY))
            return;

        double w = maxX - minX, h = maxY - minY;
        if (w < 0.5 && h < 0.5) return;

        // Small filled closed paths without stroke = individual glyph outlines → discard
        if (fill != "transparent" && stroke == "transparent" && w * h < 400) return;

        bool isH = h < 3 && w > 10;
        bool isV = w < 3 && h > 10;
        out_.Add(new SvgShape(minX + off.Tx, minY + off.Ty, w, h, fill, stroke, sw, isH || isV));
    }

    private static void ExtractLine(
        XElement el, (double Tx, double Ty) off,
        Dictionary<string, Dictionary<string, string>> css,
        List<SvgShape> out_)
    {
        double x1 = DA(el, "x1") + off.Tx, y1 = DA(el, "y1") + off.Ty;
        double x2 = DA(el, "x2") + off.Tx, y2 = DA(el, "y2") + off.Ty;

        var es = EffectiveStyle(el, css);
        string stroke = HexColor(EGet(es, "stroke",       "#000000"));
        double sw     = Math.Max(1, N(EGet(es, "stroke-width", "1")));

        double x = Math.Min(x1, x2), y = Math.Min(y1, y2);
        double w = Math.Abs(x2 - x1), h = Math.Abs(y2 - y1);
        out_.Add(new SvgShape(x, y, Math.Max(1, w), Math.Max(sw, h),
            "transparent", stroke, sw, true));
    }

    private static void ExtractEllipse(
        XElement el, (double Tx, double Ty) off,
        Dictionary<string, Dictionary<string, string>> css,
        List<SvgShape> out_)
    {
        bool isCircle = el.Name.LocalName == "circle";
        double cx = DA(el, "cx") + off.Tx, cy = DA(el, "cy") + off.Ty;
        double rx = isCircle ? DA(el, "r") : DA(el, "rx");
        double ry = isCircle ? rx           : DA(el, "ry");
        if (rx < 0.5 || ry < 0.5) return;

        var es = EffectiveStyle(el, css);
        string fill   = HexColor(EGet(es, "fill",         "none"));
        string stroke = HexColor(EGet(es, "stroke",       "none"));
        double sw     = N(EGet(es,       "stroke-width",  "0"));

        out_.Add(new SvgShape(cx - rx, cy - ry, rx * 2, ry * 2,
            fill, stroke, sw, false, 0, "circle"));
    }

    private static void ExtractImage(
        XElement el, (double Tx, double Ty) off, List<SvgImage> out_)
    {
        double x = DA(el, "x") + off.Tx, y = DA(el, "y") + off.Ty;
        double w = DA(el, "width"), h = DA(el, "height");
        if (w < 1 && h < 1) return;

        string? href = (string?)el.Attribute(XLinkNs + "href")
                    ?? (string?)el.Attribute("href");
        if (string.IsNullOrWhiteSpace(href)) return;

        out_.Add(new SvgImage(x, y, w, h, href));
    }

    // ── Stage 4: Table Detection ──────────────────────────────────────────────

    private static (List<TableRegion> tables, List<ElementDto> unusedLines)
        DetectTables(List<SvgShape> lineShapes, int pg, ref int seq)
    {
        var hLines = new List<(double X1, double X2, double Y, string Color)>();
        var vLines = new List<(double X, double Y1, double Y2, string Color)>();

        foreach (var s in lineShapes)
        {
            string col = s.Fill != "transparent" ? s.Fill : s.Stroke;
            bool isH = s.H < 3 && s.W > 20;
            bool isV = s.W < 3 && s.H > 20;
            if (isH) hLines.Add((s.X, s.X + s.W, s.Y + s.H / 2, col));
            else if (isV) vLines.Add((s.X + s.W / 2, s.Y, s.Y + s.H, col));
        }

        var unusedLines = new List<ElementDto>();

        if (hLines.Count < 2)
        {
            EmitLines(hLines, vLines, unusedLines);
            return ([], unusedLines);
        }

        var tables = new List<TableRegion>();
        var usedH  = new HashSet<int>();

        for (int hi = 0; hi < hLines.Count; hi++)
        {
            if (usedH.Contains(hi)) continue;
            var (hx1, hx2, _, _) = hLines[hi];

            var clusterH = new List<int> { hi };
            for (int hj = hi + 1; hj < hLines.Count; hj++)
                if (Math.Abs(hLines[hj].X1 - hx1) < 15 &&
                    Math.Abs(hLines[hj].X2 - hx2) < 15)
                    clusterH.Add(hj);

            if (clusterH.Count < 2) continue;

            double tblLeft  = clusterH.Min(i => hLines[i].X1);
            double tblRight = clusterH.Max(i => hLines[i].X2);
            double tblTop   = clusterH.Min(i => hLines[i].Y);
            double tblBot   = clusterH.Max(i => hLines[i].Y);

            double[] rowYs = clusterH.Select(i => hLines[i].Y)
                .Distinct().OrderBy(y => y).ToArray();

            var clusterV = vLines
                .Where(v => v.X  >= tblLeft - 5 && v.X  <= tblRight + 5 &&
                            v.Y1 <= tblTop  + 5 && v.Y2 >= tblBot   - 5)
                .Select(v => v.X).ToList();

            if (clusterV.Count < 2) clusterV = [tblLeft, tblRight];

            foreach (var i in clusterH) usedH.Add(i);

            double[] colXs = clusterV.Distinct().OrderBy(x => x).ToArray();
            tables.Add(new TableRegion(
                tblLeft, tblTop,
                tblRight - tblLeft, tblBot - tblTop,
                colXs, rowYs));
        }

        var usedHSet = new HashSet<int>(usedH);
        EmitLines(
            hLines.Select((l, i) => (i, l)).Where(t => !usedHSet.Contains(t.i)).Select(t => t.l).ToList(),
            vLines, unusedLines);

        return (tables, unusedLines);
    }

    private static void EmitLines(
        IEnumerable<(double X1, double X2, double Y, string Color)> hLines,
        IEnumerable<(double X, double Y1, double Y2, string Color)> vLines,
        List<ElementDto> target)
    {
        int i = 0;
        foreach (var (x1, x2, y, col) in hLines)
            target.Add(new ElementDto
            {
                Id     = $"ln-uh-{i++}", Type   = "rect",
                X      = Math.Round(x1, 1),     Y      = Math.Round(y - 0.5, 1),
                Width  = Math.Max(1, Math.Round(x2 - x1, 1)), Height = 1,
                Style  = new Dictionary<string, object>
                    { ["backgroundColor"] = col, ["borderWidth"] = 0 },
            });
        foreach (var (x, y1, y2, col) in vLines)
            target.Add(new ElementDto
            {
                Id     = $"ln-uv-{i++}", Type   = "rect",
                X      = Math.Round(x - 0.5, 1), Y      = Math.Round(y1, 1),
                Width  = 1, Height = Math.Max(1, Math.Round(y2 - y1, 1)),
                Style  = new Dictionary<string, object>
                    { ["backgroundColor"] = col, ["borderWidth"] = 0 },
            });
    }

    private static ElementDto BuildTableElement(
        TableRegion tbl, List<SvgTextRun> runs, int pg, ref int seq)
    {
        int rows = Math.Max(1, tbl.RowYs.Length - 1);
        int cols = Math.Max(1, tbl.ColXs.Length - 1);

        var cellData = new string[rows][];
        for (int r = 0; r < rows; r++)
        {
            cellData[r] = new string[cols];
            double rowTop = tbl.RowYs[r], rowBot = tbl.RowYs[r + 1];
            for (int c = 0; c < cols; c++)
            {
                double colL = tbl.ColXs[c], colR = tbl.ColXs[c + 1];
                var sb = new StringBuilder();
                foreach (var run in runs)
                    if (run.X >= colL - 8 && run.X <= colR + 8 &&
                        run.Y >= rowTop - 8 && run.Y <= rowBot + 8)
                        sb.Append(run.Text);
                cellData[r][c] = sb.ToString().Trim();
            }
        }

        double[] colWidths = new double[cols];
        for (int c = 0; c < cols; c++)
            colWidths[c] = Math.Round(tbl.ColXs[c + 1] - tbl.ColXs[c], 1);

        return new ElementDto
        {
            Id           = $"tbl-{pg}-{seq++}",
            Type         = "table",
            X            = Math.Round(tbl.X, 1), Y      = Math.Round(tbl.Y, 1),
            Width        = Math.Round(tbl.W, 1), Height = Math.Round(tbl.H, 1),
            CellData     = cellData,
            ColumnWidths = colWidths,
            Style        = new Dictionary<string, object>
            {
                ["rows"]        = rows, ["columns"]     = cols,
                ["borderWidth"] = 1,    ["borderColor"] = "#000000",
                ["cellPadding"] = 4,
            },
        };
    }

    // ── Stage 5: Text Layout ──────────────────────────────────────────────────

    private static List<TextBlock> LayoutText(
        List<SvgTextRun> runs,
        List<(double X1, double Y1, double X2, double Y2)> tableBounds)
    {
        if (runs.Count == 0) return [];

        // Exclude runs inside table regions (their text is routed to cells)
        var freeRuns = tableBounds.Count > 0
            ? runs.Where(r => !tableBounds.Any(tb =>
                r.X >= tb.X1 - 8 && r.X <= tb.X2 + 8 &&
                r.Y >= tb.Y1 - 8 && r.Y <= tb.Y2 + 8)).ToList()
            : runs.ToList();

        freeRuns.Sort((a, b) =>
            a.Y.CompareTo(b.Y) != 0 ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));

        // Group into text lines: same baseline (±3 pt) with no large horizontal gap
        var textLines = new List<TextLine>();
        foreach (var run in freeRuns)
        {
            var match = textLines.LastOrDefault(l =>
                Math.Abs(l.Y - run.Y) <= LineTolerance &&
                run.X - l.Right < ColGapPt);
            if (match is not null) match.Runs.Add(run);
            else textLines.Add(new TextLine([run]));
        }

        // Group text lines into paragraph blocks.
        // Key invariant: two lines can only merge when their X-ranges overlap,
        // preventing text from separate columns being combined.
        var blocks = new List<TextBlock>();
        TextBlock? cur = null;

        foreach (var line in textLines)
        {
            if (cur is null) { cur = new TextBlock([line]); continue; }

            var   lastLine    = cur.Lines[^1];
            double gap        = line.Y - lastLine.Y;
            double prevFs     = lastLine.DomFs;
            bool   sameBaseline = gap < 2;
            bool   columnGap    = line.Left - lastLine.Right > 28;

            if (sameBaseline && columnGap)
            {
                // Same Y, different column → always split
                blocks.Add(cur);
                cur = new TextBlock([line]);
            }
            else if (gap < Math.Max(prevFs, 4) * ParaGapFactor)
            {
                // Small Y gap: only merge when X ranges overlap
                bool xOverlaps = line.Left  < cur.Right + 20 &&
                                 line.Right > cur.Left  - 20;
                if (xOverlaps) cur.Lines.Add(line);
                else { blocks.Add(cur); cur = new TextBlock([line]); }
            }
            else
            {
                blocks.Add(cur);
                cur = new TextBlock([line]);
            }
        }
        if (cur is not null) blocks.Add(cur);

        return blocks;
    }

    // ── Stage 6: Canvas Element Assembly ─────────────────────────────────────

    private static ElementDto? BlockToDto(TextBlock block, int pg, ref int seq)
    {
        var all = block.Lines.SelectMany(l => l.Runs).ToList();
        if (all.Count == 0) return null;

        string text = string.Join("\n", block.Lines.Select(l => JoinLineRuns(l.Runs))).Trim();
        if (string.IsNullOrWhiteSpace(text)) return null;

        double domFs  = all.Max(r => r.FontSize);
        double x      = block.Left;
        double w      = Math.Max(block.Right - block.Left, 20);
        double elemY  = block.Top  - domFs;
        double elemH  = Math.Max((block.Bottom - block.Top) + domFs * 1.5, domFs * 1.4);

        var domRun = all.MaxBy(r => r.FontSize)!;
        bool uniform = all.All(r =>
            r.Color      == domRun.Color      &&
            r.FontFamily == domRun.FontFamily &&
            Math.Abs(r.FontSize - domRun.FontSize) < 0.5 &&
            r.Bold   == domRun.Bold   &&
            r.Italic == domRun.Italic);

        string domAlign = all
            .GroupBy(r => r.TextAlign)
            .OrderByDescending(g => g.Count())
            .First().Key;

        var style = new Dictionary<string, object>
        {
            ["fontSize"]   = Math.Round(domFs, 1),
            ["fontFamily"] = domRun.FontFamily,
            ["color"]      = domRun.Color,
            ["fontWeight"] = domRun.Bold   ? (object)"bold"   : "normal",
            ["fontStyle"]  = domRun.Italic ? (object)"italic" : "normal",
        };
        if (domAlign != "left") style["textAlign"] = domAlign;

        if (uniform)
        {
            return new ElementDto
            {
                Id      = $"p-{pg}-{seq++}", Type   = "text",
                X       = Math.Round(x, 1),  Y      = Math.Round(elemY, 1),
                Width   = Math.Round(w, 1),  Height = Math.Round(elemH + 4, 1),
                Content = text,
                Style   = style,
            };
        }

        var html = new StringBuilder();
        for (int li = 0; li < block.Lines.Count; li++)
        {
            if (li > 0) html.Append("<br/>");
            var lineRuns = block.Lines[li].Runs;
            for (int ri = 0; ri < lineRuns.Count; ri++)
            {
                var r = lineRuns[ri];
                if (ri > 0)
                {
                    double gap = r.X - lineRuns[ri - 1].EndX;
                    if (gap > 1.5 &&
                        !lineRuns[ri - 1].Text.EndsWith(' ') &&
                        !r.Text.StartsWith(' '))
                        html.Append(' ');
                }
                string sp = SpanStyle(r, domRun);
                html.Append(string.IsNullOrEmpty(sp)
                    ? WebUtility.HtmlEncode(r.Text)
                    : $"<span style=\"{sp}\">{WebUtility.HtmlEncode(r.Text)}</span>");
            }
        }

        return new ElementDto
        {
            Id          = $"p-{pg}-{seq++}", Type   = "richtext",
            X           = Math.Round(x, 1),  Y      = Math.Round(elemY, 1),
            Width       = Math.Round(w, 1),  Height = Math.Round(elemH + 4, 1),
            HtmlContent = html.ToString(),
            Style       = style,
        };
    }

    private static ElementDto ShapeToDto(SvgShape s, int pg, ref int seq)
    {
        if (s.IsLine)
        {
            string col = s.Fill != "transparent" ? s.Fill : s.Stroke;
            return new ElementDto
            {
                Id     = $"ln-{pg}-{seq++}", Type   = "rect",
                X      = Math.Round(s.X, 1), Y      = Math.Round(s.Y, 1),
                Width  = Math.Max(1, Math.Round(s.W, 1)),
                Height = Math.Max(1, Math.Round(s.H, 1)),
                Style  = new Dictionary<string, object>
                    { ["backgroundColor"] = col, ["borderWidth"] = 0 },
            };
        }

        return new ElementDto
        {
            Id     = $"sh-{pg}-{seq++}", Type   = s.Kind,
            X      = Math.Round(s.X, 1), Y      = Math.Round(s.Y, 1),
            Width  = Math.Round(s.W, 1), Height = Math.Round(s.H, 1),
            Style  = new Dictionary<string, object>
            {
                ["backgroundColor"] = s.Fill,
                ["borderColor"]     = s.Stroke,
                ["borderWidth"]     = (int)Math.Max(0, Math.Round(s.StrokeWidth)),
                ["borderStyle"]     = "solid",
                ["borderRadius"]    = (int)Math.Max(0, Math.Round(s.Rx)),
            },
        };
    }

    private static ElementDto ImageToDto(SvgImage img, int pg, ref int seq) =>
        new ElementDto
        {
            Id      = $"img-{pg}-{seq++}", Type = "image",
            X       = Math.Max(0, Math.Round(img.X, 1)),
            Y       = Math.Max(0, Math.Round(img.Y, 1)),
            Width   = Math.Round(img.W, 1),
            Height  = Math.Round(img.H, 1),
            Content = img.Href,
            Style   = new Dictionary<string, object> { ["fitMode"] = "contain" },
        };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string JoinLineRuns(List<SvgTextRun> runs)
    {
        if (runs.Count == 0) return "";
        var sb = new StringBuilder(runs[0].Text);
        for (int i = 1; i < runs.Count; i++)
        {
            double gap = runs[i].X - runs[i - 1].EndX;
            if (gap > 1.5 &&
                !runs[i - 1].Text.EndsWith(' ') &&
                !runs[i].Text.StartsWith(' '))
                sb.Append(' ');
            sb.Append(runs[i].Text);
        }
        return sb.ToString();
    }

    private static string SpanStyle(SvgTextRun r, SvgTextRun dom)
    {
        var sb = new StringBuilder();
        if (r.Bold    != dom.Bold)        sb.Append(r.Bold    ? "font-weight:bold;"  : "font-weight:normal;");
        if (r.Italic  != dom.Italic)      sb.Append(r.Italic  ? "font-style:italic;" : "font-style:normal;");
        if (r.Color   != dom.Color)       sb.Append($"color:{r.Color};");
        if (r.FontFamily != dom.FontFamily) sb.Append($"font-family:'{r.FontFamily}';");
        if (Math.Abs(r.FontSize - dom.FontSize) > 0.5) sb.Append($"font-size:{r.FontSize}px;");
        return sb.ToString();
    }

    private static (double W, double H) ParseViewBox(XElement root)
    {
        var parts = ((string?)root.Attribute("viewBox") ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 4 &&
            double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double w) &&
            double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out double h))
            return (w, h);
        return (595, 842);
    }

    private static Dictionary<string, string> InlineStyle(XElement el)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? s = (string?)el.Attribute("style");
        if (s is null) return d;
        foreach (var part in s.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int i = part.IndexOf(':');
            if (i >= 0) d[part[..i].Trim()] = part[(i + 1)..].Trim();
        }
        return d;
    }

    private static readonly HashSet<string> InheritableProps =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "fill", "stroke", "stroke-width", "stroke-dasharray",
            "font-family", "font-size", "font-weight", "font-style",
            "text-anchor", "color", "opacity", "letter-spacing",
        };

    private static readonly Regex CssClassRule =
        new(@"\.([\w-]+)\s*\{([^}]*)\}", RegexOptions.Compiled);

    private static readonly ConditionalWeakTable<
        XElement, Dictionary<string, Dictionary<string, string>>> CssCache = new();

    private static Dictionary<string, Dictionary<string, string>> GetCssRules(XElement svgRoot) =>
        CssCache.GetValue(svgRoot, root =>
        {
            var rules = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var styleEl in root.Descendants().Where(e => e.Name.LocalName == "style"))
            {
                foreach (Match m in CssClassRule.Matches(styleEl.Value ?? ""))
                {
                    var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var decl in m.Groups[2].Value.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        int ci = decl.IndexOf(':');
                        if (ci > 0) props[decl[..ci].Trim()] = decl[(ci + 1)..].Trim();
                    }
                    if (props.Count > 0) rules[m.Groups[1].Value] = props;
                }
            }
            return rules;
        });

    // CSS cascade: class rules (lowest) → XML attributes → inline style (highest).
    // Walks the ancestor chain root-first so child overrides parent.
    private static Dictionary<string, string> EffectiveStyle(
        XElement el, Dictionary<string, Dictionary<string, string>> cssRules)
    {
        var chain = new List<XElement>();
        XElement? cur = el;
        while (cur != null)
        {
            chain.Add(cur);
            if (cur.Name.LocalName == "svg") break;
            cur = cur.Parent as XElement;
        }
        chain.Reverse();

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in chain)
        {
            string? cls = (string?)node.Attribute("class");
            if (cls != null)
                foreach (var cn in cls.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    if (cssRules.TryGetValue(cn, out var cp))
                        foreach (var (k, v) in cp) result[k] = v;

            foreach (var attr in node.Attributes())
                if (InheritableProps.Contains(attr.Name.LocalName))
                    result[attr.Name.LocalName] = attr.Value;

            foreach (var (k, v) in InlineStyle(node)) result[k] = v;
        }
        return result;
    }

    private static string EGet(Dictionary<string, string> es, string prop, string def)
        => es.TryGetValue(prop, out var v) && !string.IsNullOrWhiteSpace(v) ? v : def;

    private static double DA(XElement el, string attr) => N((string?)el.Attribute(attr));

    private static double N(string? s)
    {
        if (s is null) return 0;
        s = s.Trim();
        if (s.EndsWith("px", StringComparison.OrdinalIgnoreCase)) s = s[..^2];
        else if (s.EndsWith("pt", StringComparison.OrdinalIgnoreCase)) s = s[..^2];
        return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static double ParseFs(string? s) =>
        string.IsNullOrWhiteSpace(s) ? 0 : N(s) is > 0 and var v ? v : 0;

    private static readonly Regex RgbRegex =
        new(@"rgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)", RegexOptions.Compiled);

    private static string HexColor(string? c)
    {
        if (string.IsNullOrWhiteSpace(c) || c is "none" or "transparent") return "transparent";
        if (c.StartsWith('#'))
            return c.Length == 4
                ? "#" + string.Concat(c[1..].Select(ch => $"{ch}{ch}"))
                : c.ToUpperInvariant();
        var m = RgbRegex.Match(c);
        if (m.Success)
            return $"#{int.Parse(m.Groups[1].Value):X2}" +
                   $"{int.Parse(m.Groups[2].Value):X2}" +
                   $"{int.Parse(m.Groups[3].Value):X2}";
        return c;
    }

    private static string CleanFont(string? ff)
    {
        if (string.IsNullOrWhiteSpace(ff)) return "Arial";
        ff = ff.Trim().Trim('\'').Trim('"');
        int plus = ff.IndexOf('+');
        if (plus is >= 1 and < 8) ff = ff[(plus + 1)..];
        return ff;
    }

    private static readonly Regex PathCmdSplit =
        new(@"(?=[MmLlHhVvCcSsQqTtAaZz])", RegexOptions.Compiled);

    private static readonly Regex PathNumRegex =
        new(@"[+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?", RegexOptions.Compiled);

    private static bool TryParseBounds(string d,
        out double minX, out double minY, out double maxX, out double maxY)
    {
        double bx0 = double.MaxValue, by0 = double.MaxValue;
        double bx1 = double.MinValue, by1 = double.MinValue;
        double cx = 0, cy = 0;

        static double[] Ns(string s) => PathNumRegex.Matches(s)
            .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();

        void T(double x, double y)
        {
            if (x < bx0) bx0 = x; if (x > bx1) bx1 = x;
            if (y < by0) by0 = y; if (y > by1) by1 = y;
        }

        foreach (var part in PathCmdSplit.Split(d.Trim()))
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            char cmd = part[0];
            double[] ns = Ns(part.Length > 1 ? part[1..] : "");
            int i = 0;
            switch (cmd)
            {
                case 'M': case 'L':
                    while (i + 1 < ns.Length) { cx = ns[i]; cy = ns[i+1]; T(cx,cy); i += 2; } break;
                case 'm': case 'l':
                    while (i + 1 < ns.Length) { cx += ns[i]; cy += ns[i+1]; T(cx,cy); i += 2; } break;
                case 'H': while (i < ns.Length) { cx = ns[i++]; T(cx,cy); } break;
                case 'h': while (i < ns.Length) { cx += ns[i++]; T(cx,cy); } break;
                case 'V': while (i < ns.Length) { cy = ns[i++]; T(cx,cy); } break;
                case 'v': while (i < ns.Length) { cy += ns[i++]; T(cx,cy); } break;
                case 'C':
                    while (i + 5 < ns.Length)
                    { T(ns[i],ns[i+1]); T(ns[i+2],ns[i+3]); cx=ns[i+4]; cy=ns[i+5]; T(cx,cy); i+=6; } break;
                case 'c':
                    while (i + 5 < ns.Length)
                    { T(cx+ns[i],cy+ns[i+1]); T(cx+ns[i+2],cy+ns[i+3]); cx+=ns[i+4]; cy+=ns[i+5]; T(cx,cy); i+=6; } break;
                case 'S':
                    while (i + 3 < ns.Length)
                    { T(ns[i],ns[i+1]); cx=ns[i+2]; cy=ns[i+3]; T(cx,cy); i+=4; } break;
                case 's':
                    while (i + 3 < ns.Length)
                    { T(cx+ns[i],cy+ns[i+1]); cx+=ns[i+2]; cy+=ns[i+3]; T(cx,cy); i+=4; } break;
                case 'Q':
                    while (i + 3 < ns.Length)
                    { T(ns[i],ns[i+1]); cx=ns[i+2]; cy=ns[i+3]; T(cx,cy); i+=4; } break;
                case 'q':
                    while (i + 3 < ns.Length)
                    { T(cx+ns[i],cy+ns[i+1]); cx+=ns[i+2]; cy+=ns[i+3]; T(cx,cy); i+=4; } break;
                case 'A':
                    while (i + 6 < ns.Length) { cx=ns[i+5]; cy=ns[i+6]; T(cx,cy); i+=7; } break;
                case 'a':
                    while (i + 6 < ns.Length) { cx+=ns[i+5]; cy+=ns[i+6]; T(cx,cy); i+=7; } break;
            }
        }

        minX = bx0; minY = by0; maxX = bx1; maxY = by1;
        return bx0 != double.MaxValue;
    }
}
