using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PXA.Core.Contracts;

namespace PXA.FileImporter;

/// <summary>
/// Converts an SVG file into a <see cref="DesignExportDto"/> with full vector fidelity.
/// Simple shapes (rect) become Canvas shape elements; text becomes text elements;
/// embedded images become image elements; all other vector primitives (path, circle,
/// ellipse, line, polyline, polygon) are emitted as inline SVG data-URI image elements,
/// preserving their exact visual appearance with all transforms applied.
/// </summary>
public sealed class SvgFileImporter : IFileImporter
{
    public IReadOnlyList<string> SupportedExtensions { get; } = ["svg"];

    public Task<DesignExportDto> ImportAsync(Stream stream, string? name = null) =>
        Task.FromResult(Import(stream, name));

    private static readonly XNamespace XlinkNs = "http://www.w3.org/1999/xlink";

    private const double DefaultPageWidth  = 800;
    private const double DefaultPageHeight = 600;

    public static DesignExportDto Import(Stream stream, string? name = null)
    {
        var doc  = XDocument.Load(stream);
        var root = doc.Root ?? throw new InvalidDataException("Not a valid SVG file.");

        var (pageW, pageH) = GetPageSize(root);

        var defs = new Dictionary<string, XElement>(StringComparer.Ordinal);
        CollectDefs(root, defs);

        var elements = new List<ElementDto>();
        int seq = 0;
        ProcessElement(root, SvgMatrix.Identity, defs, elements, ref seq, pageW, pageH, isRoot: true);

        return new DesignExportDto
        {
            Id             = Guid.NewGuid().ToString("N")[..12],
            Name           = name ?? "Imported SVG",
            Pages          = [new PageDto { Id = "page-1", Elements = elements }],
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

    // ── Page dimensions ───────────────────────────────────────────────────────

    private static (double w, double h) GetPageSize(XElement root)
    {
        var vb = Attr(root, "viewBox");
        if (!string.IsNullOrEmpty(vb))
        {
            var parts = vb.Trim().Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4 &&
                TryParseDouble(parts[2], out var vbW) &&
                TryParseDouble(parts[3], out var vbH) &&
                vbW > 0 && vbH > 0)
                return (vbW, vbH);
        }
        return (
            ParseLengthAttr(root, "width",  DefaultPageWidth),
            ParseLengthAttr(root, "height", DefaultPageHeight));
    }

    // ── Defs collection ───────────────────────────────────────────────────────

    private static void CollectDefs(XElement root, Dictionary<string, XElement> defs)
    {
        foreach (var el in root.Descendants())
        {
            var id = Attr(el, "id");
            if (!string.IsNullOrEmpty(id))
                defs.TryAdd(id, el);
        }
    }

    // ── Element traversal ─────────────────────────────────────────────────────

    private static void ProcessElement(
        XElement el,
        SvgMatrix ctm,
        Dictionary<string, XElement> defs,
        List<ElementDto> elements,
        ref int seq,
        double pageW,
        double pageH,
        bool isRoot = false)
    {
        var localName = el.Name.LocalName;

        var localTransform = ParseTransform(Attr(el, "transform"));
        var currentCtm     = ctm.Multiply(localTransform);

        switch (localName)
        {
            case "svg" when isRoot:
            case "g":
                foreach (var child in el.Elements())
                    ProcessElement(child, currentCtm, defs, elements, ref seq, pageW, pageH);
                break;

            case "use":
                ProcessUse(el, currentCtm, defs, elements, ref seq, pageW, pageH);
                break;

            case "defs":
            case "title":
            case "desc":
            case "metadata":
            case "style":
            case "script":
            case "symbol":
                break;

            case "rect":
                EmitRect(el, currentCtm, elements, ref seq);
                break;

            case "text":
                EmitText(el, currentCtm, elements, ref seq);
                break;

            case "image":
                EmitImage(el, currentCtm, elements, ref seq);
                break;

            case "circle":
            case "ellipse":
            case "line":
            case "polyline":
            case "polygon":
            case "path":
                EmitVectorAsInlineSvg(el, currentCtm, elements, ref seq);
                break;

            default:
                foreach (var child in el.Elements())
                    ProcessElement(child, currentCtm, defs, elements, ref seq, pageW, pageH);
                break;
        }
    }

    private static void ProcessUse(
        XElement useEl,
        SvgMatrix ctm,
        Dictionary<string, XElement> defs,
        List<ElementDto> elements,
        ref int seq,
        double pageW,
        double pageH)
    {
        var href = Attr(useEl, "href");
        if (string.IsNullOrEmpty(href))
            href = (string?)useEl.Attribute(XlinkNs + "href") ?? "";

        var id = href.TrimStart('#');
        if (string.IsNullOrEmpty(id) || !defs.TryGetValue(id, out var target)) return;

        var dx         = ParseLength(Attr(useEl, "x"), 0);
        var dy         = ParseLength(Attr(useEl, "y"), 0);
        var usedMatrix = SvgMatrix.Identity.Translate(dx, dy);
        var composedCtm = ctm.Multiply(usedMatrix);

        if (target.Name.LocalName == "symbol")
        {
            foreach (var child in target.Elements())
                ProcessElement(child, composedCtm, defs, elements, ref seq, pageW, pageH);
        }
        else
        {
            ProcessElement(target, composedCtm, defs, elements, ref seq, pageW, pageH);
        }
    }

    // ── Element emitters ─────────────────────────────────────────────────────

    private static void EmitRect(XElement el, SvgMatrix ctm, List<ElementDto> elements, ref int seq)
    {
        var lx = ParseLength(Attr(el, "x"), 0);
        var ly = ParseLength(Attr(el, "y"), 0);
        var lw = ParseLength(Attr(el, "width"),  0);
        var lh = ParseLength(Attr(el, "height"), 0);
        if (lw <= 0 || lh <= 0) return;

        var (px, py, pw, ph) = ctm.TransformBounds(lx, ly, lw, lh);
        if (pw < 0.5 || ph < 0.5) return;

        var fill   = ResolveColor(el, "fill",   "#000000");
        var stroke = ResolveColor(el, "stroke", "transparent");
        var sw     = ParseStrokeWidth(el);

        elements.Add(new ElementDto
        {
            Id     = $"rect-{seq++}", Type   = "shape",
            X      = Math.Round(px, 1),  Y    = Math.Round(py, 1),
            Width  = Math.Round(pw, 1),  Height = Math.Round(ph, 1),
            Style  = new Dictionary<string, object>
            {
                ["backgroundColor"] = fill,
                ["borderColor"]     = stroke,
                ["borderWidth"]     = sw,
                ["borderStyle"]     = "solid",
            },
        });
    }

    private static void EmitText(XElement el, SvgMatrix ctm, List<ElementDto> elements, ref int seq)
    {
        var text = string.Concat(el.DescendantNodes().OfType<XText>().Select(t => t.Value)).Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        var lx = ParseLength(Attr(el, "x"), 0);
        var ly = ParseLength(Attr(el, "y"), 0);
        var fs = ParseLength(Attr(el, "font-size") ?? ResolveStyleProp(el, "font-size"), 16);
        if (fs < 1) fs = 16;

        // SVG y is the text baseline; shift up to get top-left
        ly -= fs;
        var estimatedW = text.Length * fs * 0.6;
        var estimatedH = fs * 1.4;

        var (px, py, pw, ph) = ctm.TransformBounds(lx, ly, estimatedW, estimatedH);

        var fill       = ResolveColor(el, "fill", "#000000");
        var fontFamily = (Attr(el, "font-family") ?? ResolveStyleProp(el, "font-family") ?? "Arial").Trim('\'', '"');
        var fontWeight = Attr(el, "font-weight")  ?? ResolveStyleProp(el, "font-weight") ?? "normal";
        var fontStyle  = Attr(el, "font-style")   ?? ResolveStyleProp(el, "font-style")  ?? "normal";
        var textAnchor = Attr(el, "text-anchor")  ?? "start";
        var align      = textAnchor switch { "middle" => "center", "end" => "right", _ => "left" };

        elements.Add(new ElementDto
        {
            Id      = $"txt-{seq++}", Type = "text",
            X       = Math.Round(px, 1),   Y      = Math.Round(py, 1),
            Width   = Math.Round(pw, 1),   Height = Math.Round(ph, 1),
            Content = text,
            Style   = new Dictionary<string, object>
            {
                ["fontSize"]   = Math.Round(fs, 1),
                ["fontFamily"] = fontFamily,
                ["fontWeight"] = fontWeight,
                ["fontStyle"]  = fontStyle,
                ["color"]      = fill,
                ["textAlign"]  = align,
            },
        });
    }

    private static void EmitImage(XElement el, SvgMatrix ctm, List<ElementDto> elements, ref int seq)
    {
        var lx = ParseLength(Attr(el, "x"), 0);
        var ly = ParseLength(Attr(el, "y"), 0);
        var lw = ParseLength(Attr(el, "width"),  0);
        var lh = ParseLength(Attr(el, "height"), 0);
        if (lw <= 0 || lh <= 0) return;

        var href = Attr(el, "href");
        if (string.IsNullOrEmpty(href))
            href = (string?)el.Attribute(XlinkNs + "href") ?? "";
        if (string.IsNullOrEmpty(href)) return;

        var (px, py, pw, ph) = ctm.TransformBounds(lx, ly, lw, lh);

        elements.Add(new ElementDto
        {
            Id      = $"img-{seq++}", Type = "image",
            X       = Math.Round(px, 1),   Y    = Math.Round(py, 1),
            Width   = Math.Round(pw, 1),   Height = Math.Round(ph, 1),
            Content = href,
            FitMode = "contain",
            Style   = new Dictionary<string, object>(),
        });
    }

    private static void EmitVectorAsInlineSvg(
        XElement el,
        SvgMatrix ctm,
        List<ElementDto> elements,
        ref int seq)
    {
        var (bx, by, bw, bh) = GetElementLocalBounds(el);
        if (bw < 0.5 || bh < 0.5) return;

        var (px, py, pw, ph) = ctm.TransformBounds(bx, by, bw, bh);
        if (pw < 0.5 || ph < 0.5) return;

        // The element's coordinates are in local space; apply CTM as its transform.
        // viewBox spans the page-space bounding box so the SVG clips to just this element.
        var matrixVal = $"matrix({N(ctm.A)},{N(ctm.B)},{N(ctm.C)},{N(ctm.D)},{N(ctm.E)},{N(ctm.F)})";
        var elSvg     = BuildSvgElementString(el, overrideTransform: matrixVal);

        var svgContent = string.Create(
            CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{N(px)} {N(py)} {N(pw)} {N(ph)}\">{elSvg}</svg>");
        var dataUri = "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svgContent));

        elements.Add(new ElementDto
        {
            Id      = $"vec-{seq++}", Type = "image",
            X       = Math.Round(px, 1),   Y    = Math.Round(py, 1),
            Width   = Math.Round(pw, 1),   Height = Math.Round(ph, 1),
            Content = dataUri,
            FitMode = "fill",
            Style   = new Dictionary<string, object> { ["svgVector"] = true },
        });
    }

    // ── SVG element serializer (local names only, no namespace declarations) ──

    private static string BuildSvgElementString(XElement el, string? overrideTransform = null)
    {
        var sb = new StringBuilder();
        sb.Append('<').Append(el.Name.LocalName);
        foreach (var attr in el.Attributes())
        {
            if (attr.IsNamespaceDeclaration) continue;
            var attrName = attr.Name.LocalName;
            if (attrName == "id") continue; // strip ids from inner elements
            var attrValue = attrName == "transform" && overrideTransform is not null
                ? overrideTransform
                : attr.Value;
            sb.Append(' ').Append(attrName).Append("=\"").Append(XmlEscape(attrValue)).Append('"');
        }
        if (overrideTransform is not null && !el.Attributes().Any(a => a.Name.LocalName == "transform"))
            sb.Append(" transform=\"").Append(overrideTransform).Append('"');

        if (!el.HasElements && string.IsNullOrEmpty(el.Value))
        {
            sb.Append("/>");
        }
        else
        {
            sb.Append('>');
            foreach (var node in el.Nodes())
            {
                if (node is XElement child)
                    sb.Append(BuildSvgElementString(child));
                else if (node is XText text)
                    sb.Append(XmlEscape(text.Value));
            }
            sb.Append("</").Append(el.Name.LocalName).Append('>');
        }
        return sb.ToString();
    }

    private static string XmlEscape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    // ── Local bounds per element type ─────────────────────────────────────────

    private static (double x, double y, double w, double h) GetElementLocalBounds(XElement el)
    {
        var sw = (double)ParseStrokeWidth(el);
        switch (el.Name.LocalName)
        {
            case "circle":
            {
                var cx  = ParseLength(Attr(el, "cx"), 0);
                var cy  = ParseLength(Attr(el, "cy"), 0);
                var r   = ParseLength(Attr(el, "r"),  0);
                var pad = r + sw / 2d;
                return (cx - pad, cy - pad, pad * 2, pad * 2);
            }
            case "ellipse":
            {
                var cx = ParseLength(Attr(el, "cx"), 0);
                var cy = ParseLength(Attr(el, "cy"), 0);
                var rx = ParseLength(Attr(el, "rx"), 0);
                var ry = ParseLength(Attr(el, "ry"), 0);
                return (cx - rx - sw / 2, cy - ry - sw / 2, rx * 2 + sw, ry * 2 + sw);
            }
            case "line":
            {
                var x1 = ParseLength(Attr(el, "x1"), 0);
                var y1 = ParseLength(Attr(el, "y1"), 0);
                var x2 = ParseLength(Attr(el, "x2"), 0);
                var y2 = ParseLength(Attr(el, "y2"), 0);
                var minX = Math.Min(x1, x2) - sw / 2;
                var minY = Math.Min(y1, y2) - sw / 2;
                var maxX = Math.Max(x1, x2) + sw / 2;
                var maxY = Math.Max(y1, y2) + sw / 2;
                return (minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
            }
            case "polyline":
            case "polygon":
            {
                var pts = ParsePointsList(Attr(el, "points") ?? "");
                if (pts.Count == 0) return (0, 0, 0, 0);
                var minX = pts.Min(p => p.x) - sw / 2;
                var minY = pts.Min(p => p.y) - sw / 2;
                var maxX = pts.Max(p => p.x) + sw / 2;
                var maxY = pts.Max(p => p.y) + sw / 2;
                return (minX, minY, Math.Max(0.5, maxX - minX), Math.Max(0.5, maxY - minY));
            }
            case "path":
            {
                var pts = ExtractPathCoords(Attr(el, "d") ?? "");
                if (pts.Count == 0) return (0, 0, 1, 1);
                var minX = pts.Min(p => p.x) - sw / 2;
                var minY = pts.Min(p => p.y) - sw / 2;
                var maxX = pts.Max(p => p.x) + sw / 2;
                var maxY = pts.Max(p => p.y) + sw / 2;
                return (minX, minY, Math.Max(0.5, maxX - minX), Math.Max(0.5, maxY - minY));
            }
            default:
                return (0, 0, 0, 0);
        }
    }

    // ── Path coordinate extraction ────────────────────────────────────────────

    private static List<(double x, double y)> ExtractPathCoords(string d)
    {
        var result = new List<(double x, double y)>();
        if (string.IsNullOrWhiteSpace(d)) return result;

        var tokens = Regex.Split(d.Trim(), @"(?=[MmZzLlHhVvCcSsQqTtAa])");
        double curX = 0, curY = 0;

        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token)) continue;
            var cmd  = token[0];
            var nums = ParseNumbers(token.AsSpan(1));
            var rel  = char.IsLower(cmd);

            switch (char.ToUpperInvariant(cmd))
            {
                case 'M':
                case 'L':
                case 'T':
                    for (int i = 0; i + 1 < nums.Count; i += 2)
                    {
                        curX = rel ? curX + nums[i]     : nums[i];
                        curY = rel ? curY + nums[i + 1] : nums[i + 1];
                        result.Add((curX, curY));
                    }
                    break;
                case 'C':
                    for (int i = 0; i + 5 < nums.Count; i += 6)
                    {
                        result.Add(rel ? (curX + nums[i],     curY + nums[i + 1]) : (nums[i],     nums[i + 1]));
                        result.Add(rel ? (curX + nums[i + 2], curY + nums[i + 3]) : (nums[i + 2], nums[i + 3]));
                        curX = rel ? curX + nums[i + 4] : nums[i + 4];
                        curY = rel ? curY + nums[i + 5] : nums[i + 5];
                        result.Add((curX, curY));
                    }
                    break;
                case 'S':
                case 'Q':
                    for (int i = 0; i + 3 < nums.Count; i += 4)
                    {
                        result.Add(rel ? (curX + nums[i],     curY + nums[i + 1]) : (nums[i],     nums[i + 1]));
                        curX = rel ? curX + nums[i + 2] : nums[i + 2];
                        curY = rel ? curY + nums[i + 3] : nums[i + 3];
                        result.Add((curX, curY));
                    }
                    break;
                case 'H':
                    foreach (var v in nums)
                    {
                        curX = rel ? curX + v : v;
                        result.Add((curX, curY));
                    }
                    break;
                case 'V':
                    foreach (var v in nums)
                    {
                        curY = rel ? curY + v : v;
                        result.Add((curX, curY));
                    }
                    break;
                case 'A':
                    for (int i = 0; i + 6 < nums.Count; i += 7)
                    {
                        // include ellipse radii in rough bounds
                        result.Add(rel ? (curX - nums[i], curY - nums[i + 1]) : (curX - nums[i], curY - nums[i + 1]));
                        result.Add(rel ? (curX + nums[i], curY + nums[i + 1]) : (curX + nums[i], curY + nums[i + 1]));
                        curX = rel ? curX + nums[i + 5] : nums[i + 5];
                        curY = rel ? curY + nums[i + 6] : nums[i + 6];
                        result.Add((curX, curY));
                    }
                    break;
            }
        }
        return result;
    }

    private static List<double> ParseNumbers(ReadOnlySpan<char> s)
    {
        var result  = new List<double>();
        var matches = Regex.Matches(s.ToString(), @"-?[\d]*\.?[\d]+(?:[eE][+-]?[\d]+)?");
        foreach (Match m in matches)
            if (TryParseDouble(m.Value, out var v))
                result.Add(v);
        return result;
    }

    private static List<(double x, double y)> ParsePointsList(string points)
    {
        var result = new List<(double x, double y)>();
        var nums   = ParseNumbers(points.AsSpan());
        for (int i = 0; i + 1 < nums.Count; i += 2)
            result.Add((nums[i], nums[i + 1]));
        return result;
    }

    // ── Transform parsing ─────────────────────────────────────────────────────

    private static SvgMatrix ParseTransform(string? transform)
    {
        if (string.IsNullOrWhiteSpace(transform)) return SvgMatrix.Identity;

        var result  = SvgMatrix.Identity;
        var matches = Regex.Matches(transform, @"(\w+)\s*\(([^)]*)\)");

        foreach (Match m in matches)
        {
            var func = m.Groups[1].Value.ToLowerInvariant();
            var args = ParseNumbers(m.Groups[2].Value.AsSpan());

            SvgMatrix t = func switch
            {
                "matrix" when args.Count >= 6 =>
                    new SvgMatrix(args[0], args[1], args[2], args[3], args[4], args[5]),

                "translate" when args.Count >= 2 => SvgMatrix.Identity.Translate(args[0], args[1]),
                "translate" when args.Count == 1 => SvgMatrix.Identity.Translate(args[0], 0),

                "scale" when args.Count >= 2 => new SvgMatrix(args[0], 0, 0, args[1], 0, 0),
                "scale" when args.Count == 1 => new SvgMatrix(args[0], 0, 0, args[0], 0, 0),

                "rotate" when args.Count >= 3 =>
                    SvgMatrix.Identity.Translate(args[1], args[2])
                        .Multiply(SvgMatrix.Rotate(args[0]))
                        .Multiply(SvgMatrix.Identity.Translate(-args[1], -args[2])),
                "rotate" when args.Count == 1 => SvgMatrix.Rotate(args[0]),

                "skewx" when args.Count >= 1 => new SvgMatrix(1, 0, Math.Tan(args[0] * Math.PI / 180d), 1, 0, 0),
                "skewy" when args.Count >= 1 => new SvgMatrix(1, Math.Tan(args[0] * Math.PI / 180d), 0, 1, 0, 0),

                _ => SvgMatrix.Identity,
            };
            result = result.Multiply(t);
        }
        return result;
    }

    // ── Color resolution ──────────────────────────────────────────────────────

    private static string ResolveColor(XElement el, string property, string fallback)
    {
        var styleVal = ResolveStyleProp(el, property);
        var val      = styleVal ?? Attr(el, property);
        if (string.IsNullOrEmpty(val) || val == "inherit" || val == "currentColor") return fallback;
        if (val == "none" || val == "transparent") return "transparent";
        if (val.StartsWith('#'))
            return val.Length == 4 ? ExpandShortHex(val) : val;
        if (val.StartsWith("rgb", StringComparison.OrdinalIgnoreCase)) return ParseRgb(val);
        return NamedColor(val) ?? fallback;
    }

    private static string ExpandShortHex(string hex) =>
        $"#{hex[1]}{hex[1]}{hex[2]}{hex[2]}{hex[3]}{hex[3]}";

    private static string ParseRgb(string rgb)
    {
        var nums = ParseNumbers(rgb.AsSpan());
        if (nums.Count < 3) return "#000000";
        return $"#{Math.Clamp((int)nums[0], 0, 255):X2}{Math.Clamp((int)nums[1], 0, 255):X2}{Math.Clamp((int)nums[2], 0, 255):X2}";
    }

    private static string? NamedColor(string name) => name.ToLowerInvariant() switch
    {
        "black"   => "#000000", "white"   => "#ffffff", "red"     => "#ff0000",
        "green"   => "#008000", "blue"    => "#0000ff", "yellow"  => "#ffff00",
        "orange"  => "#ffa500", "purple"  => "#800080", "gray"    => "#808080",
        "grey"    => "#808080", "silver"  => "#c0c0c0", "navy"    => "#000080",
        "teal"    => "#008080", "lime"    => "#00ff00", "aqua"    => "#00ffff",
        "cyan"    => "#00ffff", "magenta" => "#ff00ff", "fuchsia" => "#ff00ff",
        "maroon"  => "#800000", "olive"   => "#808000", "brown"   => "#a52a2a",
        "pink"    => "#ffc0cb", "coral"   => "#ff7f50", "salmon"  => "#fa8072",
        "gold"    => "#ffd700", "khaki"   => "#f0e68c", "violet"  => "#ee82ee",
        "indigo"  => "#4b0082", "crimson" => "#dc143c", "tomato"  => "#ff6347",
        _ => null,
    };

    // ── Inline style parsing ──────────────────────────────────────────────────

    private static string? ResolveStyleProp(XElement el, string property)
    {
        var style = Attr(el, "style");
        if (string.IsNullOrEmpty(style)) return null;
        foreach (var decl in style.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = decl.IndexOf(':');
            if (colon < 0) continue;
            if (string.Equals(decl[..colon].Trim(), property, StringComparison.OrdinalIgnoreCase))
                return decl[(colon + 1)..].Trim();
        }
        return null;
    }

    // ── Attribute / length helpers ────────────────────────────────────────────

    private static string? Attr(XElement el, string name) => (string?)el.Attribute(name);

    private static double ParseLengthAttr(XElement el, string attr, double fallback) =>
        ParseLength(Attr(el, attr), fallback);

    private static double ParseLength(string? value, double fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        value = value.Trim();
        if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseDouble(value[..^2], out var v) ? v : fallback;
        }
        if (value.EndsWith("pt", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseDouble(value[..^2], out var v) ? v * 1.3333 : fallback;
        }
        if (value.EndsWith("mm", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseDouble(value[..^2], out var v) ? v * 3.7795 : fallback;
        }
        if (value.EndsWith("cm", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseDouble(value[..^2], out var v) ? v * 37.795 : fallback;
        }
        if (value.EndsWith("in", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseDouble(value[..^2], out var v) ? v * 96 : fallback;
        }
        if (value.EndsWith("em", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith('%'))
        {
            return fallback; // relative units — use fallback
        }
        return TryParseDouble(value, out var plain) ? plain : fallback;
    }

    private static int ParseStrokeWidth(XElement el)
    {
        var sw = ParseLength(Attr(el, "stroke-width") ?? ResolveStyleProp(el, "stroke-width"), 0);
        return (int)Math.Max(0, Math.Round(sw));
    }

    private static bool TryParseDouble(string s, out double value) =>
        double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);

    private static string N(double value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);

    // ── Transform matrix ─────────────────────────────────────────────────────

    private readonly record struct SvgMatrix(double A, double B, double C, double D, double E, double F)
    {
        public static SvgMatrix Identity => new(1, 0, 0, 1, 0, 0);

        public (double x, double y) Transform(double x, double y) =>
            (A * x + C * y + E, B * x + D * y + F);

        public SvgMatrix Translate(double tx, double ty) =>
            Multiply(new SvgMatrix(1, 0, 0, 1, tx, ty));

        public static SvgMatrix Rotate(double degrees)
        {
            var rad = degrees * Math.PI / 180d;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);
            return new SvgMatrix(cos, sin, -sin, cos, 0, 0);
        }

        public SvgMatrix Multiply(SvgMatrix m) => new(
            A * m.A + C * m.B,
            B * m.A + D * m.B,
            A * m.C + C * m.D,
            B * m.C + D * m.D,
            A * m.E + C * m.F + E,
            B * m.E + D * m.F + F);

        public (double x, double y, double w, double h) TransformBounds(
            double x, double y, double w, double h)
        {
            var (x0, y0) = Transform(x,     y);
            var (x1, y1) = Transform(x + w, y);
            var (x2, y2) = Transform(x,     y + h);
            var (x3, y3) = Transform(x + w, y + h);
            var minX = Math.Min(Math.Min(x0, x1), Math.Min(x2, x3));
            var minY = Math.Min(Math.Min(y0, y1), Math.Min(y2, y3));
            var maxX = Math.Max(Math.Max(x0, x1), Math.Max(x2, x3));
            var maxY = Math.Max(Math.Max(y0, y1), Math.Max(y2, y3));
            return (minX, minY, maxX - minX, maxY - minY);
        }
    }
}
