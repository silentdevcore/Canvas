using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Canvas.Pdf;
using QRCoder;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using DesignLayoutPlanner = Canvas.Core.Primitives.DesignLayoutPlanner;

namespace Canvas.WebApi.Infrastructure;

public static class DesignJsonMapper
{
    public static PdfDocument MapToPdfDocument(
        DesignExportDto design,
        PdfFontLoader? fontLoader = null,
        string? targetLanguage = null)
    {
        // Query-param language > JSON body targetLanguage > systemLanguage
        targetLanguage ??= design.PageSettings?.TargetLanguage;

        // Resolve localized property values and apply them as content substitutions.
        var resolvedProps = LocalizedPropertyResolver.Resolve(
            design.PageSettings?.LocalizedProperties,
            targetLanguage ?? design.PageSettings?.SystemLanguage,
            design.PageSettings?.SystemLanguage);

        if (resolvedProps.Count > 0)
            design = ApplyPropertySubstitutions(design, resolvedProps);

        var document = new PdfDocument();
        document.FontLoader = fontLoader;
        document.Info.Title   = design.PageSettings?.Metadata?.Title   ?? design.Name;
        document.Info.Author  = design.PageSettings?.Metadata?.Author  ?? "";
        document.Info.Subject = design.PageSettings?.Metadata?.Subject ?? design.Category ?? "";
        document.Info.Keywords = design.PageSettings?.Metadata?.Keywords ?? "";
        document.Info.Creator = "Canvas PDF Renderer";

        var ps = design.PageSettings ?? new PageSettingsDto();
        var plannedPages = DesignLayoutPlanner.BuildPages(design);
        var totalPages = plannedPages.Count;
        var marginLeft = ps.Margins?.Left ?? 0;
        var marginTop  = ps.Margins?.Top  ?? 0;
        var sourcePages = design.Pages ?? [];

        // Collect elements with a cross-page scope (watermarks, page-number overlays, etc.)
        // These must be rendered on every page that matches their scope, regardless of which
        // page they were originally placed on.
        var effectiveLang = targetLanguage ?? design.PageSettings?.SystemLanguage;
        var scopedElements = sourcePages
            .SelectMany(p => p.Elements ?? [])
            .Where(e => e.Hidden != true && e.PageScope is not (null or "current") &&
                (e.ElementLanguage is null || string.Equals(
                    LocalizedPropertyResolver.NormalizeTag(e.ElementLanguage),
                    LocalizedPropertyResolver.NormalizeTag(effectiveLang ?? ""),
                    StringComparison.OrdinalIgnoreCase)))
            .ToList();

        // ── Add PDF bookmarks BEFORE page rendering so case "toc" can read them ─
        for (var bi = 0; bi < plannedPages.Count; bi++)
        {
            foreach (var bel in plannedPages[bi].Elements.Where(e =>
                e.HeadingLevel is >= 1 and <= 3 &&
                e.Hidden != true))
            {
                var bmTitle = Regex.Replace((bel.Content ?? bel.HtmlContent ?? "").Trim(), "<[^>]+>", "").Trim();
                if (string.IsNullOrWhiteSpace(bmTitle))
                    bmTitle = $"Heading {bel.HeadingLevel}";
                document.AddBookmark(bmTitle, bi + 1, bel.HeadingLevel!.Value);
            }
        }

        for (var pi = 0; pi < plannedPages.Count; pi++)
        {
            var plannedPage = plannedPages[pi];
            var page = document.AddPage(ps.Width, ps.Height);
            var pageNumber = pi + 1;

            // Page background color (skip default white to keep PDF clean)
            if (!string.IsNullOrEmpty(ps.BackgroundColor) && ps.BackgroundColor != "#ffffff")
            {
                var bg = ParseColor(ps.BackgroundColor);
                page.DrawRectangle(0, 0, ps.Width, ps.Height, lineWidth: 0.01, fill: true, strokeColor: bg, fillColor: bg);
            }

            // Page background image (data: URLs only; external URLs are not reachable server-side)
            if (!string.IsNullOrEmpty(ps.BackgroundImage) &&
                ps.BackgroundImage.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var commaIdx = ps.BackgroundImage.IndexOf(',', StringComparison.Ordinal);
                    if (commaIdx >= 0)
                    {
                        var bytes = Convert.FromBase64String(ps.BackgroundImage[(commaIdx + 1)..]);
                        var ext = ps.BackgroundImage.Contains("jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : ".png";
                        var tempPath = Path.ChangeExtension(Path.GetTempFileName(), ext);
                        File.WriteAllBytes(tempPath, bytes);
                        try
                        {
                            switch (ps.BackgroundImageFit)
                            {
                                case "contain":
                                    page.DrawImageFitCenter(tempPath, 0, 0, ps.Width, ps.Height);
                                    break;
                                case "tile":
                                    const double tileSize = 100;
                                    for (var ty = 0.0; ty < ps.Height; ty += tileSize)
                                    for (var tx = 0.0; tx < ps.Width;  tx += tileSize)
                                        page.DrawImage(tempPath, tx, ty, tileSize, tileSize);
                                    break;
                                default:
                                    page.DrawImage(tempPath, 0, 0, ps.Width, ps.Height);
                                    break;
                            }
                        }
                        finally { TryDelete(tempPath); }
                    }
                }
                catch { /* ignore failed background image — page is still usable */ }
            }

            // Element coordinates from the frontend are page-absolute (origin = page top-left).
            // Margins are visual guides only and must NOT be added to element positions.
            // ElementLanguage: if set, only render this element when the target language matches.
            foreach (var el in plannedPage.Elements.Where(e =>
                e.PageScope is null or "current" &&
                (e.ElementLanguage is null || string.Equals(
                    LocalizedPropertyResolver.NormalizeTag(e.ElementLanguage),
                    LocalizedPropertyResolver.NormalizeTag(effectiveLang ?? ""),
                    StringComparison.OrdinalIgnoreCase))))
                RenderElement(document, page, el, ps.Height, pageNumber, totalPages, effectiveLang);

            // Scoped elements: draw on each page that satisfies the scope condition
            foreach (var el in scopedElements)
            {
                if (!MatchesPageScope(el.PageScope, el.PageRange, pageNumber, totalPages)) continue;
                RenderElement(document, page, el, ps.Height, pageNumber, totalPages, effectiveLang);
            }

            // Global watermark from page settings
            if (ps.GlobalWatermark is { } gw && !string.IsNullOrWhiteSpace(gw.Content) &&
                MatchesPageScope(gw.PageScope ?? "all", gw.PageRange, pageNumber, totalPages))
            {
                var wColor = ParseColor(gw.Color ?? "#d1d5db");
                var wFs = gw.FontSize * Math.Max(gw.Scale, 0.1);
                page.DrawText(gw.Content,
                    ps.Width / 2, ps.Height / 2,
                    new PdfDrawTextOptions
                    {
                        FontSize = wFs,
                        FillColor = wColor,
                        RotationDegrees = gw.Rotation,
                    });
            }

            // Global auto page numbering from page settings
            if (ps.PageNumbering is { } pn && (pn.ShowOnFirstPage || pageNumber > 1))
            {
                var num = pageNumber + pn.StartNumber - 1;
                var numText = pn.Format switch
                {
                    "total"       => totalPages.ToString(CultureInfo.InvariantCulture),
                    "pageOfTotal" => $"{num} / {totalPages}",
                    "roman"       => ToRoman(num),
                    "alphabetic"  => ToAlpha(num),
                    _             => num.ToString(CultureInfo.InvariantCulture)
                };
                var pnText = $"{pn.Prefix ?? ""}{numText}{pn.Suffix ?? ""}";
                const double pnFs = 10;
                const double pnPad = 18;
                var (pnX, pnY) = pn.Placement switch
                {
                    "top-left"      => (marginLeft + pnPad,            ps.Height - marginTop - pnPad),
                    "top-center"    => (ps.Width / 2,                  ps.Height - marginTop - pnPad),
                    "top-right"     => (ps.Width - marginLeft - pnPad, ps.Height - marginTop - pnPad),
                    "bottom-left"   => (marginLeft + pnPad,            (ps.Margins?.Bottom ?? 0) + pnPad),
                    "bottom-right"  => (ps.Width - marginLeft - pnPad, (ps.Margins?.Bottom ?? 0) + pnPad),
                    _               => (ps.Width / 2,                  (ps.Margins?.Bottom ?? 0) + pnPad) // bottom-center
                };
                page.DrawText(pnText, pnX, pnY,
                    new PdfDrawTextOptions { FontSize = pnFs, FillColor = ParseColor("#374151") });
            }
        }

        return document;
    }

    // ── Coordinate helpers ───────────────────────────────────────────────────
    // CSS uses top-down Y (0 = page top). PDF uses bottom-up Y (0 = page bottom).

    /// <summary>PDF Y of a text baseline, approximately at the CSS element top.</summary>
    private static double TextY(double pageH, double cssTop, double fontSize) =>
        pageH - cssTop - fontSize * 0.72;

    /// <summary>PDF Y of the bottom-left corner of a rectangle.</summary>
    private static double RectBottomY(double pageH, double cssTop, double height) =>
        pageH - cssTop - height;

    // ── Main renderer ────────────────────────────────────────────────────────

    private static void RenderElement(
        PdfDocument document,
        PdfPage page, ElementDto el, double pageH,
        int pageIndex, int totalPages,
        string? effectiveLang = null)
    {
        var w    = el.Width;
        var h    = el.Height;
        var elX  = el.X;
        var elY  = el.Y;
        var style = el.Style ?? [];

        // Apply per-language position/rotation override when a target language is active.
        if (effectiveLang != null && el.LangOverrides?.Count > 0)
        {
            var key = el.LangOverrides.ContainsKey(effectiveLang)
                ? effectiveLang
                : LocalizedPropertyResolver.NormalizeTag(effectiveLang);
            if (el.LangOverrides.TryGetValue(key, out var ov))
            {
                if (ov.X.HasValue)        elX   = ov.X.Value;
                if (ov.Y.HasValue)        elY   = ov.Y.Value;
                if (ov.Width.HasValue)    w     = ov.Width.Value;
                if (ov.Height.HasValue)   h     = ov.Height.Value;
                if (ov.Rotation.HasValue)
                    style = new Dictionary<string, object>(style) { ["rotation"] = ov.Rotation.Value };
            }
        }

        if (w <= 0 || h <= 0) return;

        switch (el.Type)
        {
            case "text":
            {
                var text = el.Content ?? "";
                if (string.IsNullOrWhiteSpace(text)) return;
                var pad  = GetDouble(style, "padding", 0);
                var padL = GetDouble(style, "paddingLeft",  pad);
                var padT = GetDouble(style, "paddingTop",   pad);
                var padR = GetDouble(style, "paddingRight", pad);
                var opts = BuildParaOptions(style, el.Language, el.TextDirection);

                // Background fill behind text element
                var bgStr = GetString(style, "backgroundColor");
                if (!string.IsNullOrEmpty(bgStr) && bgStr != "transparent")
                {
                    var bgC = ParseColor(bgStr);
                    page.DrawRectangle(elX, RectBottomY(pageH, elY, h), w, h,
                        lineWidth: 0.01, fill: true, strokeColor: bgC, fillColor: bgC);
                }

                // Border around text element
                var borderStr = GetString(style, "borderColor") ?? "";
                var bw = GetDouble(style, "borderWidth", 0);
                if (!string.IsNullOrEmpty(borderStr) && bw > 0)
                {
                    var radius = ClampRadius(ParseRadius(GetString(style, "borderRadius")), w, h);
                    var strokeStyle = BorderStyleToStroke(GetString(style, "borderStyle"), bw);
                    var bColor = ParseColor(borderStr);
                    if (radius > 0)
                        page.DrawRoundedRectangle(elX, RectBottomY(pageH, elY, h), w, h, radius,
                            lineWidth: bw, fill: false, strokeColor: bColor, strokeStyle: strokeStyle);
                    else
                        page.DrawRectangle(elX, RectBottomY(pageH, elY, h), w, h,
                            lineWidth: bw, fill: false, strokeColor: bColor, strokeStyle: strokeStyle);
                }

                var availW = Math.Max(w - padL - padR, 1);
                page.DrawParagraph(text, elX + padL, TextY(pageH, elY + padT, opts.FontSize), availW, opts);
                break;
            }

            case "richtext":
            {
                var html = el.HtmlContent ?? el.Content ?? "";
                if (string.IsNullOrWhiteSpace(html)) return;
                var pad  = GetDouble(style, "padding", 0);
                var padL = GetDouble(style, "paddingLeft",  pad);
                var padT = GetDouble(style, "paddingTop",   pad);
                var padR = GetDouble(style, "paddingRight", pad);
                var opts = BuildParaOptions(style);
                var availW = Math.Max(w - padL - padR, 1);
                var baseColor = ParseColor(GetString(style, "color") ?? "#101828");
                RichTextRenderer.Render(
                    page, html,
                    elX + padL,
                    TextY(pageH, elY + padT, opts.FontSize),
                    availW,
                    opts.FontSize,
                    baseColor,
                    opts.LineHeight);
                break;
            }

            case "date":
            {
                string dateText;
                if (el.DateMode == "render")
                {
                    var now = DateTime.UtcNow;
                    if (!string.IsNullOrEmpty(el.Timezone))
                    {
                        try { now = TimeZoneInfo.ConvertTimeFromUtc(now, TimeZoneInfo.FindSystemTimeZoneById(el.Timezone)); }
                        catch { /* keep UTC */ }
                    }
                    var fmt = el.DateFormat ?? "dd.MM.yyyy";
                    try { dateText = now.ToString(fmt, new CultureInfo(el.Locale ?? "en")); }
                    catch { dateText = now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture); }
                }
                else
                {
                    dateText = el.Content ?? el.FallbackText ?? "";
                }
                if (string.IsNullOrWhiteSpace(dateText)) return;
                var opts = BuildParaOptions(style, el.Language, el.TextDirection);
                page.DrawParagraph(dateText, elX, TextY(pageH, elY, opts.FontSize), w, opts);
                break;
            }

            case "pagenumber":
            {
                var fmt = el.NumberingFormat ?? "current";
                var start = el.StartNumber ?? 1;
                var num = pageIndex + start - 1;
                var numText = fmt switch
                {
                    "total"       => totalPages.ToString(CultureInfo.InvariantCulture),
                    "pageOfTotal" => $"{num} / {totalPages}",
                    "roman"       => ToRoman(num),
                    "alphabetic"  => ToAlpha(num),
                    _             => num.ToString(CultureInfo.InvariantCulture)
                };
                var full = $"{el.Prefix ?? ""}{numText}{el.Suffix ?? ""}";
                if (string.IsNullOrWhiteSpace(full)) return;
                var opts = BuildParaOptions(style, el.Language, el.TextDirection);
                page.DrawParagraph(full, elX, TextY(pageH, elY, opts.FontSize), w, opts);
                break;
            }

            case "field":
            {
                var label = el.FieldLabel ?? el.FieldName ?? "";
                var borderColor = ParseColor(GetString(style, "borderColor") ?? "#d1d5db");
                var labelColor = ParseColor(GetString(style, "color") ?? "#374151");
                var bgStr = GetString(style, "backgroundColor");
                var hasBg = !string.IsNullOrEmpty(bgStr) && bgStr != "transparent";
                var fontSize = GetDouble(style, "fontSize", 11);

                const double labelOffset = 16.0;
                var boxH = Math.Clamp(h - labelOffset, 2, 24.0);
                var boxY = RectBottomY(pageH, elY + labelOffset, boxH);
                page.DrawRectangle(elX, boxY, w, boxH,
                    lineWidth: 1, fill: hasBg,
                    strokeColor: borderColor,
                    fillColor: hasBg ? ParseColor(bgStr!) : PdfColor.White);

                // Placeholder text inside the input box
                var placeholderText = el.FieldName ?? "";
                if (!string.IsNullOrEmpty(placeholderText) && boxH > 10)
                    page.DrawText(placeholderText, elX + 6, TextY(pageH, elY + labelOffset + (boxH - fontSize * 1.2) / 2, fontSize),
                        new PdfDrawTextOptions { FontSize = fontSize, FillColor = ParseColor("#9ca3af"), Italic = true });

                if (!string.IsNullOrEmpty(label))
                {
                    var baseY = TextY(pageH, elY + 2, fontSize);
                    page.DrawText(label, elX, baseY,
                        new PdfDrawTextOptions { FontSize = fontSize, FillColor = labelColor });
                    if (el.Required == true)
                    {
                        var labelW = label.Length * fontSize * 0.52;
                        page.DrawText(" *", elX + labelW, baseY,
                            new PdfDrawTextOptions { FontSize = fontSize, FillColor = ParseColor("#ef4444") });
                    }
                }

                var fieldFieldName = !string.IsNullOrWhiteSpace(el.FieldName) ? el.FieldName
                    : !string.IsNullOrWhiteSpace(el.Id) ? el.Id
                    : Guid.NewGuid().ToString("N");
                page.AddTextField(fieldFieldName, elX, boxY, w, boxH,
                    defaultValue: "", fontSize: Math.Max(6, fontSize));
                break;
            }

            case "textarea":
            {
                var label = el.FieldLabel ?? el.FieldName ?? "";
                var borderColor = ParseColor(GetString(style, "borderColor") ?? "#d1d5db");
                var labelColor = ParseColor(GetString(style, "color") ?? "#374151");
                var fontSize = GetDouble(style, "fontSize", 11);
                const double taLabelOffset = 16.0;
                var boxH = Math.Clamp(h - taLabelOffset, 2, 20.0);
                var boxY = RectBottomY(pageH, elY + taLabelOffset, boxH);

                page.DrawRectangle(elX, boxY, w, boxH, lineWidth: 1, fill: true,
                    strokeColor: borderColor, fillColor: PdfColor.White);

                var placeholder = el.Placeholder ?? "";
                if (!string.IsNullOrEmpty(placeholder) && boxH > 10)
                    page.DrawText(placeholder, elX + 6,
                        TextY(pageH, elY + taLabelOffset + (boxH - fontSize * 1.2) / 2, fontSize),
                        new PdfDrawTextOptions { FontSize = fontSize, FillColor = ParseColor("#9ca3af"), Italic = true });

                if (!string.IsNullOrEmpty(label))
                {
                    var baseY = TextY(pageH, elY + 2, fontSize);
                    page.DrawText(label, elX, baseY,
                        new PdfDrawTextOptions { FontSize = fontSize, FillColor = labelColor });
                    if (el.Required == true)
                    {
                        var lw = label.Length * fontSize * 0.52;
                        page.DrawText(" *", elX + lw, baseY,
                            new PdfDrawTextOptions { FontSize = fontSize, FillColor = ParseColor("#ef4444") });
                    }
                }

                var fieldName = !string.IsNullOrWhiteSpace(el.Id) ? el.Id
                    : !string.IsNullOrWhiteSpace(el.FieldName) ? el.FieldName
                    : Guid.NewGuid().ToString("N");
                page.AddMultilineTextField(fieldName, elX, boxY, w, boxH,
                    defaultValue: "", fontSize: Math.Max(6, fontSize));
                break;
            }

            case "checkbox":
            {
                const double boxSize = 14;
                var boxCssTop = elY + (h - boxSize) / 2;
                var boxY = RectBottomY(pageH, boxCssTop, boxSize);
                var borderColor = ParseColor(GetString(style, "borderColor") ?? "#374151");
                var fillStr = GetString(style, "backgroundColor") ?? "#ffffff";
                var hasFill = fillStr != "transparent";
                var fontSize = GetDouble(style, "fontSize", 12);

                page.DrawRectangle(elX, boxY, boxSize, boxSize,
                    lineWidth: 1.5, fill: hasFill, strokeColor: borderColor,
                    fillColor: hasFill ? ParseColor(fillStr) : PdfColor.White);

                var state = el.CheckState ?? "empty";
                var midBoxY = boxY + boxSize / 2;

                if (state == "checked")
                {
                    page.DrawLine(elX + 2.5, midBoxY - 0.5, elX + 5.5, midBoxY + 3,
                        lineWidth: 1.5, strokeColor: borderColor);
                    page.DrawLine(elX + 5.5, midBoxY + 3, elX + 11.5, midBoxY - 4,
                        lineWidth: 1.5, strokeColor: borderColor);
                }
                else if (state == "cross")
                {
                    page.DrawLine(elX + 3, boxY + 3, elX + boxSize - 3, boxY + boxSize - 3,
                        lineWidth: 1.5, strokeColor: borderColor);
                    page.DrawLine(elX + boxSize - 3, boxY + 3, elX + 3, boxY + boxSize - 3,
                        lineWidth: 1.5, strokeColor: borderColor);
                }
                else if (state == "dot")
                {
                    page.DrawCircle(elX + boxSize / 2, midBoxY, 3,
                        lineWidth: 1, fill: true, strokeColor: borderColor, fillColor: borderColor);
                }

                var cbLabel = el.FieldLabel ?? "";
                if (!string.IsNullOrEmpty(cbLabel))
                {
                    var labelY = TextY(pageH, elY + (h - fontSize) / 2, fontSize);
                    page.DrawText(cbLabel, elX + boxSize + 8, labelY, new PdfDrawTextOptions
                    {
                        FontSize = fontSize,
                        FillColor = ParseColor(GetString(style, "color") ?? "#101828")
                    });
                }
                break;
            }

            case "checkmark":
            {
                var color = ParseColor(GetString(style, "color") ?? "#374151");
                var bw = GetDouble(style, "borderWidth", 2);
                var fontSize = GetDouble(style, "fontSize", 12);
                var boxSize = Math.Min(h / 2.0, 16.0);
                var boxY = RectBottomY(pageH, elY + (h - boxSize) / 2, boxSize);
                var state = el.CheckState ?? "checked";

                if (el.MarkMode == "rectangle" || el.MarkMode is null)
                {
                    if (state == "empty")
                    {
                        // Draw an empty box as a visual placeholder…
                        page.DrawRectangle(elX, boxY, boxSize, boxSize, lineWidth: bw, fill: false,
                            strokeColor: color, fillColor: PdfColor.White);
                        // …and overlay an interactive AcroForm checkbox widget.
                        var cbFieldName = !string.IsNullOrWhiteSpace(el.FieldName) ? el.FieldName
                            : !string.IsNullOrWhiteSpace(el.Id) ? el.Id
                            : Guid.NewGuid().ToString("N");
                        page.AddCheckBox(cbFieldName, elX, boxY, boxSize, isChecked: false);
                    }
                    else
                    {
                        page.DrawRectangle(elX, boxY, boxSize, boxSize, lineWidth: bw, fill: false,
                            strokeColor: color, fillColor: PdfColor.White);
                        var cx = elX + boxSize / 2;
                        var cy = boxY + boxSize / 2;
                        if (state == "checked")
                        {
                            page.DrawLine(elX + boxSize * 0.2, cy,
                                          cx - boxSize * 0.05, cy - boxSize * 0.25, lineWidth: bw, strokeColor: color);
                            page.DrawLine(cx - boxSize * 0.05, cy - boxSize * 0.25,
                                          elX + boxSize * 0.85, cy + boxSize * 0.3, lineWidth: bw, strokeColor: color);
                        }
                        else if (state == "cross")
                        {
                            page.DrawLine(elX + 4, boxY + 4, elX + boxSize - 4, boxY + boxSize - 4, lineWidth: bw, strokeColor: color);
                            page.DrawLine(elX + boxSize - 4, boxY + 4, elX + 4, boxY + boxSize - 4, lineWidth: bw, strokeColor: color);
                        }
                        else if (state == "dot")
                        {
                            page.DrawCircle(cx, cy, boxSize * 0.25, lineWidth: 1,
                                fill: true, strokeColor: color, fillColor: color);
                        }
                    }

                    // Label to the right of the box
                    var label = el.FieldLabel ?? "";
                    if (!string.IsNullOrEmpty(label) && w > boxSize + 4)
                    {
                        var labelX = elX + boxSize + 8;
                        var labelY = TextY(pageH, elY + (h - fontSize) / 2, fontSize);
                        var labelColor = ParseColor(GetString(style, "labelColor") ?? GetString(style, "color") ?? "#374151");
                        page.DrawText(label, labelX, labelY,
                            new PdfDrawTextOptions { FontSize = fontSize, FillColor = labelColor });
                    }
                }
                break;
            }

            case "signature":
            {
                var label = el.SignatureLabel ?? "Signature";
                var lineColor  = ParseColor(GetString(style, "borderColor") ?? GetString(style, "color") ?? "#9ca3af");
                var labelColor = ParseColor(GetString(style, "labelColor") ?? GetString(style, "color") ?? "#6b7280");
                var promptColor = ParseColor("#d1d5db");
                var dashStr = GetString(style, "dashStyle") ?? GetString(style, "borderStyle");
                var strokeStyle = BorderStyleToStroke(dashStr, 1);
                var pdfLineY = pageH - (elY + h - 14);

                // "Sign here" prompt inside the signing area
                if (h > 30)
                    page.DrawText("Sign here", elX + w / 2 - 25, pdfLineY + (h - 14) / 2,
                        new PdfDrawTextOptions { FontSize = 10, FillColor = promptColor, Italic = true });

                page.DrawLine(elX, pdfLineY, elX + w, pdfLineY,
                    lineWidth: 1, strokeColor: lineColor, strokeStyle: strokeStyle);
                page.DrawText(label, elX, pdfLineY - 6,
                    new PdfDrawTextOptions { FontSize = 10, FillColor = labelColor, Italic = true });
                break;
            }

            case "rect":
            case "shape":
            {
                var fillStr = GetString(style, "backgroundColor") ?? GetString(style, "fill") ?? "";
                var borderStr = GetString(style, "borderColor") ?? "";
                var bw = GetDouble(style, "borderWidth", 0);
                var hasFill = !string.IsNullOrEmpty(fillStr) && fillStr != "transparent";
                var hasBorder = !string.IsNullOrEmpty(borderStr) && bw > 0;
                var radius = ClampRadius(ParseRadius(GetString(style, "borderRadius")), w, h);
                var strokeColor = hasBorder ? ParseColor(borderStr) : (hasFill ? ParseColor(fillStr) : ParseColor("#d1d5db"));
                var fillColor = hasFill ? ParseColor(fillStr) : PdfColor.White;
                var lineW = hasBorder ? Math.Max(bw, 0.5) : 0.01;
                var boxY = RectBottomY(pageH, elY, h);
                var strokeStyle = BorderStyleToStroke(GetString(style, "borderStyle"), lineW);

                if (radius > 0)
                    page.DrawRoundedRectangle(elX, boxY, w, h, radius,
                        lineWidth: lineW, fill: hasFill,
                        strokeColor: strokeColor, fillColor: fillColor, strokeStyle: strokeStyle);
                else
                    page.DrawRectangle(elX, boxY, w, h,
                        lineWidth: lineW, fill: hasFill,
                        strokeColor: strokeColor, fillColor: fillColor, strokeStyle: strokeStyle);
                break;
            }

            case "circle":
            {
                var fillStr = GetString(style, "backgroundColor") ?? "#f3f4f6";
                var borderStr = GetString(style, "borderColor") ?? "#d1d5db";
                var bw = GetDouble(style, "borderWidth", 1);
                var hasFill = !string.IsNullOrEmpty(fillStr) && fillStr != "transparent";
                var cx = elX + w / 2;
                var cy = pageH - elY - h / 2;
                var strokeC = ParseColor(borderStr);
                var fillC = hasFill ? ParseColor(fillStr) : PdfColor.White;
                var lw = Math.Max(bw, 0.5);

                if (Math.Abs(w - h) < 1)
                {
                    page.DrawCircle(cx, cy, w / 2, lineWidth: lw, fill: hasFill,
                        strokeColor: strokeC, fillColor: fillC);
                }
                else
                {
                    page.DrawPolygon(EllipsePoints(cx, cy, w / 2, h / 2),
                        lineWidth: lw, fill: hasFill, strokeColor: strokeC, fillColor: fillC);
                }
                break;
            }

            case "line":
            {
                var color = ParseColor(GetString(style, "color") ?? GetString(style, "borderColor") ?? "#374151");
                var sw = Math.Max(GetDouble(style, "strokeWidth", 1), 0.5);
                var dashStr = GetString(style, "dashStyle") ?? GetString(style, "borderStyle");
                var strokeStyle = BorderStyleToStroke(dashStr, sw);

                if (w >= h)
                {
                    var midY = pageH - elY - h / 2;
                    page.DrawLine(elX, midY, elX + w, midY, lineWidth: sw, strokeColor: color, strokeStyle: strokeStyle);
                }
                else
                {
                    var midX = elX + w / 2;
                    page.DrawLine(midX, pageH - elY, midX, pageH - elY - h,
                        lineWidth: sw, strokeColor: color, strokeStyle: strokeStyle);
                }
                break;
            }

            case "arrow":
            {
                var color = ParseColor(GetString(style, "color") ?? "#374151");
                var sw = Math.Max(GetDouble(style, "strokeWidth", 1.5), 0.5);
                var lineY = pageH - elY - h / 2;
                var tipW = Math.Min(10.0, w * 0.2);
                var dotR = tipW * 0.4;

                var startMarker = el.StartMarker ?? "none";
                var endMarker   = el.EndMarker   ?? "arrow";

                var lineX0 = elX + (startMarker != "none" ? tipW : 0);
                var lineX1 = elX + w - (endMarker != "none" ? tipW : 0);

                page.DrawLine(lineX0, lineY, lineX1, lineY, lineWidth: sw, strokeColor: color);

                if (startMarker == "arrow")
                    page.DrawPolygon([
                        new PdfPoint(elX, lineY),
                        new PdfPoint(elX + tipW, lineY + tipW / 2),
                        new PdfPoint(elX + tipW, lineY - tipW / 2)
                    ], lineWidth: 0.5, fill: true, strokeColor: color, fillColor: color);
                else if (startMarker == "dot")
                    page.DrawCircle(elX + dotR, lineY, dotR,
                        lineWidth: 0.5, fill: true, strokeColor: color, fillColor: color);

                if (endMarker == "arrow")
                    page.DrawPolygon([
                        new PdfPoint(elX + w, lineY),
                        new PdfPoint(elX + w - tipW, lineY + tipW / 2),
                        new PdfPoint(elX + w - tipW, lineY - tipW / 2)
                    ], lineWidth: 0.5, fill: true, strokeColor: color, fillColor: color);
                else if (endMarker == "dot")
                    page.DrawCircle(elX + w - dotR, lineY, dotR,
                        lineWidth: 0.5, fill: true, strokeColor: color, fillColor: color);
                break;
            }

            case "highlight":
            {
                var fillStr = GetString(style, "backgroundColor") ?? "#fef08a";
                var c = ParseColor(fillStr);
                page.DrawRectangle(elX, RectBottomY(pageH, elY, h), w, h,
                    lineWidth: 0.01, fill: true, strokeColor: c, fillColor: c);
                break;
            }

            case "watermark":
            {
                var text = el.Content ?? "";
                if (string.IsNullOrWhiteSpace(text)) return;
                var fontSize = GetDouble(style, "fontSize", 48) * Math.Max(GetDouble(style, "scale", 1), 0.1);
                var rotation = GetDouble(style, "rotation", 45);
                if (rotation == 0) rotation = ParseRotation(GetString(style, "transform"));
                page.DrawText(text, elX + w / 2, pageH - elY - h / 2, new PdfDrawTextOptions
                {
                    FontSize = fontSize,
                    FillColor = ParseColor(GetString(style, "color") ?? "#d1d5db"),
                    RotationDegrees = rotation
                });
                break;
            }

            case "table":
            {
                var rows = el.CellData;
                if (rows is null || rows.Length == 0) return;

                var fontSize = GetDouble(style, "fontSize", 11);
                var textColor = ParseColor(GetString(style, "color") ?? "#101828");
                var borderColor = ParseColor(GetString(style, "borderColor") ?? "#e5e7eb");
                var headerBg = string.IsNullOrEmpty(el.HeaderBgColor)
                    ? new PdfColor(0.94, 0.97, 1.0)
                    : (IPdfColor)ParseColor(el.HeaderBgColor);
                var bw = GetDouble(style, "borderWidth", 0.75);
                var rowH = rows.Length > 0 ? h / rows.Length : 24;
                var cellPad = GetDouble(style, "cellPadding", 4);
                var footerBgStr = GetString(style, "footerBgColor");
                IPdfColor footerBg = string.IsNullOrEmpty(footerBgStr)
                    ? new PdfColor(0.97, 0.98, 0.99)
                    : ParseColor(footerBgStr);

                // Column count is determined by the first data row
                var colCount = rows[0]?.Length ?? 0;

                PdfTextAlignment[]? colAligns = null;
                if (el.ColumnAlignments is { Length: > 0 } ca && ca.Length == colCount)
                {
                    colAligns = ca.Select(a => a switch
                    {
                        "center" => PdfTextAlignment.Center,
                        "right"  => PdfTextAlignment.Right,
                        _        => PdfTextAlignment.Left
                    }).ToArray();
                }

                IPdfColor? zebraColor = null;
                if (el.ZebraEnabled == true)
                    zebraColor = string.IsNullOrEmpty(el.ZebraColor)
                        ? new PdfGrayColor(0.96)
                        : (IPdfColor)ParseColor(el.ZebraColor);

                // Only pass ColumnWidths when it matches the column count — an empty array
                // would cause the engine to throw (0 entries ≠ colCount).
                // The engine treats values as relative weights, so pixel widths work too.
                List<double>? colWidths = el.ColumnWidths is { Length: > 0 } cw && cw.Length == colCount
                    ? cw.ToList()
                    : null;

                var typedRows = rows.Select(r => (IReadOnlyList<string>)r.Select(c => c ?? "").ToList()).ToList();

                page.DrawSimpleTable(elX, pageH - elY, w, typedRows, new PdfTableOptions
                {
                    HasHeaderRow = el.HeaderRow ?? false,
                    HasFooterRow = el.FooterRow ?? false,
                    AutoRowHeight = true,
                    RowHeight = Math.Max(rowH, 16),
                    CellPadding = cellPad,
                    FontSize = fontSize,
                    TextColor = textColor,
                    BorderColor = borderColor,
                    BorderLineWidth = Math.Max(bw, 0.5),
                    HeaderFillColor = headerBg,
                    FooterFillColor = footerBg,
                    AlternateRowFillColor = zebraColor,
                    ColumnAlignments = colAligns,
                    ColumnWidths = colWidths,
                });
                break;
            }

            case "note":
            {
                var title = el.NoteTitle ?? "Note";
                var body = el.NoteBody ?? "";
                var bgColor = ParseColor(GetString(style, "backgroundColor") ?? "#fef9c3");
                var borderColor = ParseColor(GetString(style, "borderColor") ?? "#fbbf24");
                var titleColor = ParseColor("#78350f");
                var bodyColor = ParseColor("#92400e");
                var boxY = RectBottomY(pageH, elY, h);

                page.DrawRectangle(elX, boxY, w, h, lineWidth: 1, fill: true,
                    strokeColor: borderColor, fillColor: bgColor);

                if (!string.IsNullOrWhiteSpace(title))
                    page.DrawText(title, elX + 8, TextY(pageH, elY + 4, 11),
                        new PdfDrawTextOptions { FontSize = 11, Bold = true, FillColor = titleColor });

                if (!string.IsNullOrWhiteSpace(body))
                    page.DrawParagraph(body, elX + 8, TextY(pageH, elY + 20, 10), w - 16,
                        new PdfParagraphOptions { FontSize = 10, FillColor = bodyColor });

                if (!string.IsNullOrEmpty(el.NoteAuthor))
                    page.DrawText($"— {el.NoteAuthor}", elX + 8, boxY + 12,
                        new PdfDrawTextOptions { FontSize = 9, Italic = true, FillColor = bodyColor });
                break;
            }

            case "button":
            {
                var text = el.Content ?? "";
                var bgStr = GetString(style, "backgroundColor") ?? "#1d6fff";
                var textColor = ParseColor(GetString(style, "color") ?? "#ffffff");
                var radius = ClampRadius(ParseRadius(GetString(style, "borderRadius") ?? "6"), w, h);
                var boxY = RectBottomY(pageH, elY, h);
                var bgColor = ParseColor(bgStr);

                if (radius > 0)
                    page.DrawRoundedRectangle(elX, boxY, w, h, radius,
                        lineWidth: 0.5, fill: true, strokeColor: bgColor, fillColor: bgColor);
                else
                    page.DrawRectangle(elX, boxY, w, h,
                        lineWidth: 0.5, fill: true, strokeColor: bgColor, fillColor: bgColor);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    var fs = GetDouble(style, "fontSize", 12);
                    page.DrawText(text, elX + 8, TextY(pageH, elY + (h - fs * 1.4) / 2, fs),
                        new PdfDrawTextOptions { FontSize = fs, Bold = true, FillColor = textColor });
                }

                // Overlay a link annotation when a button action is configured.
                var btnAction = el.ButtonAction ?? "";
                if (btnAction.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    page.AddWebLink(elX, boxY, w, h, btnAction);
                }
                else if (btnAction.StartsWith("page:", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(btnAction.AsSpan(5), out var targetPage)
                    && targetPage >= 1)
                {
                    page.AddPageLink(elX, boxY, w, h, targetPage);
                }
                break;
            }

            case "dropdown":
            {
                var label = el.FieldLabel ?? el.Content ?? "";
                var borderColor = ParseColor(GetString(style, "borderColor") ?? "#d1d5db");
                var textColor = ParseColor(GetString(style, "color") ?? "#374151");
                var fs = GetDouble(style, "fontSize", 12);
                var boxY = RectBottomY(pageH, elY, h);

                page.DrawRectangle(elX, boxY, w, h, lineWidth: 1, fill: true,
                    strokeColor: borderColor, fillColor: PdfColor.White);

                var dropdownOptions = el.Options is { Length: > 0 } opts ? opts : null;
                if (dropdownOptions is not null)
                {
                    var fieldName = !string.IsNullOrWhiteSpace(el.Id) ? el.Id
                        : !string.IsNullOrWhiteSpace(el.FieldName) ? el.FieldName
                        : System.Guid.NewGuid().ToString("N");
                    page.AddComboBox(fieldName, elX, boxY, w, h, dropdownOptions, el.SelectedValue, Math.Max(6, fs));
                }
                else
                {
                    if (!string.IsNullOrEmpty(label))
                        page.DrawText(label, elX + 6, TextY(pageH, elY + (h - fs * 1.4) / 2, fs),
                            new PdfDrawTextOptions { FontSize = fs, FillColor = textColor });

                    page.DrawText("v", elX + w - 14, TextY(pageH, elY + (h - fs * 1.4) / 2, fs),
                        new PdfDrawTextOptions { FontSize = fs, FillColor = ParseColor("#9ca3af") });
                }
                break;
            }

            case "radio":
            case "optionlist":
            {
                var options = el.Options ?? [];
                var fs = GetDouble(style, "fontSize", 11);
                var textColor = ParseColor(GetString(style, "color") ?? "#101828");
                var lineH = fs * 1.7;

                var markerStyle = GetString(style, "markerStyle") ?? (el.Ordered == true ? "decimal" : "disc");

                for (var i = 0; i < options.Length; i++)
                {
                    var optCssY = elY + i * lineH;
                    if (optCssY + fs > elY + h) break;
                    var isSelected = options[i] == el.SelectedValue;
                    var bullet = el.Type == "radio"
                        ? (isSelected ? "* " : "o ")
                        : MarkerForStyle(markerStyle, i + 1);
                    page.DrawText($"{bullet}{options[i]}", elX, TextY(pageH, optCssY, fs),
                        new PdfDrawTextOptions { FontSize = fs, Bold = isSelected, FillColor = textColor });
                }
                break;
            }

            case "subsection":
            case "area":
            {
                var boxY = RectBottomY(pageH, elY, h);
                var dash = new PdfStrokeStyle { LineWidth = 0.5, DashArray = [4, 4] };
                page.DrawRectangle(elX, boxY, w, h, lineWidth: 0.5, fill: false,
                    strokeColor: ParseColor("#d1d5db"), strokeStyle: dash);
                break;
            }

            case "image":
            {
                var content = el.Content ?? "";
                if (content.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    DrawDataUrlImage(page, el, pageH, content);
                    break;
                }
                DrawPlaceholder(page, el, pageH, "[Image]");
                break;
            }

            case "qrcode":
            {
                var value = el.QrValue ?? "";
                if (string.IsNullOrWhiteSpace(value)) { DrawPlaceholder(page, el, pageH, "QR: (empty)"); break; }
                try
                {
                    var pngBytes = GenerateQrPng(value);
                    var tempPath = Path.ChangeExtension(Path.GetTempFileName(), ".png");
                    File.WriteAllBytes(tempPath, pngBytes);
                    try { page.DrawImage(tempPath, elX, RectBottomY(pageH, elY, h), w, h); }
                    finally { TryDelete(tempPath); }
                }
                catch { DrawPlaceholder(page, el, pageH, $"QR: {value}"); }
                break;
            }

            case "barcode":
            {
                var value = el.BarcodeValue ?? "";
                if (string.IsNullOrWhiteSpace(value)) { DrawPlaceholder(page, el, pageH, "Barcode: (empty)"); break; }
                try
                {
                    var pngBytes = GenerateBarcodePng(value, el.BarcodeType, (int)Math.Max(w, 1), (int)Math.Max(h * 0.7, 1));
                    var tempPath = Path.ChangeExtension(Path.GetTempFileName(), ".png");
                    File.WriteAllBytes(tempPath, pngBytes);
                    try { page.DrawImage(tempPath, elX, RectBottomY(pageH, elY, h * 0.7), w, h * 0.7); }
                    finally { TryDelete(tempPath); }
                }
                catch { DrawPlaceholder(page, el, pageH, $"Barcode: {value}"); }
                break;
            }

            case "chart":
            {
                try
                {
                    var pngBytes = GenerateChartPng(el, (int)Math.Max(w, 80), (int)Math.Max(h, 60));
                    var tempPath = Path.ChangeExtension(Path.GetTempFileName(), ".png");
                    File.WriteAllBytes(tempPath, pngBytes);
                    try { page.DrawImage(tempPath, elX, RectBottomY(pageH, elY, h), w, h); }
                    finally { TryDelete(tempPath); }
                }
                catch { DrawPlaceholder(page, el, pageH, $"Chart ({el.ChartType ?? "bar"})"); }
                break;
            }

            case "draw":
            {
                var path = el.PathData;
                if (string.IsNullOrWhiteSpace(path)) { DrawPlaceholder(page, el, pageH, "[Drawing]"); break; }
                var strokeColor = ParseColor(GetString(style, "color") ?? "#1d4ed8");
                var lineW       = GetDouble(style, "strokeWidth", 2);
                RenderSvgPath(page, path, elX, elY, pageH, lineW, strokeColor);
                break;
            }

            case "link":
            {
                var text  = el.Content ?? el.Href ?? "";
                var href  = el.Href ?? "";
                if (string.IsNullOrWhiteSpace(text)) text = href;
                if (string.IsNullOrWhiteSpace(text)) break;
                var fs       = GetDouble(style, "fontSize", 12);
                var fgColor  = ParseColor(GetString(style, "color") ?? "#2563eb");
                page.DrawText(text, elX, TextY(pageH, elY, fs),
                    new PdfDrawTextOptions { FontSize = fs, FillColor = fgColor, Underline = true });
                if (!string.IsNullOrWhiteSpace(href))
                    page.AddWebLink(elX, RectBottomY(pageH, elY, h), w, h, href);
                break;
            }

            case "number":
            {
                var val      = el.NumberValue ?? 0;
                var numStyle = (el.NumberStyle ?? "decimal").ToLowerInvariant();
                var decimals = el.NumberDecimals ?? 2;
                var currency = el.NumberCurrency ?? "USD";
                CultureInfo culture;
                try   { culture = CultureInfo.CreateSpecificCulture(el.NumberLocale ?? "en-US"); }
                catch { culture = CultureInfo.InvariantCulture; }

                var text = numStyle switch
                {
                    "currency"   => val.ToString($"C{decimals}", culture),
                    "percent"    => val.ToString($"P{decimals}", culture),
                    "scientific" => val.ToString($"E{decimals}", CultureInfo.InvariantCulture),
                    "ordinal"    => FormatOrdinal((long)Math.Round(val), culture),
                    _            => val.ToString($"N{decimals}", culture),
                };

                var fs      = GetDouble(style, "fontSize", 16);
                var fgColor = ParseColor(GetString(style, "color") ?? "#000000");
                var align   = ParseAlignment(GetString(style, "textAlign"));
                page.DrawParagraph(text, elX, TextY(pageH, elY, fs), w, new PdfParagraphOptions
                {
                    FontSize = fs, FillColor = fgColor, Alignment = align
                });
                break;
            }

            case "bookmark":
            {
                var name = el.BookmarkName ?? el.Id;
                if (!string.IsNullOrWhiteSpace(name))
                    document.AddNamedDestination(name, pageIndex, pageH - elY);
                break;
            }

            case "footnote":
            case "endnote":
            {
                // Superscript reference marker at the element position
                var refText = el.FootnoteRef ?? "*";
                var markerFs = GetDouble(style, "fontSize", 9);
                var fgColor  = ParseColor(GetString(style, "color") ?? "#374151");
                page.DrawText(refText, elX, TextY(pageH, elY, markerFs),
                    new PdfDrawTextOptions { FontSize = markerFs, FillColor = fgColor });

                // Footnote text block at the bottom of the page
                var fnText = el.FootnoteText ?? el.Content ?? "";
                if (!string.IsNullOrWhiteSpace(fnText))
                {
                    var fnFs  = markerFs * 0.9;
                    var fnX   = elX;
                    var fnY   = 32.0; // bottom margin area in PDF units
                    var fnColor = ParseColor("#6b7280");
                    var fullNote = $"{refText} {fnText}";
                    page.DrawParagraph(fullNote, fnX, fnY, w, new PdfParagraphOptions { FontSize = fnFs, FillColor = fnColor });
                }
                break;
            }

            case "comment":
            {
                var text   = el.CommentText ?? el.Content ?? "";
                var author = el.CommentAuthor ?? "";
                var date   = el.CommentDate ?? "";
                if (string.IsNullOrWhiteSpace(text)) break;

                var bgColor  = ParseColor("#fef9c3");
                var bdColor  = ParseColor("#ca8a04");
                var txColor  = ParseColor("#1c1917");
                var metaColor = ParseColor("#78716c");
                var fs       = GetDouble(style, "fontSize", 10);

                page.DrawRectangle(elX, RectBottomY(pageH, elY, h), w, h,
                    lineWidth: 0.5, fill: true, strokeColor: bdColor, fillColor: bgColor);

                var metaLine = string.Join("  ", new[] { author, date }.Where(s => !string.IsNullOrWhiteSpace(s)));
                var curY = elY + 4;
                if (!string.IsNullOrWhiteSpace(metaLine))
                {
                    page.DrawParagraph(metaLine, elX + 4, TextY(pageH, curY, fs * 0.85), w - 8,
                        new PdfParagraphOptions { FontSize = fs * 0.85, FillColor = metaColor, Bold = true });
                    curY += fs * 1.4;
                }
                page.DrawParagraph(text, elX + 4, TextY(pageH, curY, fs), w - 8,
                    new PdfParagraphOptions { FontSize = fs, FillColor = txColor });
                break;
            }

            case "contentcontrol":
            {
                var label   = el.ContentControlTitle ?? el.ContentControlTag ?? el.Type;
                var content = el.Content ?? el.ContentControlPlaceholder ?? "";
                var fs      = GetDouble(style, "fontSize", 11);
                var fgColor = ParseColor(GetString(style, "color") ?? "#111827");
                var bdColor = ParseColor("#6b7280");

                page.DrawRectangle(elX, RectBottomY(pageH, elY, h), w, h,
                    lineWidth: 0.7, fill: false, strokeColor: bdColor);
                if (!string.IsNullOrWhiteSpace(label))
                    page.DrawText(label, elX + 3, TextY(pageH, elY, fs * 0.75) + fs * 0.75,
                        new PdfDrawTextOptions { FontSize = fs * 0.75, FillColor = bdColor });
                if (!string.IsNullOrWhiteSpace(content))
                    page.DrawParagraph(content, elX + 4, TextY(pageH, elY + fs, fs), w - 8,
                        new PdfParagraphOptions { FontSize = fs, FillColor = fgColor });
                break;
            }

            case "pageboundary":
                break;

            case "toc":
            {
                var title        = string.IsNullOrWhiteSpace(el.TocTitle) ? "Table of Contents" : el.TocTitle;
                var showPageNums = el.TocShowPageNumbers ?? true;
                var showDots     = el.TocShowLeaderDots  ?? true;
                var minLevel     = el.TocMinLevel ?? 1;
                var maxLevel     = el.TocMaxLevel ?? 3;
                var fgColor      = ParseColor(GetString(style, "color") ?? "#1f2937");
                var fs           = GetDouble(style, "fontSize", 12);
                var lineH        = fs * 1.6;
                const double indentPerLevel = 12;
                const double pageNumW       = 30;

                // Prefer frontend-computed entries; fall back to live bookmarks
                // (bookmarks are scanned before the render loop so they're always ready).
                var rawEntries = el.TocEntries is { Length: > 0 }
                    ? el.TocEntries
                          .Where(e => e.Level >= minLevel && e.Level <= maxLevel)
                          .Select(e => (e.Text, e.Level, e.Page))
                          .ToList()
                    : document.GetBookmarks()
                          .Where(b => b.Level >= minLevel && b.Level <= maxLevel)
                          .Select(b => (b.Title, b.Level, b.PageNumber))
                          .ToList();

                if (rawEntries.Count == 0) break;

                // Title row
                var titleFs   = fs * 1.4;
                var titleOpts = new PdfParagraphOptions { FontSize = titleFs, FillColor = fgColor, Bold = true };
                page.DrawParagraph(title, elX, TextY(pageH, elY, titleFs), w, titleOpts);
                var cursorY = elY + titleFs + 8;

                var entryOpts = new PdfParagraphOptions { FontSize = fs, FillColor = fgColor };

                foreach (var (text, level, targetPage) in rawEntries)
                {
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    if (cursorY + lineH > elY + h) break;

                    var indent   = (level - 1) * indentPerLevel;
                    var textW    = showPageNums ? Math.Max(w - indent - pageNumW - 4, 10) : Math.Max(w - indent, 10);
                    var textX    = elX + indent;
                    var baseline = TextY(pageH, cursorY, fs);

                    page.DrawParagraph(text, textX, baseline, textW, entryOpts);

                    if (showPageNums)
                    {
                        if (showDots)
                        {
                            var dotsX = textX + textW;
                            var dotsW = w - indent - textW - pageNumW;
                            if (dotsW >= 4)
                            {
                                var dotCount = (int)(dotsW / (fs * 0.35));
                                page.DrawParagraph(new string('.', Math.Max(dotCount, 1)),
                                    dotsX, baseline, dotsW, entryOpts);
                            }
                        }
                        page.DrawParagraph(targetPage.ToString(CultureInfo.InvariantCulture),
                            elX + w - pageNumW, baseline, pageNumW,
                            new PdfParagraphOptions { FontSize = fs, FillColor = fgColor, Alignment = PdfTextAlignment.Right });
                    }

                    // Clickable link spanning the full row width
                    var linkBottomY = RectBottomY(pageH, cursorY, lineH);
                    page.AddPageLink(elX, linkBottomY, w, lineH, targetPage);

                    cursorY += lineH;
                }
                break;
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void DrawDataUrlImage(PdfPage page, ElementDto el, double pageH, string dataUrl)
    {
        try
        {
            var commaIdx = dataUrl.IndexOf(',', StringComparison.Ordinal);
            if (commaIdx < 0) return;
            var bytes = Convert.FromBase64String(dataUrl[(commaIdx + 1)..]);
            var ext = dataUrl.Contains("jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : ".png";
            var tempPath = Path.ChangeExtension(Path.GetTempFileName(), ext);
            File.WriteAllBytes(tempPath, bytes);
            try
            {
                var x = el.X;
                var y = RectBottomY(pageH, el.Y, el.Height);
                var w = el.Width;
                var h = el.Height;
                switch (el.FitMode)
                {
                    case "contain":
                        page.DrawImageFitCenter(tempPath, x, y, w, h);
                        break;
                    case "none":
                        page.DrawImageFit(tempPath, x, y, w, h);
                        break;
                    default:
                        page.DrawImage(tempPath, x, y, w, h);
                        break;
                }
            }
            finally { TryDelete(tempPath); }
        }
        catch { DrawPlaceholder(page, el, pageH, "[Image]"); }
    }

    private static byte[] GenerateChartPng(ElementDto el, int width, int height)
    {
        var chartType = (el.ChartType ?? "bar").ToLowerInvariant();
        var data = el.ChartData ?? new Dictionary<string, object>();

        // Extract labels and datasets from ChartData
        string[] labels = [];
        if (data.TryGetValue("labels", out var labObj) && labObj is JsonElement labEl && labEl.ValueKind == JsonValueKind.Array)
            labels = labEl.EnumerateArray().Select(e => e.GetString() ?? "").ToArray();

        var series = new List<(string label, double[] values, SKColor color)>();
        SKColor[] palette = [
            SKColor.Parse("#3b82f6"), SKColor.Parse("#10b981"), SKColor.Parse("#f59e0b"),
            SKColor.Parse("#ef4444"), SKColor.Parse("#8b5cf6"), SKColor.Parse("#06b6d4")
        ];
        if (data.TryGetValue("datasets", out var dsObj) && dsObj is JsonElement dsEl && dsEl.ValueKind == JsonValueKind.Array)
        {
            var idx = 0;
            foreach (var ds in dsEl.EnumerateArray())
            {
                var sLabel = ds.TryGetProperty("label", out var lp) ? lp.GetString() ?? "" : $"Series {idx + 1}";
                double[] vals = [];
                if (ds.TryGetProperty("data", out var dp) && dp.ValueKind == JsonValueKind.Array)
                    vals = dp.EnumerateArray().Select(e => e.TryGetDouble(out var d) ? d : 0).ToArray();
                var colorStr = ds.TryGetProperty("backgroundColor", out var cp) ? cp.GetString()
                             : ds.TryGetProperty("color", out var cp2) ? cp2.GetString() : null;
                var color = !string.IsNullOrEmpty(colorStr) ? SKColor.Parse(colorStr) : palette[idx % palette.Length];
                series.Add((sLabel, vals, color));
                idx++;
            }
        }

        if (series.Count == 0 || (labels.Length == 0 && series.All(s => s.values.Length == 0)))
        {
            // Fallback: simple demo data
            labels = ["A", "B", "C", "D"];
            series = [("Data", [40, 70, 50, 90], SKColor.Parse("#3b82f6"))];
        }

        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        const float padL = 32, padR = 12, padT = 12, padB = 28;
        float chartW = width - padL - padR;
        float chartH = height - padT - padB;

        double allMax = series.SelectMany(s => s.values).DefaultIfEmpty(1).Max();
        if (allMax <= 0) allMax = 1;

        using var axisPaint = new SKPaint { Color = SKColor.Parse("#9ca3af"), StrokeWidth = 1, IsAntialias = true, Style = SKPaintStyle.Stroke };
        using var textPaint = new SKPaint { Color = SKColor.Parse("#374151"), IsAntialias = true };
        var typeface = SKTypeface.Default;
        using var tf = new SKFont(typeface, 9);

        if (chartType == "pie")
        {
            // Pie chart
            float cx = padL + chartW / 2, cy = padT + chartH / 2;
            float r = Math.Min(chartW, chartH) / 2f - 4;
            var allVals = series.SelectMany(s => s.values).ToArray();
            var pieLabels = labels.Length > 0 ? labels : series.Select(s => s.label).ToArray();
            double total = allVals.Sum(); if (total <= 0) total = 1;
            float startAngle = -90;
            for (var i = 0; i < allVals.Length; i++)
            {
                float sweep = (float)(allVals[i] / total * 360.0);
                var sliceColor = palette[i % palette.Length];
                using var slicePaint = new SKPaint { Color = sliceColor, Style = SKPaintStyle.Fill, IsAntialias = true };
                using var path = new SKPath();
                path.MoveTo(cx, cy);
                path.ArcTo(new SKRect(cx - r, cy - r, cx + r, cy + r), startAngle, sweep, false);
                path.LineTo(cx, cy);
                canvas.DrawPath(path, slicePaint);
                startAngle += sweep;
            }
        }
        else if (chartType == "line")
        {
            // Line chart
            int nLabels = Math.Max(labels.Length, series.Select(s => s.values.Length).DefaultIfEmpty(0).Max());
            if (nLabels == 0) nLabels = 1;
            float xStep = nLabels > 1 ? chartW / (nLabels - 1) : chartW;

            canvas.DrawLine(padL, padT, padL, padT + chartH, axisPaint);
            canvas.DrawLine(padL, padT + chartH, padL + chartW, padT + chartH, axisPaint);

            foreach (var (_, vals, color) in series)
            {
                using var linePaint = new SKPaint { Color = color, StrokeWidth = 2, IsAntialias = true, Style = SKPaintStyle.Stroke };
                using var dotPaint  = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
                using var path = new SKPath();
                for (var i = 0; i < vals.Length; i++)
                {
                    float x = padL + i * xStep;
                    float y = padT + chartH - (float)(vals[i] / allMax * chartH);
                    if (i == 0) path.MoveTo(x, y); else path.LineTo(x, y);
                    canvas.DrawCircle(x, y, 3, dotPaint);
                }
                canvas.DrawPath(path, linePaint);
            }
            // X labels
            for (var i = 0; i < labels.Length; i++)
            {
                float x = padL + i * xStep;
                canvas.DrawText(labels[i], x - 6, padT + chartH + 16, tf, textPaint);
            }
        }
        else // bar
        {
            int nGroups = Math.Max(labels.Length, series.Select(s => s.values.Length).DefaultIfEmpty(0).Max());
            if (nGroups == 0) nGroups = 1;
            float groupW = chartW / nGroups;
            float barW = Math.Max(2, groupW / (series.Count + 1));

            canvas.DrawLine(padL, padT, padL, padT + chartH, axisPaint);
            canvas.DrawLine(padL, padT + chartH, padL + chartW, padT + chartH, axisPaint);

            for (var gi = 0; gi < nGroups; gi++)
            {
                float groupX = padL + gi * groupW + barW * 0.5f;
                for (var si = 0; si < series.Count; si++)
                {
                    var (_, vals, color) = series[si];
                    if (gi >= vals.Length) continue;
                    float barH = (float)(vals[gi] / allMax * chartH);
                    float barX = groupX + si * barW;
                    float barY = padT + chartH - barH;
                    using var barPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill, IsAntialias = true };
                    canvas.DrawRect(barX, barY, barW - 1, barH, barPaint);
                }
                // X label
                if (gi < labels.Length)
                    canvas.DrawText(labels[gi], padL + gi * groupW + groupW / 2 - 6, padT + chartH + 16, tf, textPaint);
            }
        }

        using var img  = SKImage.FromBitmap(bitmap);
        using var enc  = img.Encode(SKEncodedImageFormat.Png, 100);
        return enc.ToArray();
    }

    private static byte[] GenerateQrPng(string value)
    {
        var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(value, QRCodeGenerator.ECCLevel.M);
        var qr = new PngByteQRCode(data);
        return qr.GetGraphic(10);
    }

    private static byte[] GenerateBarcodePng(string value, string? barcodeType, int width, int height)
    {
        var format = ParseBarcodeFormat(barcodeType);
        var writer = new MultiFormatWriter();
        var hints = new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 2 };
        var matrix = writer.encode(value, format, width, height, hints);

        var pixels = new bool[matrix.Height, matrix.Width];
        for (var y = 0; y < matrix.Height; y++)
            for (var x = 0; x < matrix.Width; x++)
                pixels[y, x] = matrix[x, y]; // ZXing: [col, row]

        return MatrixToPng(pixels, 1);
    }

    private static BarcodeFormat ParseBarcodeFormat(string? type) => type?.ToLowerInvariant() switch
    {
        "code128" or "code-128" => BarcodeFormat.CODE_128,
        "code39"  or "code-39"  => BarcodeFormat.CODE_39,
        "ean13"   or "ean-13"   => BarcodeFormat.EAN_13,
        "ean8"    or "ean-8"    => BarcodeFormat.EAN_8,
        "upca"    or "upc-a"    => BarcodeFormat.UPC_A,
        "pdf417"                 => BarcodeFormat.PDF_417,
        _                        => BarcodeFormat.CODE_128
    };

    private static byte[] MatrixToPng(bool[,] matrix, int scale)
    {
        var rows = matrix.GetLength(0);
        var cols = matrix.GetLength(1);
        var width = cols * scale;
        var height = rows * scale;

        var raw = new byte[height * (1 + width)];
        var p = 0;
        for (var y = 0; y < height; y++)
        {
            raw[p++] = 0; // filter: None
            var row = y / scale;
            for (var x = 0; x < width; x++)
                raw[p++] = matrix[row, x / scale] ? (byte)0 : (byte)255;
        }

        byte[] idat;
        using (var ms = new MemoryStream())
        {
            using (var zlib = new ZLibStream(ms, CompressionLevel.Fastest, leaveOpen: true))
                zlib.Write(raw, 0, raw.Length);
            idat = ms.ToArray();
        }

        using var png = new MemoryStream();
        png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        WritePngChunk(png, "IHDR", [
            (byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width,
            (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height,
            8, 0, 0, 0, 0  // 8-bit grayscale
        ]);
        WritePngChunk(png, "IDAT", idat);
        WritePngChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WritePngChunk(Stream s, string type, byte[] data)
    {
        var len = data.Length;
        s.Write(new[] { (byte)(len >> 24), (byte)(len >> 16), (byte)(len >> 8), (byte)len });
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        var crcInput = new byte[4 + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, 4);
        var crc = PngCrc32(crcInput);
        s.Write(new[] { (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc });
    }

    private static uint PngCrc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }

    private static IReadOnlyList<PdfPoint> EllipsePoints(double cx, double cy, double rx, double ry)
    {
        const int steps = 32;
        var pts = new List<PdfPoint>(steps);
        for (var i = 0; i < steps; i++)
        {
            var a = 2 * Math.PI * i / steps;
            pts.Add(new PdfPoint(cx + rx * Math.Cos(a), cy + ry * Math.Sin(a)));
        }
        return pts;
    }

    private static PdfStrokeStyle? BorderStyleToStroke(string? borderStyle, double lineW) =>
        borderStyle switch
        {
            "dashed" => new PdfStrokeStyle { LineWidth = lineW, DashArray = [8, 4] },
            "dotted" => new PdfStrokeStyle { LineWidth = lineW, DashArray = [lineW, lineW * 2] },
            _ => null
        };

    private static bool MatchesPageScope(string? scope, string? range, int pageNumber, int totalPages) =>
        scope switch
        {
            "all"   => true,
            "first" => pageNumber == 1,
            "odd"   => pageNumber % 2 == 1,
            "even"  => pageNumber % 2 == 0,
            "range" => MatchesPageRange(range, pageNumber, totalPages),
            _       => false
        };

    private static bool MatchesPageRange(string? range, int pageNumber, int totalPages)
    {
        if (string.IsNullOrWhiteSpace(range)) return false;
        foreach (var part in range.Split(','))
        {
            var seg = part.Trim();
            if (seg.Contains('-'))
            {
                var dash = seg.IndexOf('-', StringComparison.Ordinal);
                var lo = seg[..dash].Trim() == "first" ? 1 : int.TryParse(seg[..dash].Trim(), out var l) ? l : 1;
                var hi = seg[(dash + 1)..].Trim() == "last" ? totalPages : int.TryParse(seg[(dash + 1)..].Trim(), out var r) ? r : totalPages;
                if (pageNumber >= lo && pageNumber <= hi) return true;
            }
            else if (int.TryParse(seg, out var exact) && exact == pageNumber) return true;
        }
        return false;
    }

    private static void DrawPlaceholder(PdfPage page, ElementDto el, double pageH, string label)
    {
        var boxY = RectBottomY(pageH, el.Y, el.Height);
        var dash = new PdfStrokeStyle { LineWidth = 0.5, DashArray = [3, 3] };
        page.DrawRectangle(el.X, boxY, el.Width, el.Height, lineWidth: 0.5, fill: false,
            strokeColor: ParseColor("#d1d5db"), strokeStyle: dash);

        if (!string.IsNullOrEmpty(label) && el.Height > 14)
        {
            var fs = Math.Min(10.0, el.Height * 0.35);
            page.DrawText(label, el.X + 4, boxY + el.Height / 2 - fs * 0.3,
                new PdfDrawTextOptions { FontSize = fs, FillColor = ParseColor("#9ca3af") });
        }
    }

    private static PdfParagraphOptions BuildParaOptions(
        Dictionary<string, object> style,
        string? language = null,
        string? textDirection = null)
    {
        var fontSize = GetDouble(style, "fontSize", 12);
        var bold = GetString(style, "fontWeight") is "bold" or "700" or "600";
        var italic = GetString(style, "fontStyle") == "italic";
        var color = ParseColor(GetString(style, "color") ?? "#101828");
        var align = ParseAlignment(GetString(style, "textAlign"));
        var family = ParseFontFamily(GetString(style, "fontFamily"));
        var lineH = GetDouble(style, "lineHeight", 0);
        var deco = GetString(style, "textDecoration") ?? "";
        var letterSpacing = GetDouble(style, "letterSpacing", 0);
        // "rotation" is stored as a direct number; "transform" uses "rotate(Ndeg)" CSS syntax
        var rotation = GetDouble(style, "rotation", 0);
        if (rotation == 0) rotation = ParseRotation(GetString(style, "transform"));

        // lineHeight from the frontend is a CSS unitless multiplier (e.g. 1.4).
        // DrawParagraph expects an absolute pixel value, so multiply by fontSize.
        // Values >= 8 are assumed to already be pixel values (legacy fallback).
        var lineHeightPx = lineH > 0 ? (lineH < 8 ? lineH * fontSize : lineH) : (double?)null;

        return new PdfParagraphOptions
        {
            FontSize = fontSize,
            Bold = bold,
            Italic = italic,
            FillColor = color,
            Alignment = align,
            FontFamily = family,
            LineHeight = lineHeightPx,
            Underline = deco.Contains("underline", StringComparison.OrdinalIgnoreCase),
            Strikethrough = deco.Contains("line-through", StringComparison.OrdinalIgnoreCase),
            CharacterSpacing = letterSpacing,
            RotationDegrees = rotation,
            Language = language,
            TextDirection = textDirection
        };
    }

    /// <summary>
    /// Mutates element content in-place, replacing {{KEY}} placeholders with the resolved
    /// property values for the target language. Operates on a cloned element list to avoid
    /// modifying the caller's design object.
    /// </summary>
    private static DesignExportDto ApplyPropertySubstitutions(
        DesignExportDto design,
        Dictionary<string, string> props)
    {
        foreach (var page in design.Pages)
            foreach (var el in page.Elements)
                SubstituteElement(el, props);
        foreach (var el in design.SharedElements)
            SubstituteElement(el, props);
        return design;
    }

    private static void SubstituteElement(ElementDto el, Dictionary<string, string> props)
    {
        el.Content        = Substitute(el.Content,        props);
        el.HtmlContent    = Substitute(el.HtmlContent,    props);
        el.FieldLabel     = Substitute(el.FieldLabel,     props);
        el.SignatureLabel = Substitute(el.SignatureLabel,  props);
        el.QrValue        = Substitute(el.QrValue,        props);
        el.BarcodeValue   = Substitute(el.BarcodeValue,   props);
        el.NoteTitle      = Substitute(el.NoteTitle,      props);
        el.NoteBody       = Substitute(el.NoteBody,       props);
        el.FootnoteText   = Substitute(el.FootnoteText,   props);
        el.ButtonAction   = Substitute(el.ButtonAction,   props);
    }

    private static string? Substitute(string? text, Dictionary<string, string> props)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{{", StringComparison.Ordinal))
            return text;
        foreach (var (key, value) in props)
            text = text.Replace("{{" + key + "}}", value, StringComparison.OrdinalIgnoreCase);
        return text;
    }

    private static string HtmlToText(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        html = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        html = Regex.Replace(html, @"</p>|</div>|</h[1-6]>", "\n", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        html = Regex.Replace(html, @"<li[^>]*>", "\n• ", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        html = Regex.Replace(html, "<[^>]+>", "", RegexOptions.None, TimeSpan.FromSeconds(1));
        html = html.Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
                   .Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase)
                   .Replace("&lt;", "<", StringComparison.OrdinalIgnoreCase)
                   .Replace("&gt;", ">", StringComparison.OrdinalIgnoreCase)
                   .Replace("&quot;", "\"", StringComparison.OrdinalIgnoreCase)
                   .Replace("&#39;", "'", StringComparison.OrdinalIgnoreCase);
        html = Regex.Replace(html, @"\n{3,}", "\n\n", RegexOptions.None, TimeSpan.FromSeconds(1));
        return html.Trim();
    }

    private static PdfColor ParseColor(string? hex)
    {
        if (string.IsNullOrEmpty(hex) || hex == "transparent") return PdfColor.White;
        hex = hex.TrimStart('#');
        if (hex.Length == 3)
            hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
        if (hex.Length != 6) return PdfColor.Black;
        var r = int.Parse(hex[..2], NumberStyles.HexNumber) / 255.0;
        var g = int.Parse(hex[2..4], NumberStyles.HexNumber) / 255.0;
        var b = int.Parse(hex[4..6], NumberStyles.HexNumber) / 255.0;
        return new PdfColor(r, g, b);
    }

    private static PdfFontFamily? ParseFontFamily(string? family)
    {
        if (string.IsNullOrEmpty(family)) return null;
        var lower = family.ToLowerInvariant();
        if (lower.Contains("times") || lower.Contains("georgia") || lower.Contains("serif"))
            return PdfFontFamily.Times;
        if (lower.Contains("courier") || lower.Contains("mono") || lower.Contains("consolas") || lower.Contains("code"))
            return PdfFontFamily.Courier;
        return PdfFontFamily.Helvetica;
    }

    private static PdfTextAlignment ParseAlignment(string? align) => align switch
    {
        "center"  => PdfTextAlignment.Center,
        "right"   => PdfTextAlignment.Right,
        "justify" => PdfTextAlignment.Justify,
        _         => PdfTextAlignment.Left
    };

    private static double ParseRotation(string? transform)
    {
        if (string.IsNullOrEmpty(transform)) return 0;
        var m = Regex.Match(transform, @"rotate\(([+-]?\d+(?:\.\d+)?)deg\)", RegexOptions.None, TimeSpan.FromSeconds(1));
        return m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;
    }

    private static double ParseRadius(string? val)
    {
        if (string.IsNullOrEmpty(val) || val == "0") return 0;
        var stripped = val.Replace("px", "", StringComparison.OrdinalIgnoreCase)
                          .Replace("%", "", StringComparison.OrdinalIgnoreCase).Trim();
        return double.TryParse(stripped, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) && r > 0 ? r : 0;
    }

    private static double ClampRadius(double r, double w, double h) =>
        r <= 0 ? 0 : Math.Min(r, Math.Min(w, h) / 2 - 0.01);

    private static string? GetString(Dictionary<string, object> style, string key) =>
        style.TryGetValue(key, out var v) ? v?.ToString() : null;

    private static double GetDouble(Dictionary<string, object> style, string key, double fallback)
    {
        if (!style.TryGetValue(key, out var v) || v is null) return fallback;
        return v switch
        {
            double d  => d,
            float f   => f,
            int i     => i,
            long l    => l,
            _ => double.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : fallback
        };
    }

    private static string ToRoman(int num)
    {
        if (num <= 0) return num.ToString(CultureInfo.InvariantCulture);
        int[] values   = [1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1];
        string[] syms  = ["m", "cm", "d", "cd", "c", "xc", "l", "xl", "x", "ix", "v", "iv", "i"];
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < values.Length; i++)
            while (num >= values[i]) { sb.Append(syms[i]); num -= values[i]; }
        return sb.ToString();
    }

    private static string ToAlpha(int num)
    {
        if (num <= 0) return "";
        var sb = new System.Text.StringBuilder();
        while (num > 0) { sb.Insert(0, (char)('a' + (num - 1) % 26)); num = (num - 1) / 26; }
        return sb.ToString();
    }

    private static string MarkerForStyle(string markerStyle, int n) => markerStyle switch
    {
        "decimal"      => $"{n}. ",
        "lower-alpha"  => $"{ToAlpha(n)}. ",
        "upper-alpha"  => $"{ToAlpha(n).ToUpperInvariant()}. ",
        "lower-roman"  => $"{ToRoman(n)}. ",
        "upper-roman"  => $"{ToRoman(n).ToUpperInvariant()}. ",
        "square"       => "■ ",
        "circle"       => "o ",
        _              => "• "   // disc / default bullet
    };

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* best-effort cleanup */ }
    }

    private static string FormatOrdinal(long n, CultureInfo culture)
    {
        var abs = Math.Abs(n);
        var suffix = (abs % 100) switch
        {
            11 or 12 or 13 => "th",
            _ => (abs % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" }
        };
        return n.ToString(culture) + suffix;
    }

    // ── SVG path → PDF primitives ─────────────────────────────────────────────
    // pathData coordinates are element-local (0,0 = element top-left, Y down).
    // Transforms: pdfX = offsetX + svgX,  pdfY = pageH - offsetY - svgY

    private static void RenderSvgPath(
        PdfPage page, string pathData,
        double offsetX, double offsetY, double pageH,
        double lineWidth, IPdfColor color)
    {
        // Split on SVG command letters, keeping the letter with the following numbers
        var segments = Regex.Split(pathData.Trim(), @"(?=[MmLlCcSsHhVvZz])");
        double cx = 0, cy = 0, startX = 0, startY = 0, prevCx2 = 0, prevCy2 = 0;

        double PdfX(double x) => offsetX + x;
        double PdfY(double y) => pageH - offsetY - y;
        double[] Nums(string s) => Regex.Matches(s, @"-?[\d.]+(?:[eE][+-]?\d+)?")
            .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();

        foreach (var seg in segments)
        {
            if (string.IsNullOrWhiteSpace(seg)) continue;
            var cmd  = seg[0];
            var nums = Nums(seg[1..]);
            var rel  = char.IsLower(cmd);

            switch (char.ToUpperInvariant(cmd))
            {
                case 'M':
                    for (var i = 0; i + 1 < nums.Length; i += 2)
                    {
                        var nx = rel ? cx + nums[i] : nums[i];
                        var ny = rel ? cy + nums[i + 1] : nums[i + 1];
                        if (i > 0) // subsequent coords in M are implicit L
                            page.DrawLine(PdfX(cx), PdfY(cy), PdfX(nx), PdfY(ny), lineWidth, color);
                        else { startX = nx; startY = ny; }
                        cx = nx; cy = ny;
                    }
                    prevCx2 = cx; prevCy2 = cy;
                    break;

                case 'L':
                    for (var i = 0; i + 1 < nums.Length; i += 2)
                    {
                        var nx = rel ? cx + nums[i] : nums[i];
                        var ny = rel ? cy + nums[i + 1] : nums[i + 1];
                        page.DrawLine(PdfX(cx), PdfY(cy), PdfX(nx), PdfY(ny), lineWidth, color);
                        cx = nx; cy = ny;
                    }
                    prevCx2 = cx; prevCy2 = cy;
                    break;

                case 'H':
                    foreach (var x in nums)
                    {
                        var nx = rel ? cx + x : x;
                        page.DrawLine(PdfX(cx), PdfY(cy), PdfX(nx), PdfY(cy), lineWidth, color);
                        cx = nx;
                    }
                    prevCx2 = cx; prevCy2 = cy;
                    break;

                case 'V':
                    foreach (var y in nums)
                    {
                        var ny = rel ? cy + y : y;
                        page.DrawLine(PdfX(cx), PdfY(cy), PdfX(cx), PdfY(ny), lineWidth, color);
                        cy = ny;
                    }
                    prevCx2 = cx; prevCy2 = cy;
                    break;

                case 'C':
                    for (var i = 0; i + 5 < nums.Length; i += 6)
                    {
                        var x1 = rel ? cx + nums[i]     : nums[i];
                        var y1 = rel ? cy + nums[i + 1] : nums[i + 1];
                        var x2 = rel ? cx + nums[i + 2] : nums[i + 2];
                        var y2 = rel ? cy + nums[i + 3] : nums[i + 3];
                        var ex = rel ? cx + nums[i + 4] : nums[i + 4];
                        var ey = rel ? cy + nums[i + 5] : nums[i + 5];
                        page.DrawBezierCurve(
                            new(PdfX(cx), PdfY(cy)), new(PdfX(x1), PdfY(y1)),
                            new(PdfX(x2), PdfY(y2)), new(PdfX(ex), PdfY(ey)),
                            lineWidth, color);
                        prevCx2 = x2; prevCy2 = y2; cx = ex; cy = ey;
                    }
                    break;

                case 'S': // smooth cubic — reflect previous control point 2
                    for (var i = 0; i + 3 < nums.Length; i += 4)
                    {
                        var x1 = 2 * cx - prevCx2;
                        var y1 = 2 * cy - prevCy2;
                        var x2 = rel ? cx + nums[i]     : nums[i];
                        var y2 = rel ? cy + nums[i + 1] : nums[i + 1];
                        var ex = rel ? cx + nums[i + 2] : nums[i + 2];
                        var ey = rel ? cy + nums[i + 3] : nums[i + 3];
                        page.DrawBezierCurve(
                            new(PdfX(cx), PdfY(cy)), new(PdfX(x1), PdfY(y1)),
                            new(PdfX(x2), PdfY(y2)), new(PdfX(ex), PdfY(ey)),
                            lineWidth, color);
                        prevCx2 = x2; prevCy2 = y2; cx = ex; cy = ey;
                    }
                    break;

                case 'Z':
                    page.DrawLine(PdfX(cx), PdfY(cy), PdfX(startX), PdfY(startY), lineWidth, color);
                    cx = startX; cy = startY;
                    prevCx2 = cx; prevCy2 = cy;
                    break;
            }
        }
    }
}
