using System.Globalization;
using System.Text.Json;
using Canvas.Core.Abstractions;
using QRCoder;
using ZXing;
using ZXing.Common;
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

            case "textarea":
            {
                var label = el.FieldLabel ?? "";
                var req   = el.Required == true ? " *" : "";
                var para  = new Paragraph();
                var ppr   = new ParagraphProperties();
                ApplyParagraphPositioning(ppr, el, layout, applyTopOffset: true);
                para.PrependChild(ppr);

                if (!string.IsNullOrEmpty(label))
                {
                    var run = para.AppendChild(new Run());
                    run.PrependChild(BuildRunProperties(s, "1F2937", forceBold: true));
                    run.AppendChild(new Text($"{label}: "));

                    if (!string.IsNullOrEmpty(req))
                    {
                        var reqRun = para.AppendChild(new Run());
                        reqRun.PrependChild(new RunProperties(new Color { Val = "DC2626" }));
                        reqRun.AppendChild(new Text(req + " "));
                    }
                }

                var lineCount = Math.Max(2, (int)Math.Round(el.Height / 20.0));
                var run2 = para.AppendChild(new Run());
                run2.PrependChild(BuildRunProperties(s, "1F2937", forceUnderline: true));
                run2.AppendChild(new Text(string.Join("\n", Enumerable.Repeat("_______________", lineCount)))
                    { Space = SpaceProcessingModeValues.Preserve });

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

            case "toc":
            {
                // Native Word TOC field — Word updates page numbers on document open / Ctrl+A → F9.
                var tocTitle  = el.TocTitle ?? "Table of Contents";
                var minLevel  = el.TocMinLevel ?? 1;
                var maxLevel  = el.TocMaxLevel ?? 3;
                var instrText = $"TOC \\o \"{minLevel}-{maxLevel}\" \\h \\z \\u";

                // Optional: emit the title as a styled paragraph before the field.
                if (!string.IsNullOrWhiteSpace(tocTitle))
                {
                    var titlePara = new Paragraph();
                    var titlePpr  = new ParagraphProperties();
                    ApplyParagraphPositioning(titlePpr, el, layout, applyTopOffset: true);
                    titlePara.PrependChild(titlePpr);
                    var titleRun = new Run();
                    titleRun.AppendChild(new RunProperties(new Bold()));
                    titleRun.AppendChild(new Text(tocTitle) { Space = SpaceProcessingModeValues.Preserve });
                    titlePara.AppendChild(titleRun);
                    body.AppendChild(titlePara);
                }

                // TOC field: <w:p><w:r><w:fldChar begin/><w:instrText TOC .../><w:fldChar end/></w:r></w:p>
                var tocPara = new Paragraph();
                var tocPpr  = new ParagraphProperties();
                if (string.IsNullOrWhiteSpace(tocTitle))
                    ApplyParagraphPositioning(tocPpr, el, layout, applyTopOffset: true);
                tocPara.PrependChild(tocPpr);

                var beginRun = new Run();
                beginRun.AppendChild(new FieldChar { FieldCharType = FieldCharValues.Begin, Dirty = true });
                tocPara.AppendChild(beginRun);

                var instrRun = new Run();
                instrRun.AppendChild(new FieldCode(instrText) { Space = SpaceProcessingModeValues.Preserve });
                tocPara.AppendChild(instrRun);

                var endRun = new Run();
                endRun.AppendChild(new FieldChar { FieldCharType = FieldCharValues.End });
                tocPara.AppendChild(endRun);

                body.AppendChild(tocPara);
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

            case "date":
            {
                var fmt  = el.DateFormat ?? "yyyy-MM-dd";
                var now  = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(el.Timezone))
                {
                    try { now = TimeZoneInfo.ConvertTimeFromUtc(now, TimeZoneInfo.FindSystemTimeZoneById(el.Timezone)); }
                    catch { /* unsupported TZ — stay UTC */ }
                }
                var text = now.ToString(fmt, CultureInfo.InvariantCulture);
                var para = new Paragraph();
                var ppr  = new ParagraphProperties();
                ApplyParagraphPositioning(ppr, el, layout, applyTopOffset: true);
                para.PrependChild(ppr);
                para.AppendChild(new Run(new Text(text)));
                body.AppendChild(para);
                AdvanceCursor(layout, el);
                break;
            }

            case "dropdown":
            {
                var label   = el.FieldLabel ?? el.FieldName ?? "Dropdown";
                var items   = el.Options     ?? [];
                var current = items.FirstOrDefault() ?? "";

                var sdtProps = new SdtProperties();
                sdtProps.AppendChild(new Tag { Val = el.FieldName ?? el.Id });
                sdtProps.AppendChild(new SdtAlias { Val = label });

                // Combo-box listing with one item per DropdownItems entry
                var combo = new SdtContentDropDownList();
                foreach (var item in items)
                    combo.AppendChild(new ListItem { DisplayText = item, Value = item });
                sdtProps.AppendChild(combo);

                var sdtContent = new SdtContentBlock();
                var innerPara  = new Paragraph();
                var ippr       = new ParagraphProperties();
                ApplyParagraphPositioning(ippr, el, layout, applyTopOffset: true);
                innerPara.PrependChild(ippr);
                innerPara.AppendChild(new Run(new Text(current)));
                sdtContent.AppendChild(innerPara);

                var sdt = new SdtBlock();
                sdt.AppendChild(sdtProps);
                sdt.AppendChild(sdtContent);
                body.AppendChild(sdt);
                AdvanceCursor(layout, el);
                break;
            }

            case "qrcode":
            {
                var value = el.QrValue ?? "";
                if (string.IsNullOrWhiteSpace(value)) { AppendUnsupportedPlaceholder(body, el, layout); break; }
                try { EmbedPngBytes(body, mainPart, el, layout, WqrGenerateQrPng(value)); }
                catch { AppendUnsupportedPlaceholder(body, el, layout); }
                break;
            }

            case "barcode":
            {
                var value = el.BarcodeValue ?? "";
                if (string.IsNullOrWhiteSpace(value)) { AppendUnsupportedPlaceholder(body, el, layout); break; }
                try
                {
                    var w = Math.Max((int)el.Width, 100);
                    var h = Math.Max((int)(el.Height * 0.7), 40);
                    EmbedPngBytes(body, mainPart, el, layout, WqrGenerateBarcodePng(value, el.BarcodeType, w, h));
                }
                catch { AppendUnsupportedPlaceholder(body, el, layout); }
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

        // Legacy flow mode positions the table with a vertical spacer paragraph. V2 floats the
        // table at absolute page coordinates instead (see the tblpPr block below), so the spacer
        // would only push it off-position.
        if (!layout.FidelityV2)
        {
            var topOffset = Math.Max(0, el.Y - layout.CursorY);
            if (topOffset > 0)
            {
                var spacer = new Paragraph();
                var ppr = new ParagraphProperties();
                ppr.AppendChild(new SpacingBetweenLines { Before = WordUnitConverter.CanvasToTwips(topOffset).ToString() });
                spacer.PrependChild(ppr);
                body.AppendChild(spacer);
            }
        }

        var cols       = cellData[0]?.Length ?? 0;
        var bw         = (uint)Math.Max(1, (int)s.GetNum("borderWidth", 1));
        var bc         = NormalizeHexColor(s.GetStr("borderColor", "#000000"), "000000");
        var hasHdr     = el.HeaderRow == true;
        var hdrBg      = NormalizeHexColor(el.HeaderBgColor ?? "#f1f5f9", "f1f5f9");
        var zebraColor = el.ZebraEnabled == true ? NormalizeHexColor(el.ZebraColor ?? "#f9fafb", "f9fafb") : null;
        var matrixHeaders = RdlMatrixHeaders(s);

        // Column widths use Canvas units converted through shared converter.
        // Fall back to equal distribution of element width across columns.
        var colWidthsPx = el.ColumnWidths ?? [];
        var tableWidthPx = el.Width > 0 ? el.Width : 500.0;
        var equalPx = tableWidthPx / Math.Max(cols, 1);
        int ColTwips(int c) => WordUnitConverter.CanvasToTwips(
            colWidthsPx.Length > c ? colWidthsPx[c] : equalPx);

        var alignments = el.ColumnAlignments ?? [];
        var cellStyleLookup = (el.CellStyles ?? []).GroupBy(x => (x.Row, x.Col)).ToDictionary(gp => gp.Key, gp => gp.First());

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
        if (layout.FidelityV2)
        {
            // Float the table at the element's absolute page coordinates so it lines up with the
            // anchored text boxes / shapes/ images rather than flowing from the document cursor.
            // In CT_TblPrBase, w:tblpPr must precede w:tblW — hence PrependChild.
            tblPr.PrependChild(new TablePositionProperties
            {
                LeftFromText = 0, RightFromText = 0, TopFromText = 0, BottomFromText = 0,
                HorizontalAnchor = HorizontalAnchorValues.Page,
                VerticalAnchor   = VerticalAnchorValues.Page,
                TablePositionX   = WordUnitConverter.CanvasToTwips(Math.Max(0, el.X)),
                TablePositionY   = WordUnitConverter.CanvasToTwips(Math.Max(0, el.Y)),
            });
        }
        else
        {
            var leftTwips = WordUnitConverter.CanvasToTwips(Math.Max(0, el.X));
            if (leftTwips > 0)
                tblPr.AppendChild(new TableIndentation { Width = leftTwips, Type = TableWidthUnitValues.Dxa });
        }
        table.AppendChild(tblPr);

        // TableGrid defines column widths
        var grid = new TableGrid();
        for (int c = 0; c < cols; c++)
            grid.AppendChild(new GridColumn { Width = ColTwips(c).ToString() });
        table.AppendChild(grid);

        foreach (var header in matrixHeaders)
            table.AppendChild(CreateMatrixHeaderRow(header, cols, ColTwips, bw, bc));

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
                var cs    = cellStyleLookup.GetValueOrDefault((r, c));
                var align = cs?.TextAlign ?? (alignments.Length > c ? alignments[c] : "left");
                var tc    = new TableCell();

                var tcp = new TableCellProperties(
                    new TableCellWidth { Width = ColTwips(c).ToString(), Type = TableWidthUnitValues.Dxa });
                // CT_TcPr order: tcW(2) → tcBorders(3) → shd(6) → vAlign(11).
                if (cs is not null && HasCellBorder(cs))
                    tcp.AppendChild(BuildCellBorders(cs));
                var bgFill = cs?.BackgroundColor is { } cbg ? NormalizeHexColor(cbg, "ffffff")
                    : isHdr ? hdrBg : isZebra ? zebraColor : null;
                if (bgFill is not null)
                    tcp.AppendChild(new Shading { Fill = bgFill, Val = ShadingPatternValues.Clear, Color = "auto" });
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
                if (BuildCellRunProps(cs, isHdr) is { } rpr) run.PrependChild(rpr);
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

    private static bool HasCellBorder(CellStyleDto cs) =>
        cs.BorderColor != null || cs.BorderWidth != null
        || cs.BorderTop != null || cs.BorderRight != null || cs.BorderBottom != null || cs.BorderLeft != null;

    // Per-cell run properties (font family/size/weight/style/colour). Bold also when it's the header row.
    private static RunProperties? BuildCellRunProps(CellStyleDto? cs, bool isHdr)
    {
        var bold = isHdr || cs?.Bold == true;
        if (!bold && cs is null) return null;
        if (!bold && cs.Italic != true && cs.Color is null && cs.FontFamily is null && cs.FontSize is null)
            return null;

        var rpr = new RunProperties();
        if (cs?.FontFamily is { Length: > 0 } ff) rpr.AppendChild(new RunFonts { Ascii = ff, HighAnsi = ff });
        if (bold) rpr.AppendChild(new Bold());
        if (cs?.Italic == true) rpr.AppendChild(new Italic());
        if (cs?.Color is { Length: > 0 } col) rpr.AppendChild(new Color { Val = NormalizeHexColor(col, "000000") });
        if (cs?.FontSize is { } fsz and > 0) rpr.AppendChild(new FontSize { Val = ((int)Math.Round(fsz * 2)).ToString() });
        return rpr;
    }

    // Per-cell borders: per-side override → uniform fallback → Nil (explicit cell borders replace the
    // table grid for that cell, matching the other exporters). Width follows the table convention (Size = pt).
    private static TableCellBorders BuildCellBorders(CellStyleDto cs) => new()
    {
        TopBorder    = CellSide<TopBorder>(cs.BorderTop, cs),
        LeftBorder   = CellSide<LeftBorder>(cs.BorderLeft, cs),
        BottomBorder = CellSide<BottomBorder>(cs.BorderBottom, cs),
        RightBorder  = CellSide<RightBorder>(cs.BorderRight, cs),
    };

    private static T CellSide<T>(CellBorderSideDto? side, CellStyleDto cs) where T : BorderType, new()
    {
        var hasUniform = cs.BorderColor != null || cs.BorderWidth != null;
        if (side is null && !hasUniform) return new T { Val = BorderValues.Nil };
        var width = side?.Width ?? cs.BorderWidth ?? 1;
        var color = side?.Color ?? cs.BorderColor ?? "#000000";
        return new T
        {
            Val = BorderValues.Single,
            Size = (uint)Math.Max(1, (int)Math.Round(width)),
            Color = NormalizeHexColor(color, "000000"),
        };
    }

    private static TableRow CreateMatrixHeaderRow(string header, int cols, Func<int, int> colTwips, uint borderWidth, string borderColor)
    {
        var tableRow = new TableRow(new TableRowProperties(new TableHeader()));
        var tc = new TableCell();
        var totalWidth = Enumerable.Range(0, Math.Max(cols, 1)).Sum(colTwips);
        var props = new TableCellProperties(
            new TableCellWidth { Width = totalWidth.ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa },
            new GridSpan { Val = Math.Max(cols, 1) },
            new Shading { Fill = "E0F2FE", Val = ShadingPatternValues.Clear, Color = "auto" },
            new TableCellBorders(
                new TopBorder { Val = BorderValues.Single, Size = borderWidth, Color = borderColor },
                new LeftBorder { Val = BorderValues.Single, Size = borderWidth, Color = borderColor },
                new BottomBorder { Val = BorderValues.Single, Size = borderWidth, Color = borderColor },
                new RightBorder { Val = BorderValues.Single, Size = borderWidth, Color = borderColor }));
        tc.AppendChild(props);

        var para = new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Left }));
        var run = para.AppendChild(new Run());
        run.PrependChild(new RunProperties(new Bold(), new Color { Val = "075985" }));
        run.AppendChild(new Text(header) { Space = SpaceProcessingModeValues.Preserve });
        tc.AppendChild(para);
        tableRow.AppendChild(tc);
        return tableRow;
    }

    private static List<string> RdlMatrixHeaders(Dictionary<string, object> style)
    {
        var headers = new List<string>();
        AddRdlMatrixHeaders(style, "rdlTablixColumnHierarchy", headers);
        AddRdlMatrixHeaders(style, "rdlTablixRowHierarchy", headers);
        return headers;
    }

    private static void AddRdlMatrixHeaders(Dictionary<string, object> style, string key, List<string> headers)
    {
        if (!style.TryGetValue(key, out var value) || value is null) return;

        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
        {
            foreach (var item in jsonArray.EnumerateArray())
                AddRdlMatrixHeader(item, headers);
            return;
        }

        if (value is IEnumerable<object> items)
        {
            foreach (var item in items)
                AddRdlMatrixHeader(item, headers);
        }
    }

    private static void AddRdlMatrixHeader(object item, List<string> headers)
    {
        switch (item)
        {
            case JsonElement { ValueKind: JsonValueKind.Object } json:
                var text = JsonProp(json, "headerText") ?? JsonProp(json, "groupName");
                if (!string.IsNullOrWhiteSpace(text)) headers.Add(text);
                break;
            case IReadOnlyDictionary<string, object> dict:
                if ((HeaderValue(dict, "headerText") ?? HeaderValue(dict, "groupName")) is { Length: > 0 } value)
                    headers.Add(value);
                break;
        }
    }

    private static string? JsonProp(JsonElement json, string name) =>
        json.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static string? HeaderValue(IReadOnlyDictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out var value) ? value?.ToString() : null;

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

    private static void EmbedPngBytes(Body body, MainDocumentPart mainPart, ElementDto el, LayoutContext layout, byte[] pngBytes)
    {
        var imagePart = mainPart.AddImagePart(ImagePartType.Png);
        using (var ms = new MemoryStream(pngBytes))
            imagePart.FeedData(ms);
        var relationshipId = mainPart.GetIdOfPart(imagePart);
        long cx = WordUnitConverter.CanvasToEmu(el.Width > 0 ? el.Width : 200);
        long cy = WordUnitConverter.CanvasToEmu(el.Height > 0 ? el.Height : 150);
        var drawingId = layout.NextDrawingId++;
        var drawing = CreateInlineDrawing(relationshipId, el, cx, cy, drawingId, false);
        var para = new Paragraph();
        var ppr = new ParagraphProperties();
        ApplyParagraphPositioning(ppr, el, layout, applyTopOffset: true);
        para.PrependChild(ppr);
        para.AppendChild(new Run(drawing));
        body.AppendChild(para);
        AdvanceCursor(layout, el);
    }

    private static byte[] WqrGenerateQrPng(string value)
    {
        var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(value, QRCodeGenerator.ECCLevel.M);
        var qr = new PngByteQRCode(data);
        return qr.GetGraphic(10);
    }

    private static byte[] WqrGenerateBarcodePng(string value, string? barcodeType, int width, int height)
    {
        var format = barcodeType?.ToLowerInvariant() switch
        {
            "code128" or "code-128" => BarcodeFormat.CODE_128,
            "code39"  or "code-39"  => BarcodeFormat.CODE_39,
            "ean13"   or "ean-13"   => BarcodeFormat.EAN_13,
            "ean8"    or "ean-8"    => BarcodeFormat.EAN_8,
            "upca"    or "upc-a"    => BarcodeFormat.UPC_A,
            "pdf417"                => BarcodeFormat.PDF_417,
            _                       => BarcodeFormat.CODE_128
        };
        var writer = new MultiFormatWriter();
        var hints = new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 2 };
        var matrix = writer.encode(value, format, width, height, hints);
        var rows = matrix.Height;
        var cols = matrix.Width;
        var raw = new byte[rows * (1 + cols)];
        var p = 0;
        for (var y = 0; y < rows; y++)
        {
            raw[p++] = 0; // PNG filter byte
            for (var x = 0; x < cols; x++)
                raw[p++] = matrix[x, y] ? (byte)0 : (byte)255; // ZXing: [col,row]
        }
        return BuildGrayscalePng((uint)cols, (uint)rows, raw);
    }

    private static byte[] BuildGrayscalePng(uint w, uint h, byte[] raw)
    {
        static void Write32(byte[] buf, int off, uint v)
        { buf[off] = (byte)(v >> 24); buf[off + 1] = (byte)(v >> 16); buf[off + 2] = (byte)(v >> 8); buf[off + 3] = (byte)v; }
        static uint Crc32(byte[] data, int start, int len)
        {
            uint c = 0xffffffff;
            for (var i = start; i < start + len; i++)
            { c ^= data[i]; for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xedb88320u ^ (c >> 1) : c >> 1; }
            return ~c;
        }
        // IHDR
        var ihdr = new byte[13];
        Write32(ihdr, 0, w); Write32(ihdr, 4, h);
        ihdr[8] = 8; ihdr[9] = 0; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
        // IDAT (zlib deflate)
        var compressed = Deflate(raw);
        using var ms2 = new MemoryStream();
        using var bw = new BinaryWriter(ms2);
        bw.Write(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }); // PNG sig
        void WriteChunk(string name, byte[] data)
        {
            var nameBytes = System.Text.Encoding.ASCII.GetBytes(name);
            bw.Write((uint)System.Net.IPAddress.HostToNetworkOrder((int)data.Length));
            bw.Write(nameBytes);
            bw.Write(data);
            var crcData = new byte[4 + data.Length];
            nameBytes.CopyTo(crcData, 0);
            data.CopyTo(crcData, 4);
            bw.Write((uint)System.Net.IPAddress.HostToNetworkOrder((int)Crc32(crcData, 0, crcData.Length)));
        }
        WriteChunk("IHDR", ihdr);
        WriteChunk("IDAT", compressed);
        WriteChunk("IEND", []);
        bw.Flush();
        return ms2.ToArray();
    }

    private static byte[] Deflate(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var ds = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Compress, true))
            ds.Write(data, 0, data.Length);
        var deflated = ms.ToArray();
        // wrap in zlib: 0x78 0x9C header + adler32 trailer
        var adler = Adler32(data);
        var result = new byte[2 + deflated.Length + 4];
        result[0] = 0x78; result[1] = 0x9C;
        deflated.CopyTo(result, 2);
        result[^4] = (byte)(adler >> 24); result[^3] = (byte)(adler >> 16);
        result[^2] = (byte)(adler >> 8);  result[^1] = (byte)adler;
        return result;
    }

    private static uint Adler32(byte[] data)
    {
        uint s1 = 1, s2 = 0;
        foreach (var b in data) { s1 = (s1 + b) % 65521; s2 = (s2 + s1) % 65521; }
        return (s2 << 16) | s1;
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

    // Reused across calls to avoid socket exhaustion; per-request timeouts come from a linked CTS.
    private static readonly System.Net.Http.HttpClient RemoteImageClient = new()
    {
        Timeout = System.Threading.Timeout.InfiniteTimeSpan,
    };

    // Hard cap on a fetched remote image to bound memory use (8 MiB).
    private const long MaxRemoteImageBytes = 8L * 1024 * 1024;

    private static byte[]? FetchRemoteImageWithRetry(string url, int maxAttempts, int timeoutSeconds, CancellationToken cancellationToken)
    {
        if (!IsSafeRemoteImageUrl(url))
            return null;

        for (var attempt = 1; attempt <= Math.Max(1, maxAttempts); attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)));

                var bytes = ReadCappedResponse(url, timeoutCts.Token);
                if (bytes is { Length: > 0 })
                    return bytes;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // caller cancelled — propagate
            }
            catch
            {
                // per-request timeout or transport error — retry until attempts exhausted
                if (attempt == maxAttempts)
                    break;
            }
        }

        return null;
    }

    private static byte[]? ReadCappedResponse(string url, CancellationToken cancellationToken)
    {
        using var response = RemoteImageClient
            .GetAsync(url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is long declared && declared > MaxRemoteImageBytes)
            return null;

        using var stream = response.Content.ReadAsStreamAsync(cancellationToken).GetAwaiter().GetResult();
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ms.Write(buffer, 0, read);
            if (ms.Length > MaxRemoteImageBytes)
                return null; // exceeded cap mid-stream
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Guards remote image fetches against SSRF: only http/https on default-ish hosts whose
    /// resolved addresses are publicly routable (no loopback, private, link-local, or multicast).
    /// </summary>
    private static bool IsSafeRemoteImageUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        try
        {
            System.Net.IPAddress[] addresses = System.Net.IPAddress.TryParse(uri.Host, out var literal)
                ? [literal]
                : System.Net.Dns.GetHostAddresses(uri.Host);

            if (addresses.Length == 0)
                return false;

            // Reject if ANY resolved address is non-public — defends against DNS that
            // returns a mix of public and internal targets.
            return addresses.All(IsPubliclyRoutable);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPubliclyRoutable(System.Net.IPAddress ip)
    {
        if (System.Net.IPAddress.IsLoopback(ip))
            return false;

        var bytes = ip.GetAddressBytes();

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            // IPv4 private / link-local / metadata ranges
            if (bytes[0] == 10) return false;                                  // 10.0.0.0/8
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return false; // 172.16.0.0/12
            if (bytes[0] == 192 && bytes[1] == 168) return false;              // 192.168.0.0/16
            if (bytes[0] == 169 && bytes[1] == 254) return false;              // 169.254.0.0/16 (link-local / cloud metadata)
            if (bytes[0] == 127) return false;                                 // loopback
            if (bytes[0] == 0) return false;                                   // 0.0.0.0/8
            if (bytes[0] >= 224) return false;                                 // multicast / reserved
            return true;
        }

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6Multicast || ip.IsIPv6SiteLocal)
                return false;
            if ((bytes[0] & 0xFE) == 0xFC) return false; // fc00::/7 unique local
            // Map IPv4-mapped IPv6 back to IPv4 rules.
            if (ip.IsIPv4MappedToIPv6)
                return IsPubliclyRoutable(ip.MapToIPv4());
            return true;
        }

        return false;
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
