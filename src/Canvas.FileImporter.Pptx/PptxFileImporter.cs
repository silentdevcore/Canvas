using System.Globalization;
using Canvas.Core.Contracts;
using Canvas.FileImporter.Abstractions;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Drawing = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace Canvas.FileImporter.Pptx;

/// <summary>
/// Converts a PowerPoint .pptx file into a <see cref="DesignExportDto"/>.
/// Each slide becomes a page; shapes, text, and pictures are mapped to Canvas elements.
/// Slide backgrounds and theme colors are resolved through the slide → layout → master chain.
/// </summary>
public sealed class PptxFileImporter : IFileImporter
{
    public IReadOnlyList<string> SupportedExtensions { get; } = ["pptx"];

    public Task<DesignExportDto> ImportAsync(Stream stream, string? name = null) =>
        Task.FromResult(Import(stream, name));

    // 1 EMU = 1/914400 inch; at 96 dpi → 1 px = 914400/96 = 9525 EMU
    private const double EmuToPx = 96.0 / 914400.0;

    public static DesignExportDto Import(Stream stream, string? name = null)
    {
        using var pptDoc = PresentationDocument.Open(stream, isEditable: false);
        var presPart = pptDoc.PresentationPart
            ?? throw new InvalidDataException("PPTX has no PresentationPart.");

        var pres = presPart.Presentation;

        // ── Slide dimensions ──────────────────────────────────────────────────
        var slideSize = pres.SlideSize;
        double pageW  = slideSize is not null ? (slideSize.Cx ?? 9144000L) * EmuToPx : 960;
        double pageH  = slideSize is not null ? (slideSize.Cy ?? 6858000L) * EmuToPx : 720;

        // ── Theme color map from first slide master ───────────────────────────
        var themeColors = BuildThemeColorMap(presPart);

        // ── Process slides in presentation order ──────────────────────────────
        var pages = new List<PageDto>();
        int pageNum = 0;

        var slideIdList = pres.SlideIdList;
        if (slideIdList is null) return EmptyDesign(name, pageW, pageH);

        foreach (var slideId in slideIdList.Elements<SlideId>())
        {
            pageNum++;
            var rId = slideId.RelationshipId?.Value;
            if (rId is null) continue;

            var slidePart = (SlidePart)presPart.GetPartById(rId);
            var elements  = new List<ElementDto>();
            int seq       = 0;

            // Background
            var bgColor = ResolveSlideBackground(slidePart, themeColors);
            if (bgColor is not null)
            {
                elements.Add(new ElementDto
                {
                    Id     = $"bg-{pageNum}",
                    Type   = "shape",
                    X      = 0, Y = 0,
                    Width  = Math.Round(pageW, 1),
                    Height = Math.Round(pageH, 1),
                    Style  = new Dictionary<string, object>
                    {
                        ["backgroundColor"] = bgColor,
                        ["borderWidth"]     = 0,
                    },
                });
            }

            // Shape tree
            var shapeTree = slidePart.Slide.CommonSlideData?.ShapeTree;
            if (shapeTree is not null)
                ProcessShapeTree(shapeTree, slidePart, themeColors, pageNum, ref seq, elements);

            pages.Add(new PageDto { Id = $"page-{pageNum}", Elements = elements });
        }

        // ── Metadata ──────────────────────────────────────────────────────────
        var coreProps = pptDoc.PackageProperties;
        string docName = name ?? coreProps.Title ?? "Imported PPTX";

        return new DesignExportDto
        {
            Id             = Guid.NewGuid().ToString("N")[..12],
            Name           = docName,
            Pages          = pages,
            SharedElements = [],
            PageSettings   = new PageSettingsDto
            {
                Width       = Math.Round(pageW, 1),
                Height      = Math.Round(pageH, 1),
                Orientation = pageW > pageH ? "landscape" : "portrait",
                Margins     = new MarginsDto { Top = 0, Right = 0, Bottom = 0, Left = 0 },
            },
        };
    }

    // ── Shape tree processing ─────────────────────────────────────────────────

    private static void ProcessShapeTree(
        P.ShapeTree tree,
        SlidePart slidePart,
        Dictionary<string, string> themeColors,
        int pageNum,
        ref int seq,
        List<ElementDto> elements)
    {
        foreach (var child in tree.Elements())
        {
            switch (child)
            {
                case P.Shape sp:
                    ProcessShape(sp, slidePart, themeColors, pageNum, ref seq, elements);
                    break;
                case P.Picture pic:
                    ProcessPicture(pic, slidePart, pageNum, ref seq, elements);
                    break;
                case P.GroupShape grp:
                    foreach (var inner in grp.Elements())
                    {
                        switch (inner)
                        {
                            case P.Shape sp2:
                                ProcessShape(sp2, slidePart, themeColors, pageNum, ref seq, elements);
                                break;
                            case P.Picture pic2:
                                ProcessPicture(pic2, slidePart, pageNum, ref seq, elements);
                                break;
                        }
                    }
                    break;
                // GraphicFrame (tables/charts) and ConnectionShape are skipped for now
            }
        }
    }

    // ── Shape → text / shape element ─────────────────────────────────────────

    private static void ProcessShape(
        P.Shape sp,
        SlidePart slidePart,
        Dictionary<string, string> themeColors,
        int pageNum,
        ref int seq,
        List<ElementDto> elements)
    {
        var xfrm   = sp.ShapeProperties?.Transform2D;
        var (x, y, w, h) = GetTransformPx(xfrm);
        if (w < 1 || h < 1) return;

        var txBody = sp.TextBody;

        if (txBody is not null)
        {
            // Collect all paragraph runs into one text element per paragraph
            var paras = txBody.Elements<Drawing.Paragraph>().ToList();
            double lineY = y;

            foreach (var para in paras)
            {
                var text = ExtractParaText(para);
                if (string.IsNullOrWhiteSpace(text)) { lineY += 4; continue; }

                var (fs, bold, italic, color, align) = ResolveParaStyle(para, slidePart, themeColors);
                double lineH = fs * 1.35;

                elements.Add(new ElementDto
                {
                    Id      = $"txt-{pageNum}-{seq++}",
                    Type    = "text",
                    X       = Math.Round(x, 1),
                    Y       = Math.Round(lineY, 1),
                    Width   = Math.Round(w, 1),
                    Height  = Math.Round(Math.Max(lineH, fs + 2), 1),
                    Content = text,
                    Style   = new Dictionary<string, object>
                    {
                        ["fontSize"]   = Math.Round(fs, 1),
                        ["fontWeight"] = bold   ? (object)"bold"   : "normal",
                        ["fontStyle"]  = italic ? (object)"italic" : "normal",
                        ["color"]      = color,
                        ["textAlign"]  = align,
                    },
                });
                lineY += lineH;
            }
        }
        else
        {
            // Shape without text → emit as a colored rect/shape
            var fillColor = ResolveShapeFill(sp, slidePart, themeColors);
            if (fillColor is null) return;

            elements.Add(new ElementDto
            {
                Id     = $"sh-{pageNum}-{seq++}",
                Type   = "shape",
                X      = Math.Round(x, 1),
                Y      = Math.Round(y, 1),
                Width  = Math.Round(w, 1),
                Height = Math.Round(h, 1),
                Style  = new Dictionary<string, object>
                {
                    ["backgroundColor"] = fillColor,
                    ["borderWidth"]     = 0,
                },
            });
        }
    }

    // ── Picture → image element ───────────────────────────────────────────────

    private static void ProcessPicture(
        P.Picture pic,
        SlidePart slidePart,
        int pageNum,
        ref int seq,
        List<ElementDto> elements)
    {
        var xfrm   = pic.ShapeProperties?.Transform2D;
        var (x, y, w, h) = GetTransformPx(xfrm);
        if (w < 1 || h < 1) return;

        var blipFill = pic.BlipFill;
        var blip     = blipFill?.Blip;
        var rId      = blip?.Embed?.Value;
        if (rId is null) return;

        try
        {
            var imagePart  = (ImagePart)slidePart.GetPartById(rId);
            using var imgStream = imagePart.GetStream();
            using var ms    = new MemoryStream();
            imgStream.CopyTo(ms);
            var bytes    = ms.ToArray();
            var mime     = imagePart.ContentType;
            var dataUri  = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";

            elements.Add(new ElementDto
            {
                Id      = $"img-{pageNum}-{seq++}",
                Type    = "image",
                X       = Math.Round(x, 1),
                Y       = Math.Round(y, 1),
                Width   = Math.Round(w, 1),
                Height  = Math.Round(h, 1),
                Content = dataUri,
                FitMode = "contain",
                Style   = new Dictionary<string, object>(),
            });
        }
        catch
        {
            // Skip images that cannot be extracted
        }
    }

    // ── Style resolution ──────────────────────────────────────────────────────

    private static (double fs, bool bold, bool italic, string color, string align)
        ResolveParaStyle(
            Drawing.Paragraph para,
            SlidePart slidePart,
            Dictionary<string, string> themeColors)
    {
        double fs    = 18;
        bool   bold  = false;
        bool   italic = false;
        string color  = "#000000";
        string align  = "left";

        var pPr = para.ParagraphProperties;
        if (pPr is not null)
        {
            // Compare InnerText to avoid non-constant enum switch issues ("ctr", "r", "just", etc.)
            align = pPr.Alignment?.InnerText switch
            {
                "ctr"     => "center",
                "r"       => "right",
                "just"    => "justify",
                "justLow" => "justify",
                _         => "left",
            };
        }

        // Use properties from the first run with explicit values
        foreach (var run in para.Elements<Drawing.Run>())
        {
            var rPr = run.RunProperties;
            if (rPr is null) continue;

            if (rPr.FontSize is not null)
                fs = rPr.FontSize.Value / 100.0; // hundredths of a point → pt → treat as px
            if (rPr.Bold?.Value == true)   bold   = true;
            if (rPr.Italic?.Value == true) italic = true;

            var solidFill = rPr.GetFirstChild<SolidFill>();
            if (solidFill is not null)
                color = ResolveColor(solidFill, themeColors);
            break;
        }

        // Fallback: check paragraph-level default run properties
        if (pPr is not null)
        {
            var defRPr = pPr.GetFirstChild<Drawing.DefaultRunProperties>();
            if (defRPr is not null)
            {
                if (defRPr.FontSize is not null && fs == 18)
                    fs = defRPr.FontSize.Value / 100.0;
                var solidFill = defRPr.GetFirstChild<SolidFill>();
                if (solidFill is not null && color == "#000000")
                    color = ResolveColor(solidFill, themeColors);
            }
        }

        return (Math.Max(8, fs), bold, italic, color, align);
    }

    private static string? ResolveShapeFill(
        P.Shape sp,
        SlidePart slidePart,
        Dictionary<string, string> themeColors)
    {
        var spPr = sp.ShapeProperties;

        // solidFill on shape properties
        var solidFill = spPr?.GetFirstChild<SolidFill>();
        if (solidFill is not null) return ResolveColor(solidFill, themeColors);

        // gradFill → use first stop color as approximation
        var gradFill = spPr?.GetFirstChild<GradientFill>();
        if (gradFill is not null)
        {
            var firstStop = gradFill.GradientStopList?.GetFirstChild<Drawing.GradientStop>();
            var stopFill  = firstStop?.GetFirstChild<SolidFill>();
            if (stopFill is not null) return ResolveColor(stopFill, themeColors);
        }

        // No fill or noFill → skip shape
        var noFill = spPr?.GetFirstChild<NoFill>();
        if (noFill is not null) return null;

        return null;
    }

    // ── Background resolution ─────────────────────────────────────────────────

    private static string? ResolveSlideBackground(
        SlidePart slidePart,
        Dictionary<string, string> themeColors)
    {
        // Try slide-level background
        var bg = slidePart.Slide.CommonSlideData?.Background;
        if (bg is not null)
        {
            var bgPr    = bg.GetFirstChild<P.BackgroundProperties>();
            var solidFill = bgPr?.GetFirstChild<SolidFill>();
            if (solidFill is not null) return ResolveColor(solidFill, themeColors);
        }

        // Try layout background
        var layoutPart = slidePart.SlideLayoutPart;
        if (layoutPart is not null)
        {
            var layoutBg = layoutPart.SlideLayout.CommonSlideData?.Background;
            var bgPr     = layoutBg?.GetFirstChild<P.BackgroundProperties>();
            var solidFill = bgPr?.GetFirstChild<SolidFill>();
            if (solidFill is not null) return ResolveColor(solidFill, themeColors);
        }

        // Try master background
        var masterPart = layoutPart?.SlideMasterPart;
        if (masterPart is not null)
        {
            var masterBg  = masterPart.SlideMaster.CommonSlideData?.Background;
            var bgPr      = masterBg?.GetFirstChild<P.BackgroundProperties>();
            var solidFill = bgPr?.GetFirstChild<SolidFill>();
            if (solidFill is not null) return ResolveColor(solidFill, themeColors);
        }

        return null;
    }

    // ── Color resolution ──────────────────────────────────────────────────────

    private static string ResolveColor(SolidFill fill, Dictionary<string, string> themeColors)
    {
        // Explicit hex color
        var rgbHex = fill.RgbColorModelHex;
        if (rgbHex?.Val is not null)
            return $"#{rgbHex.Val.Value}";

        // Explicit percent/hex model
        var rgbPct = fill.RgbColorModelPercentage;
        if (rgbPct is not null)
        {
            int r = (int)Math.Round((rgbPct.RedPortion?.Value ?? 0) / 1000.0 * 255 / 100);
            int g = (int)Math.Round((rgbPct.GreenPortion?.Value ?? 0) / 1000.0 * 255 / 100);
            int b = (int)Math.Round((rgbPct.BluePortion?.Value ?? 0) / 1000.0 * 255 / 100);
            return $"#{Math.Clamp(r, 0, 255):X2}{Math.Clamp(g, 0, 255):X2}{Math.Clamp(b, 0, 255):X2}";
        }

        // Scheme color lookup
        var schemeClr = fill.SchemeColor;
        if (schemeClr?.Val is not null)
        {
            var key = schemeClr.Val.Value.ToString();
            if (themeColors.TryGetValue(key, out var hex))
            {
                // Apply luminance modifiers if present
                var lumMod = schemeClr.GetFirstChild<LuminanceModulation>();
                var lumOff = schemeClr.GetFirstChild<LuminanceOffset>();
                if (lumMod is not null || lumOff is not null)
                    return ApplyLumModifiers(hex, lumMod?.Val?.Value, lumOff?.Val?.Value);
                return $"#{hex}";
            }
        }

        // Preset color
        var presetClr = fill.PresetColor;
        if (presetClr?.Val is not null)
            return PresetColorToHex(presetClr.Val.Value.ToString()) ?? "#000000";

        return "#000000";
    }

    private static string ApplyLumModifiers(string hex, int? lumModPct, int? lumOffPct)
    {
        // Convert hex to RGB
        if (hex.Length != 6) return $"#{hex}";
        if (!int.TryParse(hex[0..2], NumberStyles.HexNumber, null, out int r)) return $"#{hex}";
        if (!int.TryParse(hex[2..4], NumberStyles.HexNumber, null, out int g)) return $"#{hex}";
        if (!int.TryParse(hex[4..6], NumberStyles.HexNumber, null, out int b)) return $"#{hex}";

        // Convert to HLS
        RgbToHls(r / 255.0, g / 255.0, b / 255.0, out double h, out double l, out double s);

        // Apply modifiers (values in thousandths of a percent, i.e. 100000 = 100%)
        double mod = lumModPct.HasValue ? lumModPct.Value / 100000.0 : 1.0;
        double off = lumOffPct.HasValue ? lumOffPct.Value / 100000.0 : 0.0;
        l = Math.Clamp(l * mod + off, 0, 1);

        HlsToRgb(h, l, s, out double nr, out double ng, out double nb);
        return $"#{(int)Math.Round(nr * 255):X2}{(int)Math.Round(ng * 255):X2}{(int)Math.Round(nb * 255):X2}";
    }

    private static void RgbToHls(double r, double g, double b, out double h, out double l, out double s)
    {
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        l = (max + min) / 2;
        if (max == min) { h = s = 0; return; }
        double d = max - min;
        s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        h = max == r ? (g - b) / d + (g < b ? 6 : 0) :
            max == g ? (b - r) / d + 2 :
                       (r - g) / d + 4;
        h /= 6;
    }

    private static void HlsToRgb(double h, double l, double s, out double r, out double g, out double b)
    {
        if (s == 0) { r = g = b = l; return; }
        double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        double p = 2 * l - q;
        r = HlsHelper(p, q, h + 1.0 / 3);
        g = HlsHelper(p, q, h);
        b = HlsHelper(p, q, h - 1.0 / 3);
    }

    private static double HlsHelper(double p, double q, double t)
    {
        if (t < 0) t += 1; if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }

    // ── Theme color map ───────────────────────────────────────────────────────

    private static Dictionary<string, string> BuildThemeColorMap(PresentationPart presPart)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var themePart = presPart.SlideMasterParts
            .SelectMany(mp => mp.ThemePart is not null ? new[] { mp.ThemePart } : [])
            .FirstOrDefault();

        if (themePart is null) return map;

        var colorScheme = themePart.Theme.ThemeElements?.ColorScheme;
        if (colorScheme is null) return map;

        // Map scheme element names to their hex values
        foreach (var el in colorScheme.Elements())
        {
            var colorKey = el.LocalName;
            var hex      = ExtractSchemeHex(el);
            if (hex is not null)
                map[colorKey] = hex;
        }
        return map;
    }

    private static string? ExtractSchemeHex(DocumentFormat.OpenXml.OpenXmlElement el)
    {
        var srgb = el.GetFirstChild<Drawing.RgbColorModelHex>();
        if (srgb?.Val is not null) return srgb.Val.Value;

        var sys = el.GetFirstChild<Drawing.SystemColor>();
        if (sys?.LastColor is not null) return sys.LastColor.Value;

        return null;
    }

    // ── Preset colors (subset) ────────────────────────────────────────────────

    private static string? PresetColorToHex(string name) => name.ToLowerInvariant() switch
    {
        "black"   => "#000000", "white"   => "#ffffff", "red"     => "#ff0000",
        "green"   => "#008000", "blue"    => "#0000ff", "yellow"  => "#ffff00",
        "orange"  => "#ffa500", "purple"  => "#800080", "gray"    => "#808080",
        "grey"    => "#808080", "silver"  => "#c0c0c0", "navy"    => "#000080",
        "teal"    => "#008080", "lime"    => "#00ff00", "aqua"    => "#00ffff",
        "cyan"    => "#00ffff", "magenta" => "#ff00ff", "fuchsia" => "#ff00ff",
        "maroon"  => "#800000", "olive"   => "#808000", "brown"   => "#a52a2a",
        "pink"    => "#ffc0cb", "coral"   => "#ff7f50", "gold"    => "#ffd700",
        "crimson" => "#dc143c", "tomato"  => "#ff6347", "violet"  => "#ee82ee",
        "indigo"  => "#4b0082", "khaki"   => "#f0e68c", "beige"   => "#f5f5dc",
        "ivory"   => "#fffff0", "snow"    => "#fffafa", "azure"   => "#f0ffff",
        _ => null,
    };

    // ── Text extraction ───────────────────────────────────────────────────────

    private static string ExtractParaText(Drawing.Paragraph para)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var run in para.Elements<Drawing.Run>())
            sb.Append(run.Text?.Text ?? "");
        foreach (var fld in para.Elements<Drawing.Field>())
            sb.Append(fld.Text?.Text ?? "");
        return sb.ToString();
    }

    // ── Transform helpers ─────────────────────────────────────────────────────

    private static (double x, double y, double w, double h) GetTransformPx(Transform2D? xfrm)
    {
        if (xfrm is null) return (0, 0, 0, 0);
        double x = (xfrm.Offset?.X?.Value ?? 0L) * EmuToPx;
        double y = (xfrm.Offset?.Y?.Value ?? 0L) * EmuToPx;
        double w = (xfrm.Extents?.Cx?.Value ?? 0L) * EmuToPx;
        double h = (xfrm.Extents?.Cy?.Value ?? 0L) * EmuToPx;
        return (x, y, w, h);
    }

    private static DesignExportDto EmptyDesign(string? name, double w, double h) => new()
    {
        Id             = Guid.NewGuid().ToString("N")[..12],
        Name           = name ?? "Imported PPTX",
        Pages          = [new PageDto { Id = "page-1", Elements = [] }],
        SharedElements = [],
        PageSettings   = new PageSettingsDto
        {
            Width  = Math.Round(w, 1),
            Height = Math.Round(h, 1),
        },
    };
}
