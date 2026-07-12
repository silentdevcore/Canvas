# PXA.Pdf Provider Feature Gaps

## Compared Providers

This checklist compares `PXA.Pdf` with strong, commonly used PDF generator/framework families:

- DevExpress PDF
- Aspose.PDF
- iText 7
- Syncfusion PDF
- Apryse / PDFTron
- Foxit PDF SDK
- DsPdf / GcPdf
- IronPDF
- PDFTools / Pdftools SDK and PDFTools Toolbox

The comparison is based on repository checklists and known provider capability categories. It tracks PDF engine/generator features, not migration-converter implementation gaps.

## Already Supported In PXA.Pdf

- [x] Multi-page document generation.
- [x] Standard fonts and embedded TrueType/OpenType fonts.
- [x] Unicode and RTL text support for multiple writing systems.
- [x] Paragraph wrapping, alignment, justification, flow layout, and lists.
- [x] Basic and styled tables with pagination helpers.
- [x] PNG/JPEG image rendering, alpha masks, opacity, clipping, and fit helpers.
- [x] Vector primitives: line, rectangle, rounded rectangle, circle, polygon, Bezier curve.
- [x] RGB, grayscale, and CMYK colors.
- [x] Links, named destinations, bookmarks/outlines, and table of contents.
- [x] Headers, footers, page numbers, sections, and watermarks.
- [x] Document metadata, page boundaries, viewer preferences, page mode/layout.
- [x] Basic AcroForm fields: text field, multiline text field, combo box, checkbox.
- [x] Optional content stream compression.
- [x] RC4-128 Standard Security Handler encryption with owner/user password and permissions.

## High Priority

- [ ] **[high] Existing PDF editing and page operations** - Load existing PDFs, append/merge, split, insert/delete/import pages, preserve object graphs, and save edited documents. Seen in: DevExpress, Aspose.PDF, iText 7, Apryse, Foxit, DsPdf, PDFTools. PXA.Pdf status: `missing` as generation API; related parsing/editing foundation exists in `PXA.Importer`.
- [ ] **[high] PDF digital signatures** - Sign PDFs with certificates, create/use signature fields, timestamp, validate signatures, and preserve existing signatures. Seen in: iText 7, Aspose.PDF, DevExpress, Foxit, DsPdf, PDFTools. PXA.Pdf status: `missing`; DOCX signing exists separately.
- [ ] **[high] PDF/A and compliance workflows** - Produce PDF/A, validate conformance, convert to archival profiles, and repair invalid documents. Seen in: Syncfusion, DsPdf, Aspose.PDF, Apryse, PDFTools. PXA.Pdf status: `missing`.
- [ ] **[high] HTML/CSS/URL/Razor to PDF rendering** - Render HTML strings, files, URLs, Razor/views, and CSS layout directly to PDF. Seen in: IronPDF, ActivePDF, Aspose.PDF, Syncfusion. PXA.Pdf status: `missing`; current migration emits manual diagnostics for this category.
- [ ] **[high] Secure redaction** - Remove redacted text/graphics from content streams and related resources, not just draw visual overlays. Seen in: Aspose.PDF, Foxit, DsPdf, Apryse. PXA.Pdf status: `missing`.

## Medium Priority

- [ ] **[medium] Advanced AcroForms** - Radio buttons, push buttons, list boxes, signature fields, calculated/action fields, field import/export, appearance stream control, and form flattening. Seen in: DevExpress, Aspose.PDF, iText 7, Foxit, Apryse. PXA.Pdf status: `partial`.
- [ ] **[medium] Advanced annotations and stamps** - Text notes, highlight/underline/squiggly, ink, popup, rubber stamp, file attachment annotations, and review workflows. Seen in: DevExpress, Aspose.PDF, iText 7, Foxit, Apryse. PXA.Pdf status: `partial`; link annotations exist.
- [ ] **[medium] Attachments and portfolios** - Embed arbitrary files, manage associated files, and create PDF packages/portfolios. Seen in: Foxit, Spire.PDF, PDFTools, Aspose.PDF. PXA.Pdf status: `missing`.
- [ ] **[medium] Tagged PDF and accessibility** - Structure tree, role mapping, artifacts, alt text, reading order, and PDF/UA-style output. Seen in: iText 7, Aspose.PDF, Apryse, PDFTools. PXA.Pdf status: `missing`.
- [ ] **[medium] Layers / optional content groups** - Create and control visible/toggleable PDF layers. Seen in: iText 7, Apryse, Aspose.PDF. PXA.Pdf status: `missing`.
- [ ] **[medium] Advanced graphics resources** - Gradients/shadings, tiling patterns, soft masks beyond image alpha, transparency groups, blend modes, clipping paths, and reusable XObjects/templates. Seen in: iText 7, Aspose.PDF, Apryse, Foxit. PXA.Pdf status: `partial`.
- [ ] **[medium] Advanced layout and table engine** - Cell spans, nested tables, floating blocks, keep-together, advanced page breaking, reusable renderer model, and HTML fragments. Seen in: iText 7, Aspose.PDF, Syncfusion, DsPdf. PXA.Pdf status: `partial`.
- [ ] **[medium] Rendering, viewer, print, and PDF-to-image** - Render PDF pages to images, viewer controls, print pipelines, and page rasterization. Seen in: Foxit, Apryse, PDFTools, ActivePDF. PXA.Pdf status: `missing`.
- [ ] **[medium] OCR and conversion workflows** - PDF/image/Office conversion, OCR extraction, and document conversion pipelines. Seen in: LEADTOOLS, Foxit, PDFTools, ActivePDF. PXA.Pdf status: `outside engine`; related file importer/image OCR projects exist separately.

## Low Priority

- [ ] **[low] AES and public-key encryption** - AES-128, AES-256, certificate/public-key encryption, and newer security revisions. Seen in: iText 7, Aspose.PDF, DevExpress, Foxit, Apryse. PXA.Pdf status: `planned/partial`; RC4-128 exists and AES-128 is deferred in `Pdf-Encryption.md`.
- [ ] **[low] Optimization and linearization** - Fast Web View, object streams, duplicate resource elimination, image downsampling, and optimizer profiles. Seen in: Apryse, Aspose.PDF, PDFTools. PXA.Pdf status: `partial`; content stream compression exists.
- [ ] **[low] Low-level PDF object API** - Direct COS/SDF object manipulation, content stream editing, low-level resource dictionaries, and incremental object control. Seen in: Apryse, iText 7, Foxit. PXA.Pdf status: `missing` in generator; `PXA.Importer` has lower-level parser/model foundations.
- [ ] **[low] Native barcode/QR PDF helpers** - Direct PDF-layer barcode/QR generation rather than designer/image-level representation. Seen in: Syncfusion, DevExpress Reporting, Aspose ecosystems. PXA.Pdf status: `missing/adjacent`; designer/report conversion can carry barcode-like elements.

## Implementation Order

1. Existing PDF editing bridge: define where `PXA.Importer` editing ends and `PXA.Pdf` regeneration begins.
2. PDF digital signatures: add signing model and writer integration for generated PDFs.
3. PDF/A/compliance: define target profiles and validation strategy.
4. Advanced AcroForms and annotations: complete form surface before form flattening/signature-field workflows.
5. Advanced graphics resources: shading/pattern/XObject APIs to improve import round-trips.
6. AES encryption: implement AES-128 first, then decide whether AES-256/public-key support is worth the complexity.

## Notes

- This is a feature roadmap, not a promise that every provider feature belongs in `PXA.Pdf`.
- Some provider features are better served by adjacent modules (`PXA.Importer`, file importers, image OCR, or migration tools) rather than the PDF generation API itself.
- Keep priority tied to product value and migration blockers, not only to provider checklists.
