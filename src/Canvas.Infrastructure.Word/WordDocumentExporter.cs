using Canvas.Core.Abstractions;
using Canvas.Core.Contracts;
using Canvas.Core.Primitives;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A   = DocumentFormat.OpenXml.Drawing;
using DW  = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using WPS = DocumentFormat.OpenXml.Office2010.Word.DrawingShape;

namespace Canvas.Infrastructure.Word;

public sealed class WordDocumentExporter : IDocumentExporter
{
    private sealed class LayoutContext
    {
        internal LayoutContext(List<string> warnings, CancellationToken cancellationToken, bool fidelityV2)
        {
            Warnings = warnings;
            CancellationToken = cancellationToken;
            FidelityV2 = fidelityV2;
        }

        internal double CursorY { get; set; }
        internal uint DrawingOrder { get; set; }
        internal uint NextDrawingId { get; set; } = 1;
        internal List<string> Warnings { get; }
        internal CancellationToken CancellationToken { get; }
        internal bool FidelityV2 { get; }
    }

    private static readonly IReadOnlyDictionary<string, string> FontFamilyFallbackMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["inter"] = "Calibri",
            ["roboto"] = "Arial",
            ["helvetica"] = "Arial",
            ["arial"] = "Arial",
            ["times new roman"] = "Times New Roman",
            ["georgia"] = "Georgia",
            ["courier new"] = "Consolas",
            ["menlo"] = "Consolas",
            ["monaco"] = "Consolas",
            ["source code pro"] = "Consolas",
            ["fira code"] = "Consolas",
        };

    public string FormatKey     => "word";
    public string MimeType      => "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public string FileExtension => ".docx";
    public IExporterCapabilities Capabilities => new ExporterCapabilities(SupportsImages: true);

    public byte[] Export(DesignExportDto design)
        => Export(design, null);

    public byte[] Export(DesignExportDto design, ExportOptions? options)
    {
        var cancellationToken = options?.CancellationToken ?? CancellationToken.None;
        var fidelityV2 = options?.WordFidelityV2 ?? true;
        cancellationToken.ThrowIfCancellationRequested();

        var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            // Use package properties API to avoid malformed core-properties XML.
            doc.PackageProperties.Title = design.PageSettings?.Metadata?.Title ?? design.Name;
            doc.PackageProperties.Creator = design.PageSettings?.Metadata?.Author ?? string.Empty;
            doc.PackageProperties.Subject = design.PageSettings?.Metadata?.Subject ?? string.Empty;
            var warnings = new List<string>();

            // Named styles
            StyleDefinitionService.Apply(doc, design.PageSettings?.NamedStyles);

            // Document protection
            DocumentProtectionService.Apply(doc, design.PageSettings?.Protection);

            // Custom document properties
            CustomPropertiesService.Apply(doc, design.PageSettings?.CustomProperties);

            // Auto-hyphenation: enable at document level when any element requests it
            var anyHyphenation = (design.Pages ?? [])
                .SelectMany(p => p.Elements ?? [])
                .Concat(design.SharedElements ?? [])
                .Any(e => e.AutoHyphenation == true);
            if (anyHyphenation)
                ApplyDocumentAutoHyphenation(doc);

            // Per-document services shared across pages
            var footnoteService = new FootnoteService(doc);
            var commentService  = new CommentService(doc);

            var plannedPages = DesignLayoutPlanner.BuildPages(design, GetElementZIndex);

            for (int pi = 0; pi < plannedPages.Count; pi++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (pi > 0)
                    body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));

                var layout = new LayoutContext(warnings, cancellationToken, fidelityV2);
                var elements = plannedPages[pi].Elements;

                foreach (var el in elements)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    RenderElement(body, mainPart, el, layout, footnoteService, commentService);
                }
            }

            footnoteService.Save();
            commentService.Save();

            if (fidelityV2 && warnings.Count > 0)
                doc.PackageProperties.Description = BuildDescriptionWithWarnings(design.Description, warnings);

            // Ensure body ends with a paragraph (Word requirement)
            if (!body.Elements<Paragraph>().Any())
                body.InsertAt(new Paragraph(), 0);

            // sectPr MUST be the last child of body (OOXML §17.6.17)
            ApplySectionGeometry(body, design.PageSettings);

            mainPart.Document.Save();
        } // doc.Dispose() commits ZIP to ms
        return ms.ToArray();
    }

    private static void RenderElement(Body body, MainDocumentPart mainPart, ElementDto el, LayoutContext layout,
        FootnoteService footnoteService, CommentService commentService)
    {
        var s = el.Style ?? [];

        switch (el.Type)
        {
            case "text":
            {
                var text = el.Content ?? "";
                if (string.IsNullOrWhiteSpace(text)) break;

                var bgHex = NormalizeHexColor(s.GetStr("backgroundColor", ""), "");

                if (layout.FidelityV2)
                {
                    body.AppendChild(CreateTextBoxParagraph(el, layout, txbx =>
                    {
                        var innerPara = new Paragraph();
                        var ppr = new ParagraphProperties();
                        ApplyParagraphTypography(ppr, s, el);
                        ppr.AppendChild(new Justification { Val = ToJustification(s.GetStr("textAlign", "left")) });
                        innerPara.PrependChild(ppr);
                        var run = innerPara.AppendChild(new Run());
                        ApplyCharacterStyle(run, el);
                        run.PrependChild(BuildRunProperties(s, "111827"));
                        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
                        txbx.AppendChild(WrapWithRevision(innerPara, el));
                    }, bgHex));
                }
                else
                {
                    var para = new Paragraph();
                    var ppr = new ParagraphProperties();
                    if (!string.IsNullOrEmpty(bgHex))
                        ppr.AppendChild(new Shading { Fill = bgHex, Val = ShadingPatternValues.Clear, Color = "auto" });
                    ApplyParagraphTypography(ppr, s, el);
                    ApplyParagraphPositioning(ppr, el, layout, applyTopOffset: true);
                    ppr.AppendChild(new Justification { Val = ToJustification(s.GetStr("textAlign", "left")) });
                    para.PrependChild(ppr);
                    var run = para.AppendChild(new Run());
                    ApplyCharacterStyle(run, el);
                    run.PrependChild(BuildRunProperties(s, "111827"));
                    run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
                    body.AppendChild(para);
                }

                AdvanceCursor(layout, el);
                break;
            }

            case "richtext":
            {
                var html = el.HtmlContent ?? el.Content ?? string.Empty;
                var richParas = RichTextSpanParser.Parse(html);
                if (richParas.Count == 0)
                    break;

                var bgHexRt = NormalizeHexColor(s.GetStr("backgroundColor", ""), "");

                if (layout.FidelityV2)
                {
                    body.AppendChild(CreateTextBoxParagraph(el, layout, txbx =>
                    {
                        foreach (var rp in richParas)
                        {
                            if (rp.Runs.Count == 0) continue;
                            var innerPara = new Paragraph();
                            var ppr = new ParagraphProperties();
                            ApplyParagraphTypography(ppr, s, el);
                            ppr.AppendChild(new Justification { Val = ToJustification(s.GetStr("textAlign", "left")) });
                            innerPara.PrependChild(ppr);
                            foreach (var rr in rp.Runs)
                            {
                                if (rr.Text.Length == 0) continue;
                                var run = innerPara.AppendChild(new Run());
                                ApplyCharacterStyle(run, el);
                                run.PrependChild(BuildRunProperties(s, "111827",
                                    forceUnderline: rr.Underline, forceBold: rr.Bold,
                                    forceItalic: rr.Italic, forceStrike: rr.Strike,
                                    colorOverrideHex: rr.ColorHex, fontSizeOverridePt: rr.FontSizePt));
                                run.AppendChild(new Text(rr.Text) { Space = SpaceProcessingModeValues.Preserve });
                            }
                            txbx.AppendChild(WrapWithRevision(innerPara, el));
                        }
                    }, bgHexRt));
                }
                else
                {
                    foreach (var rp in richParas)
                    {
                        if (rp.Runs.Count == 0) continue;
                        var para = new Paragraph();
                        var ppr = new ParagraphProperties();
                        if (!string.IsNullOrEmpty(bgHexRt))
                            ppr.AppendChild(new Shading { Fill = bgHexRt, Val = ShadingPatternValues.Clear, Color = "auto" });
                        ApplyParagraphTypography(ppr, s, el);
                        ApplyParagraphPositioning(ppr, el, layout, applyTopOffset: rp == richParas[0]);
                        ppr.AppendChild(new Justification { Val = ToJustification(s.GetStr("textAlign", "left")) });
                        para.PrependChild(ppr);
                        foreach (var rr in rp.Runs)
                        {
                            if (rr.Text.Length == 0) continue;
                            var run = para.AppendChild(new Run());
                            ApplyCharacterStyle(run, el);
                            run.PrependChild(BuildRunProperties(s, "111827",
                                forceUnderline: rr.Underline, forceBold: rr.Bold,
                                forceItalic: rr.Italic, forceStrike: rr.Strike,
                                colorOverrideHex: rr.ColorHex, fontSizeOverridePt: rr.FontSizePt));
                            run.AppendChild(new Text(rr.Text) { Space = SpaceProcessingModeValues.Preserve });
                        }
                        body.AppendChild(WrapWithRevision(para, el));
                    }
                }

                AdvanceCursor(layout, el);
                break;
            }

            case "link":
            {
                var text = el.Content ?? el.Href ?? "";
                var href = el.Href ?? "#";

                var para  = new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = ToJustification(s.GetStr("textAlign", "left")) }));
                var linkPpr = para.GetFirstChild<ParagraphProperties>();
                if (linkPpr is not null)
                {
                    ApplyParagraphTypography(linkPpr, s);
                    ApplyParagraphPositioning(linkPpr, el, layout, applyTopOffset: true);
                }
                Run run;
                if (Uri.TryCreate(href, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto))
                {
                    var rid = mainPart.AddHyperlinkRelationship(uri, true).Id;
                    var hyper = para.AppendChild(new Hyperlink { Id = rid });
                    run = hyper.AppendChild(new Run());
                }
                else
                {
                    // Invalid/relative href should not fail export.
                    AddWarning(layout, $"Link fallback to plain text for element '{el.Id}' (href='{href}').");
                    run = para.AppendChild(new Run());
                }

                var rpr = BuildRunProperties(s, "2563EB", forceUnderline: true);
                run.PrependChild(rpr);
                run.AppendChild(new Text(text));
                body.AppendChild(para);
                AdvanceCursor(layout, el);
                break;
            }

            case "table":
                RenderTable(body, el, layout);
                break;

            case "signature":
            {
                var label = el.SignatureLabel ?? "Signature";
                var para  = new Paragraph();
                var ppr = new ParagraphProperties();
                ApplyParagraphPositioning(ppr, el, layout, applyTopOffset: true);
                para.PrependChild(ppr);

                var labelRun = para.AppendChild(new Run());
                labelRun.PrependChild(BuildRunProperties(s, "1F2937", forceBold: true));
                labelRun.AppendChild(new Text($"{label}: "));

                var lineRun = para.AppendChild(new Run());
                lineRun.PrependChild(BuildRunProperties(s, "1F2937"));
                lineRun.AppendChild(new Text("_______________________"));

                body.AppendChild(para);
                AdvanceCursor(layout, el);
                break;
            }

            case "field":
            {
                var label = el.FieldLabel ?? "";
                var req   = el.Required == true ? " *" : "";
                var para  = new Paragraph();
                var ppr = new ParagraphProperties();
                ApplyParagraphPositioning(ppr, el, layout, applyTopOffset: true);
                para.PrependChild(ppr);

                var run = para.AppendChild(new Run());
                run.PrependChild(BuildRunProperties(s, "1F2937", forceBold: true));
                run.AppendChild(new Text($"{label}: "));

                if (!string.IsNullOrEmpty(req))
                {
                    var reqRun = para.AppendChild(new Run());
                    reqRun.PrependChild(new RunProperties(new Color { Val = "DC2626" }));
                    reqRun.AppendChild(new Text(req + " "));
                }

                var run2 = para.AppendChild(new Run());
                run2.PrependChild(BuildRunProperties(s, "1F2937", forceUnderline: true));
                run2.AppendChild(new Text("_______________") { Space = SpaceProcessingModeValues.Preserve });

                body.AppendChild(para);
                AdvanceCursor(layout, el);
                break;
            }

            case "checkbox":
            {
                var label = el.FieldLabel ?? "";
                var para  = new Paragraph();
                var ppr = new ParagraphProperties();
                ApplyParagraphPositioning(ppr, el, layout, applyTopOffset: true);
                para.PrependChild(ppr);

                var run = para.AppendChild(new Run());
                run.PrependChild(BuildRunProperties(s, "1F2937"));
                run.AppendChild(new Text($"☐ {label}"));

                body.AppendChild(para);
                AdvanceCursor(layout, el);
                break;
            }

            case "note":
            {
                var title  = el.NoteTitle ?? "Note";
                var body_  = el.NoteBody ?? "";
                var noteBg = NormalizeHexColor(s.GetStr("backgroundColor", "#fef3c7"), "fef3c7");

                var para1 = new Paragraph(new ParagraphProperties(
                    new Shading { Fill = noteBg, Val = ShadingPatternValues.Clear, Color = "auto" }));
                var ppr1 = para1.GetFirstChild<ParagraphProperties>();
                if (ppr1 is not null)
                    ApplyParagraphPositioning(ppr1, el, layout, applyTopOffset: true);
                var run1 = para1.AppendChild(new Run());
                run1.PrependChild(new RunProperties(new Bold()));
                run1.AppendChild(new Text(title));
                body.AppendChild(para1);
                if (!string.IsNullOrWhiteSpace(body_))
                {
                    var para2 = new Paragraph(new ParagraphProperties(
                        new Shading { Fill = noteBg, Val = ShadingPatternValues.Clear, Color = "auto" }));
                    var ppr2 = para2.GetFirstChild<ParagraphProperties>();
                    if (ppr2 is not null)
                        ApplyParagraphPositioning(ppr2, el, layout, applyTopOffset: false);
                    para2.AppendChild(new Run(new Text(body_)));
                    body.AppendChild(para2);
                }
                AdvanceCursor(layout, el);
                break;
            }

            case "optionlist":
            {
                var opts   = el.Options ?? [];
                var style  = el.ListStyle ?? (el.Ordered == true ? "decimal" : "disc");
                var isOrd  = style is "decimal" or "lower-alpha" or "upper-alpha" or "lower-roman" or "upper-roman";
                for (int i = 0; i < opts.Length; i++)
                {
                    var para = new Paragraph();
                    var ppr = new ParagraphProperties();
                    ApplyParagraphPositioning(ppr, el, layout, applyTopOffset: i == 0);
                    para.PrependChild(ppr);
                    para.AppendChild(new Run(new Text(isOrd ? $"{i + 1}. {opts[i]}" : $"• {opts[i]}")));
                    body.AppendChild(para);
                }
                AdvanceCursor(layout, el);
                break;
            }

            case "number":
            {
                var val  = el.NumberValue?.ToString() ?? "";
                var para = new Paragraph();
                var ppr = new ParagraphProperties();
                ApplyParagraphPositioning(ppr, el, layout, applyTopOffset: true);
                para.PrependChild(ppr);
                para.AppendChild(new Run(new Text(val)));
                body.AppendChild(para);
                AdvanceCursor(layout, el);
                break;
            }

            case "pagenumber":
            {
                var para  = new Paragraph();
                var ppr = new ParagraphProperties();
                ApplyParagraphPositioning(ppr, el, layout, applyTopOffset: true);
                para.PrependChild(ppr);

                if (!string.IsNullOrEmpty(el.Prefix))
                    para.AppendChild(new Run(new Text(el.Prefix) { Space = SpaceProcessingModeValues.Preserve }));

                var format = (el.NumberingFormat ?? "current").ToLowerInvariant();
                if (format == "total")
                {
                    AppendField(para, " NUMPAGES ");
                }
                else if (format == "pageoftotal")
                {
                    AppendField(para, " PAGE ");
                    para.AppendChild(new Run(new Text(" / ") { Space = SpaceProcessingModeValues.Preserve }));
                    AppendField(para, " NUMPAGES ");
                }
                else
                {
                    AppendField(para, " PAGE ");
                }

                if (!string.IsNullOrEmpty(el.Suffix))
                    para.AppendChild(new Run(new Text(el.Suffix) { Space = SpaceProcessingModeValues.Preserve }));

                body.AppendChild(para);
                AdvanceCursor(layout, el);
                break;
            }

            case "image":
                EmbedImage(body, mainPart, el, layout);
                break;

            case "rect":
            case "shape":
            {
                if (layout.FidelityV2)
                {
                    var fillHex   = NormalizeHexColor(s.GetStr("backgroundColor", ""), "");
                    var strokeHex = NormalizeHexColor(s.GetStr("borderColor", s.GetStr("color", "")), "");
                    var strokePx  = (int)Math.Round(s.GetNum("borderWidth", 1));
                    body.AppendChild(CreateShapeParagraph(el, layout, A.ShapeTypeValues.Rectangle, fillHex, strokeHex, strokePx));
                }
                // non-v2: silently skip
                break;
            }

            case "circle":
            {
                if (layout.FidelityV2)
                {
                    var fillHex   = NormalizeHexColor(s.GetStr("backgroundColor", ""), "");
                    var strokeHex = NormalizeHexColor(s.GetStr("borderColor", s.GetStr("color", "")), "");
                    var strokePx  = (int)Math.Round(s.GetNum("borderWidth", 1));
                    body.AppendChild(CreateShapeParagraph(el, layout, A.ShapeTypeValues.Ellipse, fillHex, strokeHex, strokePx));
                }
                // non-v2: silently skip
                break;
            }

            case "line":
            case "arrow":
            {
                if (layout.FidelityV2)
                {
                    var strokeHex = NormalizeHexColor(s.GetStr("color", s.GetStr("borderColor", "000000")), "000000");
                    var strokePx  = (int)Math.Round(s.GetNum("lineWidth", s.GetNum("borderWidth", 2)));
                    body.AppendChild(CreateShapeParagraph(el, layout, A.ShapeTypeValues.Line, "", strokeHex, strokePx));
                }
                // non-v2: silently skip
                break;
            }

            // Skip layout-only elements
            case "draw":
            case "watermark":
            case "highlight":
            case "pageboundary":
            case "area":
            case "subsection":
            {
                if (layout.FidelityV2)
                    AppendUnsupportedPlaceholder(body, el, layout);
                break;
            }

            case "footnote":
            {
                var text = el.FootnoteText ?? el.Content ?? "";
                if (string.IsNullOrWhiteSpace(text)) break;
                var refRun = footnoteService.AddFootnote(text);
                var para = new Paragraph();
                var ppr = new ParagraphProperties();
                ApplyParagraphPositioning(ppr, el, layout, applyTopOffset: true);
                para.PrependChild(ppr);
                para.AppendChild(refRun);
                body.AppendChild(para);
                AdvanceCursor(layout, el);
                break;
            }

            case "endnote":
            {
                var text = el.FootnoteText ?? el.Content ?? "";
                if (string.IsNullOrWhiteSpace(text)) break;
                var refRun = footnoteService.AddEndnote(text);
                var para = new Paragraph();
                var ppr = new ParagraphProperties();
                ApplyParagraphPositioning(ppr, el, layout, applyTopOffset: true);
                para.PrependChild(ppr);
                para.AppendChild(refRun);
                body.AppendChild(para);
                AdvanceCursor(layout, el);
                break;
            }

            case "bookmark":
            {
                var name   = el.BookmarkName ?? el.Name ?? $"bm_{el.Id}";
                var bmId   = Math.Abs(el.Id.GetHashCode()) % 100000;
                var para   = new Paragraph();
                var ppr    = new ParagraphProperties();
                ApplyParagraphPositioning(ppr, el, layout, applyTopOffset: true);
                para.PrependChild(ppr);
                para.AppendChild(new BookmarkStart { Id = bmId.ToString(), Name = name });
                if (!string.IsNullOrWhiteSpace(el.Content))
                {
                    var run = new Run();
                    run.AppendChild(new Text(el.Content) { Space = SpaceProcessingModeValues.Preserve });
                    para.AppendChild(run);
                }
                para.AppendChild(new BookmarkEnd { Id = bmId.ToString() });
                body.AppendChild(para);
                AdvanceCursor(layout, el);
                break;
            }

            case "comment":
            {
                var author = el.CommentAuthor ?? "Canvas";
                var text   = el.CommentText ?? el.Content ?? "";
                if (string.IsNullOrWhiteSpace(text)) break;
                var (start, end, refRunProps) = commentService.AddComment(text, author, el.CommentDate);
                var para = new Paragraph();
                var ppr  = new ParagraphProperties();
                ApplyParagraphPositioning(ppr, el, layout, applyTopOffset: true);
                para.PrependChild(ppr);
                para.AppendChild(start);
                var anchorRun = new Run();
                anchorRun.AppendChild(refRunProps);
                para.AppendChild(anchorRun);
                para.AppendChild(end);
                body.AppendChild(para);
                AdvanceCursor(layout, el);
                break;
            }

            case "contentcontrol":
            {
                var title = el.ContentControlTitle ?? el.Name ?? "Field";
                var tag   = el.ContentControlTag   ?? el.Id;
                var placeholder = el.ContentControlPlaceholder ?? el.Content ?? "";

                var sdtProps = new SdtProperties(
                    new SdtAlias { Val = title },
                    new Tag      { Val = tag });

                if (el.ContentControlType == "datePicker")
                    sdtProps.AppendChild(new SdtContentDate());
                else if (el.ContentControlType is "comboBox" or "dropdown")
                    sdtProps.AppendChild(new SdtContentDropDownList());
                else
                    sdtProps.AppendChild(new SdtContentText());

                var sdtContent = new SdtContentBlock();
                var innerPara  = new Paragraph();
                var ppr        = new ParagraphProperties();
                ApplyParagraphPositioning(ppr, el, layout, applyTopOffset: true);
                innerPara.PrependChild(ppr);
                if (!string.IsNullOrEmpty(placeholder))
                {
                    var run = new Run();
                    run.AppendChild(new Text(placeholder) { Space = SpaceProcessingModeValues.Preserve });
                    innerPara.AppendChild(run);
                }
                sdtContent.AppendChild(innerPara);

                var sdt = new SdtBlock();
                sdt.AppendChild(sdtProps);
                sdt.AppendChild(sdtContent);
                body.AppendChild(sdt);
                AdvanceCursor(layout, el);
                break;
            }

            default:
            {
                AppendUnsupportedPlaceholder(body, el, layout);
                break;
            }
        }
    }

    private static void AppendUnsupportedPlaceholder(Body body, ElementDto el, LayoutContext layout)
    {
        AddWarning(layout, $"Unsupported element '{el.Type}' rendered as textual annotation (id='{el.Id}').");
        var desc = !string.IsNullOrWhiteSpace(el.Content) ? $": {el.Content}" : "";
        var para = new Paragraph();
        var ppr = new ParagraphProperties();
        ApplyParagraphPositioning(ppr, el, layout, applyTopOffset: true);
        para.PrependChild(ppr);
        var run = para.AppendChild(new Run());
        run.PrependChild(new RunProperties(new Italic()));
        run.AppendChild(new Text($"[{el.Type}{desc}]"));
        body.AppendChild(para);
        AdvanceCursor(layout, el);
    }

    private static void RenderTable(Body body, ElementDto el, LayoutContext layout)
    {
        var s        = el.Style ?? [];
        var cellData = el.CellData;
        if (cellData is null || cellData.Length == 0) return;

        var topOffset = Math.Max(0, el.Y - layout.CursorY);
        if (topOffset > 0)
        {
            var spacer = new Paragraph();
            var ppr = new ParagraphProperties();
            ppr.AppendChild(new SpacingBetweenLines { Before = WordUnitConverter.CanvasToTwips(topOffset).ToString() });
            spacer.PrependChild(ppr);
            body.AppendChild(spacer);
        }

        var cols       = cellData[0]?.Length ?? 0;
        var bw         = (uint)Math.Max(1, (int)s.GetNum("borderWidth", 1));
        var bc         = NormalizeHexColor(s.GetStr("borderColor", "#000000"), "000000");
        var hasHdr     = el.HeaderRow == true;
        var hdrBg      = NormalizeHexColor(el.HeaderBgColor ?? "#f1f5f9", "f1f5f9");
        var zebraColor = el.ZebraEnabled == true ? NormalizeHexColor(el.ZebraColor ?? "#f9fafb", "f9fafb") : null;

        // Column widths use Canvas units converted through shared converter.
        // Fall back to equal distribution of element width across columns.
        var colWidthsPx = el.ColumnWidths ?? [];
        var tableWidthPx = el.Width > 0 ? el.Width : 500.0;
        var equalPx = tableWidthPx / Math.Max(cols, 1);
        int ColTwips(int c) => WordUnitConverter.CanvasToTwips(
            colWidthsPx.Length > c ? colWidthsPx[c] : equalPx);

        var alignments = el.ColumnAlignments ?? [];

        var table = new Table();
        var tblPr = new TableProperties(
            new TableWidth { Width = WordUnitConverter.CanvasToTwips(tableWidthPx).ToString(), Type = TableWidthUnitValues.Dxa },
            new TableLayout { Type = TableLayoutValues.Fixed },
            new TableBorders(
                new TopBorder    { Val = BorderValues.Single, Size = bw, Color = bc },
                new LeftBorder   { Val = BorderValues.Single, Size = bw, Color = bc },
                new BottomBorder { Val = BorderValues.Single, Size = bw, Color = bc },
                new RightBorder  { Val = BorderValues.Single, Size = bw, Color = bc },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = bw, Color = bc },
                new InsideVerticalBorder   { Val = BorderValues.Single, Size = bw, Color = bc }));
        var leftTwips = WordUnitConverter.CanvasToTwips(Math.Max(0, el.X));
        if (leftTwips > 0)
            tblPr.AppendChild(new TableIndentation { Width = leftTwips, Type = TableWidthUnitValues.Dxa });
        table.AppendChild(tblPr);

        // TableGrid defines column widths
        var grid = new TableGrid();
        for (int c = 0; c < cols; c++)
            grid.AppendChild(new GridColumn { Width = ColTwips(c).ToString() });
        table.AppendChild(grid);

        for (int r = 0; r < cellData.Length; r++)
        {
            var row     = cellData[r] ?? [];
            var isHdr   = hasHdr && r == 0;
            var isZebra = !isHdr && zebraColor != null && r % 2 == 1;

            var tableRow = new TableRow();
            if (isHdr)
                tableRow.AppendChild(new TableRowProperties(new TableHeader()));

            for (int c = 0; c < cols; c++)
            {
                var cell  = row.Length > c ? row[c] ?? "" : "";
                var align = alignments.Length > c ? alignments[c] : "left";
                var tc    = new TableCell();

                var tcp = new TableCellProperties(
                    new TableCellWidth { Width = ColTwips(c).ToString(), Type = TableWidthUnitValues.Dxa });
                if (isHdr)
                    tcp.AppendChild(new Shading { Fill = hdrBg, Val = ShadingPatternValues.Clear, Color = "auto" });
                else if (isZebra)
                    tcp.AppendChild(new Shading { Fill = zebraColor!, Val = ShadingPatternValues.Clear, Color = "auto" });
                // CT_TcPr order: tcW(2) → shd(6) → vAlign(11)
                var vAlignVal = el.Style?.GetStr("verticalAlign", "top") switch {
                    "middle" or "center" => TableVerticalAlignmentValues.Center,
                    "bottom"             => TableVerticalAlignmentValues.Bottom,
                    _                    => TableVerticalAlignmentValues.Top,
                };
                tcp.AppendChild(new TableCellVerticalAlignment { Val = vAlignVal });
                tc.AppendChild(tcp);

                var para = new Paragraph(
                    new ParagraphProperties(new Justification { Val = ToJustification(align) }));
                var run  = para.AppendChild(new Run());
                if (isHdr) run.PrependChild(new RunProperties(new Bold()));
                run.AppendChild(new Text(cell) { Space = SpaceProcessingModeValues.Preserve });
                tc.AppendChild(para);
                tableRow.AppendChild(tc);
            }
            table.AppendChild(tableRow);
        }

        body.AppendChild(table);
        body.AppendChild(new Paragraph()); // spacing after table
        AdvanceCursor(layout, el);
    }

    private static void EmbedImage(Body body, MainDocumentPart mainPart, ElementDto el, LayoutContext layout)
    {
        var s = el.Style ?? [];
        var src = el.Content ?? "";
        byte[]? imgBytes = null;
        var contentType = "image/png";

        try
        {
            if (src.StartsWith("data:"))
            {
                var mime = src[5..src.IndexOf(';')];
                contentType = mime;
                var base64 = src[(src.IndexOf(',') + 1)..];
                imgBytes = Convert.FromBase64String(base64);
            }
            else if (src.StartsWith("http://") || src.StartsWith("https://"))
            {
                imgBytes = FetchRemoteImageWithRetry(src, maxAttempts: 2, timeoutSeconds: 5, layout.CancellationToken);
                if (imgBytes is not null)
                {
                    if (src.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        src.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                        contentType = "image/jpeg";
                }
            }
        }
        catch { }

        if (imgBytes is null)
        {
            AppendImagePlaceholder(body, el, layout, s);
            return;
        }

        var imagePart = mainPart.AddImagePart(contentType switch
        {
            "image/jpeg" => ImagePartType.Jpeg,
            "image/gif"  => ImagePartType.Gif,
            _            => ImagePartType.Png,
        });
        using (var ms = new MemoryStream(imgBytes))
            imagePart.FeedData(ms);

        var relationshipId = mainPart.GetIdOfPart(imagePart);

        long cx = WordUnitConverter.CanvasToEmu(el.Width > 0 ? el.Width : 200);
        long cy = WordUnitConverter.CanvasToEmu(el.Height > 0 ? el.Height : 150);
        var drawingId = layout.NextDrawingId++;
        var fitMode = (el.FitMode ?? "fill").Trim().ToLowerInvariant();
        var preserveAspect = fitMode is "contain" or "cover";
        var positioned = layout.FidelityV2 && (el.X > 0 || el.Y > 0);
        var zIndex = Math.Max(0, (int)Math.Round(s.GetNum("zIndex", 0), MidpointRounding.AwayFromZero));
        var drawing = positioned
            ? CreateAnchoredDrawing(relationshipId, el, cx, cy, drawingId, zIndex, preserveAspect, layout)
            : CreateInlineDrawing(relationshipId, el, cx, cy, drawingId, preserveAspect);

        var para = new Paragraph();
        if (!positioned)
        {
            var ppr = new ParagraphProperties();
            ApplyParagraphPositioning(ppr, el, layout, applyTopOffset: true);
            para.PrependChild(ppr);
        }
        para.AppendChild(new Run(drawing));
        body.AppendChild(para);
        AdvanceCursor(layout, el);
    }

    private static void AppendImagePlaceholder(Body body, ElementDto el, LayoutContext layout, Dictionary<string, object> style)
    {
        AddWarning(layout, $"Image fallback placeholder rendered for element '{el.Id}'.");
        var para = new Paragraph();
        var ppr = new ParagraphProperties();
        ApplyParagraphPositioning(ppr, el, layout, applyTopOffset: true);
        para.PrependChild(ppr);

        var run = para.AppendChild(new Run());
        run.PrependChild(BuildRunProperties(style, "6B7280", forceItalic: true));
        run.AppendChild(new Text("[image unavailable]"));

        body.AppendChild(para);
        AdvanceCursor(layout, el);
    }

    private static byte[]? FetchRemoteImageWithRetry(string url, int maxAttempts, int timeoutSeconds, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= Math.Max(1, maxAttempts); attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)) };
                var bytes = http.GetByteArrayAsync(url, cancellationToken).GetAwaiter().GetResult();
                if (bytes.Length > 0)
                    return bytes;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                if (attempt == maxAttempts)
                    break;
            }
        }

        return null;
    }

    private static void AddWarning(LayoutContext layout, string warning)
    {
        if (string.IsNullOrWhiteSpace(warning))
            return;

        if (!layout.Warnings.Contains(warning, StringComparer.Ordinal))
            layout.Warnings.Add(warning);
    }

    private static string BuildDescriptionWithWarnings(string? description, List<string> warnings)
    {
        var prefix = string.IsNullOrWhiteSpace(description)
            ? string.Empty
            : description.Trim() + "\n";

        var joined = string.Join(" | ", warnings.Select(w => w.Replace("\r", " ").Replace("\n", " ")));
        return prefix + "ExportWarnings: " + joined;
    }

    private static Drawing CreateInlineDrawing(string relationshipId, ElementDto el, long cx, long cy, uint drawingId, bool preserveAspect)
    {
        return new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                new DW.DocProperties { Id = drawingId, Name = el.Id ?? "image" },
                new DW.NonVisualGraphicFrameDrawingProperties(
                    new A.GraphicFrameLocks { NoChangeAspect = preserveAspect }),
                BuildPictureGraphic(relationshipId, el, cx, cy))
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U,
            });
    }

    private static Drawing CreateAnchoredDrawing(
        string relationshipId,
        ElementDto el,
        long cx,
        long cy,
        uint drawingId,
        int zIndex,
        bool preserveAspect,
        LayoutContext layout)
    {
        var x = WordUnitConverter.CanvasToEmu(Math.Max(0, el.X));
        var y = WordUnitConverter.CanvasToEmu(Math.Max(0, el.Y));
        var relativeHeight = (uint)Math.Min(uint.MaxValue, (uint)zIndex + layout.DrawingOrder);
        layout.DrawingOrder++;

        return new Drawing(
            new DW.Anchor(
                new DW.SimplePosition { X = 0L, Y = 0L },
                new DW.HorizontalPosition(
                    new DW.PositionOffset(x.ToString()))
                { RelativeFrom = DW.HorizontalRelativePositionValues.Page },
                new DW.VerticalPosition(
                    new DW.PositionOffset(y.ToString()))
                { RelativeFrom = DW.VerticalRelativePositionValues.Page },
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                new DW.WrapNone(),
                new DW.DocProperties { Id = drawingId, Name = el.Id ?? "image" },
                new DW.NonVisualGraphicFrameDrawingProperties(
                    new A.GraphicFrameLocks { NoChangeAspect = preserveAspect }),
                BuildPictureGraphic(relationshipId, el, cx, cy))
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U,
                SimplePos = false,
                RelativeHeight = relativeHeight,
                BehindDoc = false,
                Locked = false,
                LayoutInCell = true,
                AllowOverlap = true,
            });
    }

    private static A.Graphic BuildPictureGraphic(string relationshipId, ElementDto el, long cx, long cy)
    {
        return new A.Graphic(
            new A.GraphicData(
                new PIC.Picture(
                    new PIC.NonVisualPictureProperties(
                        new PIC.NonVisualDrawingProperties { Id = 0U, Name = el.Id ?? "image" },
                        new PIC.NonVisualPictureDrawingProperties()),
                    new PIC.BlipFill(
                        new A.Blip { Embed = relationshipId },
                        new A.Stretch(new A.FillRectangle())),
                    new PIC.ShapeProperties(
                        new A.Transform2D(
                            new A.Offset { X = 0, Y = 0 },
                            new A.Extents { Cx = cx, Cy = cy }),
                        new A.PresetGeometry(new A.AdjustValueList())
                        { Preset = A.ShapeTypeValues.Rectangle })))
            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" });
    }

    private static JustificationValues ToJustification(string align) => align switch
    {
        "center"  => JustificationValues.Center,
        "right"   => JustificationValues.Right,
        "justify" => JustificationValues.Both,
        _         => JustificationValues.Left,
    };

    private static void AppendField(Paragraph para, string code)
    {
        var begin = para.AppendChild(new Run());
        begin.AppendChild(new FieldChar { FieldCharType = FieldCharValues.Begin });

        var fieldCode = para.AppendChild(new Run());
        fieldCode.AppendChild(new FieldCode(code));

        var end = para.AppendChild(new Run());
        end.AppendChild(new FieldChar { FieldCharType = FieldCharValues.End });
    }

    private static RunProperties BuildRunProperties(
        Dictionary<string, object> style,
        string defaultColor,
        bool forceUnderline = false,
        bool forceBold = false,
        bool forceItalic = false,
        bool forceStrike = false,
        string? colorOverrideHex = null,
        double? fontSizeOverridePt = null)
    {
        var rpr = new RunProperties();

        var mappedFamily = ResolveFontFamily(style.GetStr("fontFamily", ""));
        if (!string.IsNullOrEmpty(mappedFamily))
            rpr.AppendChild(new RunFonts { Ascii = mappedFamily, HighAnsi = mappedFamily });

        if (forceBold || IsBoldWeight(style.GetStr("fontWeight", "normal")))
            rpr.AppendChild(new Bold());
        if (forceItalic || style.GetStr("fontStyle", "normal") == "italic")
            rpr.AppendChild(new Italic());

        var decoration = style.GetStr("textDecoration", "none").ToLowerInvariant();
        if (forceStrike || HasDecoration(decoration, "line-through"))
            rpr.AppendChild(new Strike());

        var colorRaw = colorOverrideHex is { Length: > 0 }
            ? colorOverrideHex
            : style.GetStr("color", $"#{defaultColor}");
        var color = NormalizeHexColor(colorRaw, defaultColor);
        rpr.AppendChild(new Color { Val = color });

        var fsPoints = Math.Max(1, fontSizeOverridePt ?? style.GetNum("fontSize", 14));
        var fsHalfPoints = WordUnitConverter.PointsToHalfPoints(fsPoints);
        rpr.AppendChild(new FontSize { Val = fsHalfPoints.ToString() });

        if (forceUnderline || HasDecoration(decoration, "underline"))
            rpr.AppendChild(new Underline { Val = UnderlineValues.Single });

        return rpr;
    }

    private static void ApplyParagraphTypography(ParagraphProperties ppr, Dictionary<string, object> style,
        ElementDto? el = null)
    {
        var lineHeight = style.GetNum("lineHeight", 0);
        if (lineHeight > 0)
        {
            var fsPoints  = Math.Max(1, style.GetNum("fontSize", 14));
            var lineTwips = (int)Math.Round(fsPoints * lineHeight * 20, MidpointRounding.AwayFromZero);
            if (lineTwips > 0)
                ppr.AppendChild(new SpacingBetweenLines
                {
                    Line     = lineTwips.ToString(),
                    LineRule = LineSpacingRuleValues.Auto,
                });
        }

        // Named paragraph style reference
        if (!string.IsNullOrWhiteSpace(el?.StyleName))
            ppr.InsertAt(new ParagraphStyleId { Val = el.StyleName }, 0);

        // Per-paragraph hyphenation: suppress when element explicitly opts out
        if (el?.AutoHyphenation == false)
            ppr.AppendChild(new SuppressAutoHyphens { Val = true });
    }

    private static void ApplyParagraphPositioning(ParagraphProperties ppr, ElementDto el, LayoutContext layout, bool applyTopOffset)
    {
        // OOXML CT_PPrBase: spacing(22) must come before ind(23)
        if (applyTopOffset)
        {
            var topOffset = Math.Max(0, el.Y - layout.CursorY);
            if (topOffset > 0)
            {
                var before = WordUnitConverter.CanvasToTwips(topOffset).ToString();
                // Merge into existing SpacingBetweenLines (e.g. from ApplyParagraphTypography) to avoid duplicates
                var existing = ppr.GetFirstChild<SpacingBetweenLines>();
                if (existing is not null)
                    existing.Before = before;
                else
                    ppr.AppendChild(new SpacingBetweenLines { Before = before });
            }
        }

        var left = WordUnitConverter.CanvasToTwips(Math.Max(0, el.X));
        if (left > 0)
            ppr.AppendChild(new Indentation { Left = left.ToString() });
    }

    /// <summary>
    /// Creates a paragraph containing an anchored WPS text box at the element's exact X/Y position.
    /// This replaces the legacy framePr approach and renders identically in Word and LibreOffice.
    /// </summary>
    private static Paragraph CreateTextBoxParagraph(
        ElementDto el,
        LayoutContext layout,
        Action<TextBoxContent> populateContent,
        string? bgHex = null)
    {
        var x  = WordUnitConverter.CanvasToEmu(Math.Max(0, el.X));
        var y  = WordUnitConverter.CanvasToEmu(Math.Max(0, el.Y));
        var cx = WordUnitConverter.CanvasToEmu(el.Width  > 0 ? el.Width  : 200);
        var cy = WordUnitConverter.CanvasToEmu(el.Height > 0 ? el.Height : 24);
        var id = layout.NextDrawingId++;
        var zh = (uint)(layout.DrawingOrder++);

        var txbxContent = new TextBoxContent();
        populateContent(txbxContent);

        // Shape fill
        OpenXmlElement fill = string.IsNullOrEmpty(bgHex)
            ? new A.NoFill()
            : new A.SolidFill(new A.RgbColorModelHex { Val = bgHex });

        var wsp = new WPS.WordprocessingShape(
            new WPS.NonVisualDrawingShapeProperties { TextBox = true },
            new WPS.ShapeProperties(
                new A.Transform2D(
                    new A.Offset   { X = 0L, Y = 0L },
                    new A.Extents  { Cx = cx, Cy = cy }),
                new A.PresetGeometry(new A.AdjustValueList())
                    { Preset = A.ShapeTypeValues.Rectangle },
                fill,
                new A.Outline(new A.NoFill())),
            new WPS.TextBoxInfo2(txbxContent),
            new WPS.TextBodyProperties
            {
                LeftInset = 0, RightInset = 0, TopInset = 0, BottomInset = 0,
                Anchor = A.TextAnchoringTypeValues.Top,
            });

        var anchor = new DW.Anchor(
            new DW.SimplePosition { X = 0L, Y = 0L },
            new DW.HorizontalPosition(new DW.PositionOffset(x.ToString()))
                { RelativeFrom = DW.HorizontalRelativePositionValues.Page },
            new DW.VerticalPosition(new DW.PositionOffset(y.ToString()))
                { RelativeFrom = DW.VerticalRelativePositionValues.Page },
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.WrapNone(),
            new DW.DocProperties { Id = id, Name = $"TextBox{id}" },
            new DW.NonVisualGraphicFrameDrawingProperties(),
            new A.Graphic(
                new A.GraphicData(wsp)
                {
                    Uri = "http://schemas.microsoft.com/office/word/2010/wordprocessingShape"
                }))
        {
            SimplePos      = false,
            BehindDoc      = false,
            DistanceFromTop    = 0U,
            DistanceFromBottom = 0U,
            DistanceFromLeft   = 0U,
            DistanceFromRight  = 0U,
            Locked         = false,
            LayoutInCell   = true,
            AllowOverlap   = true,
            RelativeHeight = zh,
        };

        return new Paragraph(new Run(new Drawing(anchor)));
    }

    private static Paragraph CreateShapeParagraph(
        ElementDto el,
        LayoutContext layout,
        A.ShapeTypeValues preset,
        string fillHex,
        string strokeHex,
        int strokePx)
    {
        var x  = WordUnitConverter.CanvasToEmu(Math.Max(0, el.X));
        var y  = WordUnitConverter.CanvasToEmu(Math.Max(0, el.Y));
        var cx = WordUnitConverter.CanvasToEmu(el.Width  > 0 ? el.Width  : 1);
        var cy = WordUnitConverter.CanvasToEmu(el.Height > 0 ? el.Height : 1);
        var id = layout.NextDrawingId++;
        var zh = layout.DrawingOrder++;

        OpenXmlElement fill = string.IsNullOrEmpty(fillHex)
            ? new A.NoFill()
            : new A.SolidFill(new A.RgbColorModelHex { Val = fillHex });

        var strokeEmu = (long)(strokePx * 12700);
        OpenXmlElement outline = string.IsNullOrEmpty(strokeHex) || strokePx <= 0
            ? new A.Outline(new A.NoFill())
            : new A.Outline(new A.SolidFill(new A.RgbColorModelHex { Val = strokeHex })) { Width = (int)strokeEmu };

        var wsp = new WPS.WordprocessingShape(
            new WPS.NonVisualDrawingShapeProperties(),
            new WPS.ShapeProperties(
                new A.Transform2D(
                    new A.Offset  { X = 0L, Y = 0L },
                    new A.Extents { Cx = cx, Cy = cy }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = preset },
                fill,
                outline),
            new WPS.TextBodyProperties());

        var anchor = new DW.Anchor(
            new DW.SimplePosition { X = 0L, Y = 0L },
            new DW.HorizontalPosition(new DW.PositionOffset(x.ToString()))
                { RelativeFrom = DW.HorizontalRelativePositionValues.Page },
            new DW.VerticalPosition(new DW.PositionOffset(y.ToString()))
                { RelativeFrom = DW.VerticalRelativePositionValues.Page },
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.WrapNone(),
            new DW.DocProperties { Id = id, Name = $"Shape{id}" },
            new DW.NonVisualGraphicFrameDrawingProperties(),
            new A.Graphic(
                new A.GraphicData(wsp)
                {
                    Uri = "http://schemas.microsoft.com/office/word/2010/wordprocessingShape"
                }))
        {
            SimplePos      = false,
            BehindDoc      = false,
            DistanceFromTop    = 0U,
            DistanceFromBottom = 0U,
            DistanceFromLeft   = 0U,
            DistanceFromRight  = 0U,
            Locked         = false,
            LayoutInCell   = true,
            AllowOverlap   = true,
            RelativeHeight = zh,
        };

        return new Paragraph(new Run(new Drawing(anchor)));
    }

    private static void AdvanceCursor(LayoutContext layout, ElementDto el)
    {
        var h = el.Height > 0 ? el.Height : 18;
        layout.CursorY = Math.Max(layout.CursorY, el.Y + h);
    }

    private static int GetElementZIndex(ElementDto el)
    {
        var s = el.Style ?? [];
        return (int)Math.Round(s.GetNum("zIndex", 0), MidpointRounding.AwayFromZero);
    }

    private static bool IsBoldWeight(string weight)
    {
        var normalized = weight.Trim().ToLowerInvariant();
        return normalized == "bold" ||
               normalized == "bolder" ||
               normalized is "600" or "700" or "800" or "900";
    }

    private static bool HasDecoration(string decorations, string token)
        => decorations.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(d => d == token);

    private static string ResolveFontFamily(string rawFontFamily)
    {
        if (string.IsNullOrWhiteSpace(rawFontFamily))
            return string.Empty;

        var first = rawFontFamily
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().Trim('\'', '"'))
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(first))
            return string.Empty;

        return FontFamilyFallbackMap.TryGetValue(first, out var mapped) ? mapped : first;
    }

    private static void ApplySectionGeometry(Body body, PageSettingsDto? settings)
    {
        var widthUnits = settings?.Width > 0 ? settings.Width : 595;
        var heightUnits = settings?.Height > 0 ? settings.Height : 842;
        var orientation = settings?.Orientation?.ToLowerInvariant() ?? "portrait";
        var isLandscape = orientation == "landscape";

        var pageWidthTwips = WordUnitConverter.CanvasToTwips(widthUnits);
        var pageHeightTwips = WordUnitConverter.CanvasToTwips(heightUnits);

        if (isLandscape && pageWidthTwips < pageHeightTwips)
            (pageWidthTwips, pageHeightTwips) = (pageHeightTwips, pageWidthTwips);
        else if (!isLandscape && pageWidthTwips > pageHeightTwips)
            (pageWidthTwips, pageHeightTwips) = (pageHeightTwips, pageWidthTwips);

        var margins = settings?.Margins;
        var sectPr = new SectionProperties(
            new PageSize
            {
                Width = new UInt32Value((uint)Math.Max(pageWidthTwips, 1)),
                Height = new UInt32Value((uint)Math.Max(pageHeightTwips, 1)),
                Orient = isLandscape ? PageOrientationValues.Landscape : PageOrientationValues.Portrait,
            },
            new PageMargin
            {
                Left = new UInt32Value((uint)Math.Max(margins is null ? 0 : WordUnitConverter.CanvasToTwips(margins.Left), 0)),
                Right = new UInt32Value((uint)Math.Max(margins is null ? 0 : WordUnitConverter.CanvasToTwips(margins.Right), 0)),
                Top = new Int32Value(margins is null ? 0 : WordUnitConverter.CanvasToTwips(margins.Top)),
                Bottom = new Int32Value(margins is null ? 0 : WordUnitConverter.CanvasToTwips(margins.Bottom)),
                Header = new UInt32Value(0U),
                Footer = new UInt32Value(0U),
                Gutter = new UInt32Value(0U),
            });

        body.AppendChild(sectPr);
    }

    /// <summary>Injects a character style reference into the run's RunProperties when the element has one.</summary>
    private static void ApplyCharacterStyle(Run run, ElementDto el)
    {
        if (string.IsNullOrWhiteSpace(el.CharacterStyle)) return;
        var rPr = run.GetFirstChild<RunProperties>() ?? run.PrependChild(new RunProperties());
        rPr.InsertAt(new RunStyle { Val = el.CharacterStyle }, 0);
    }

    /// <summary>
    /// Wraps a paragraph (or returns it unchanged) in a <c>&lt;w:ins&gt;</c> or <c>&lt;w:del&gt;</c>
    /// revision block when the element carries revision metadata.
    /// </summary>
    private static Paragraph WrapWithRevision(Paragraph para, ElementDto el)
    {
        if (string.IsNullOrWhiteSpace(el.RevisionType)) return para;

        var revId  = Math.Abs((el.RevisionId ?? el.Id).GetHashCode()) % 100000;
        var author = el.RevisionAuthor ?? "Canvas";
        var date   = DateTime.TryParse(el.RevisionDate, out var dt) ? dt.ToUniversalTime() : DateTime.UtcNow;

        if (el.RevisionType == "delete")
        {
            foreach (var run in para.Elements<Run>())
                foreach (var t in run.Elements<Text>().ToList())
                {
                    t.InsertBeforeSelf(new DeletedText(t.Text) { Space = SpaceProcessingModeValues.Preserve });
                    t.Remove();
                }

            var wDel = new DeletedRun { Id = revId.ToString(), Author = author, Date = date };
            foreach (var child in para.ChildElements.OfType<Run>().ToList())
            {
                child.Remove();
                wDel.Append((Run)child.CloneNode(true));
            }
            para.AppendChild(wDel);
            return para;
        }

        if (el.RevisionType == "insert")
        {
            var wIns = new InsertedRun { Id = revId.ToString(), Author = author, Date = date };
            foreach (var child in para.ChildElements.OfType<Run>().ToList())
            {
                child.Remove();
                wIns.Append((Run)child.CloneNode(true));
            }
            para.AppendChild(wIns);
            return para;
        }

        // "format" — record a run-property change marker on each run
        foreach (var run in para.Elements<Run>())
        {
            var rPr = run.GetFirstChild<RunProperties>() ?? run.PrependChild(new RunProperties());
            rPr.AppendChild(new RunPropertiesChange { Id = revId.ToString(), Author = author, Date = date });
        }
        return para;
    }

    private static void ApplyDocumentAutoHyphenation(WordprocessingDocument doc)
    {
        var main     = doc.MainDocumentPart!;
        var settings = main.DocumentSettingsPart ?? main.AddNewPart<DocumentSettingsPart>();
        settings.Settings ??= new Settings();
        settings.Settings.RemoveAllChildren<AutoHyphenation>();
        settings.Settings.InsertAt(new AutoHyphenation { Val = true }, 0);
        settings.Settings.Save();
    }

    private static string NormalizeHexColor(string raw, string fallback)
    {
        if (string.IsNullOrWhiteSpace(raw)) return fallback;

        var value = raw.Trim().TrimStart('#');
        if (value.Length == 3 && value.All(Uri.IsHexDigit))
            return string.Concat(value.Select(c => $"{char.ToUpperInvariant(c)}{char.ToUpperInvariant(c)}"));

        return value.Length == 6 && value.All(Uri.IsHexDigit)
            ? value.ToUpperInvariant()
            : fallback.ToUpperInvariant();
    }

}
