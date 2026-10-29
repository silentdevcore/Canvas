# PXA.Pdf Provider Feature Gaps

> Naming history: **PXA has been renamed to PVAD (technical name). PVADE is the market name.** PDF uses PVADP and Spreadsheet uses PVADS. Historical PXA/Canvas product references, implementation notes and compatibility identifiers are retained; task statuses are unchanged.

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
- [x] AES-128 interoperability and AES-256 revision 6 encryption, permissions, Unicode passwords,
      encrypted-metadata control, existing-document decryption, and replacement encryption.
- [x] PAdES B-B, B-T, B-LT, and B-LTA signing and validation with timestamps, trust policy,
      revocation evidence, certification policy, and Viewer workflows.
- [x] PDF/A-2b and PDF/A-3b generation, bounded existing-PDF conversion, structural preflight,
      and pinned veraPDF evidence.
- [x] Tagged PDF and PDF/UA-1 generation with Designer remediation, Viewer reporting,
      automated validation evidence, and an explicit manual-review boundary.
- [x] Fail-closed applied redaction for the documented supported corpus, including sanitization,
      post-save verification, audit evidence, image/resource handling, and Viewer comparison.

## High Priority

- [ ] **[high] Existing PDF editing and page operations** - Load existing PDFs, append/merge, split, insert/delete/import pages, preserve object graphs, and save edited documents. Seen in: DevExpress, Aspose.PDF, iText 7, Apryse, Foxit, DsPdf, PDFTools. PXA.Pdf status: `missing` as generation API; related parsing/editing foundation exists in `PXA.Importer`.
- [x] **[high] PDF digital signatures** - PXA supports PAdES B-B, B-T, B-LT, and B-LTA signing and validation, document timestamps, signature fields, trust/revocation policy, certification signatures, and signature-preserving incremental updates. Remote qualified-signature providers remain outside this milestone.
- [x] **[high] PDF/A and compliance workflows** - PXA generates PDF/A-2b and PDF/A-3b, preflights and converts the documented existing-PDF subset, retains machine-readable veraPDF evidence, and blocks unsupported repair rather than claiming success. PDF/A-4 remains deferred.
- [ ] **[high] HTML/CSS/URL/Razor to PDF rendering** - Render HTML strings, files, URLs, Razor/views, and CSS layout directly to PDF. Seen in: IronPDF, ActivePDF, Aspose.PDF, Syncfusion. PXA.Pdf status: `missing`; current migration emits manual diagnostics for this category.
- [x] **[high] Secure redaction** - PXA provides read-only preflight, bounded object-level removal, full-rewrite sanitization, post-save residue verification, signed/timestampable audit evidence, external recovery probes, and Viewer comparison for the documented supported corpus. Unsupported codecs and ambiguous structures fail closed. Tracked in `PVAD.PDF-Trust-Foundation.md`.

## Medium Priority

- [ ] **[medium] Advanced AcroForms** - Radio buttons, push buttons, list boxes, signature fields, calculated/action fields, field import/export, appearance stream control, and form flattening. Seen in: DevExpress, Aspose.PDF, iText 7, Foxit, Apryse. PXA.Pdf status: `partial`.
- [ ] **[medium] Advanced annotations and stamps** - Text notes, highlight/underline/squiggly, ink, popup, rubber stamp, file attachment annotations, and review workflows. Seen in: DevExpress, Aspose.PDF, iText 7, Foxit, Apryse. PXA.Pdf status: `partial`; link annotations exist.
- [ ] **[medium] Attachments and portfolios** - Embed arbitrary files, manage associated files, and create PDF packages/portfolios. Seen in: Foxit, Spire.PDF, PDFTools, Aspose.PDF. PXA.Pdf status: `missing`.
- [x] **[medium] Tagged PDF and accessibility** - PXA supports structure and parent trees, role mapping, artifacts, alternatives, reading order, tables, native widgets, PDF/UA-1 output, Designer remediation, and pinned automated validation. Manual assistive-technology review remains required for an accessibility claim.
- [ ] **[medium] Layers / optional content groups** - Create and control visible/toggleable PDF layers. Seen in: iText 7, Apryse, Aspose.PDF. PXA.Pdf status: `missing`.
- [ ] **[medium] Advanced graphics resources** - Gradients/shadings, tiling patterns, soft masks beyond image alpha, transparency groups, blend modes, clipping paths, and reusable XObjects/templates. Seen in: iText 7, Aspose.PDF, Apryse, Foxit. PXA.Pdf status: `partial`.
- [ ] **[medium] Advanced layout and table engine** - Cell spans, nested tables, floating blocks, keep-together, advanced page breaking, reusable renderer model, and HTML fragments. Seen in: iText 7, Aspose.PDF, Syncfusion, DsPdf. PXA.Pdf status: `partial`.
- [x] **[medium] Rendering, viewer, print, and PDF-to-image** - PXA has page rasterization, thumbnails, browser Viewer controls, PDF-based print, visual-diff tooling, and multi-page image export. Advanced plug-in and collaboration features remain separate Viewer roadmap items.
- [ ] **[medium] OCR and conversion workflows** - PDF/image/Office conversion, OCR extraction, and document conversion pipelines. Seen in: LEADTOOLS, Foxit, PDFTools, ActivePDF. PXA.Pdf status: `outside engine`; related file importer/image OCR projects exist separately.

## Low Priority

- [x] **[low] AES and public-key encryption** - PXA supports AES-128, AES-256 revision 6, RC4 compatibility, password permissions, existing-document security handlers, replacement encryption, and Adobe.PubSec X.509 recipient encryption. The full external reader matrix remains release evidence rather than an implementation gap.
- [ ] **[low] Optimization and linearization** - Fast Web View, object streams, duplicate resource elimination, image downsampling, and optimizer profiles. Seen in: Apryse, Aspose.PDF, PDFTools. PXA.Pdf status: `partial`; content stream compression exists.
- [ ] **[low] Low-level PDF object API** - Direct COS/SDF object manipulation, content stream editing, low-level resource dictionaries, and incremental object control. Seen in: Apryse, iText 7, Foxit. PXA.Pdf status: `missing` in generator; `PXA.Importer` has lower-level parser/model foundations.
- [ ] **[low] Native barcode/QR PDF helpers** - Direct PDF-layer barcode/QR generation rather than designer/image-level representation. Seen in: Syncfusion, DevExpress Reporting, Aspose ecosystems. PXA.Pdf status: `missing/adjacent`; designer/report conversion can carry barcode-like elements.

## Implementation Order

The security, signature, compliance, accessibility, and irreversible-redaction
work is consolidated in
[PVAD.PDF-Trust-Foundation.md](PVAD.PDF-Trust-Foundation.md). That checklist is the
implementation tracker for those trust-sensitive capabilities.

1. Existing PDF editing bridge: define where `PXA.Importer` editing ends and `PXA.Pdf` regeneration begins.
2. Advanced AcroForms and annotations: complete form surface and appearance regeneration.
3. Attachments, portfolios, layers, and low-level public editing APIs.
4. Advanced graphics resources: shading/pattern/XObject APIs to improve import round-trips.
5. Optimization, linearization, and larger interoperability/performance corpora.

## Notes

- This is a feature roadmap, not a promise that every provider feature belongs in `PXA.Pdf`.
- Some provider features are better served by adjacent modules (`PXA.Importer`, file importers, image OCR, or migration tools) rather than the PDF generation API itself.
- Keep priority tied to product value and migration blockers, not only to provider checklists.
- Specialized domains such as e-invoicing, legal workflows, PDF/X, capture, PDF/VT,
  GeoPDF, PDF/E/3D, rich media, and provenance are centralized in
  [PVAD.PDF-Specialized-Features-Roadmap.md](PVAD.PDF-Specialized-Features-Roadmap.md).
