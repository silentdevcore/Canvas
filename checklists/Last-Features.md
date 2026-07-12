# Last Features — Missing GemBox Document Capabilities

Features available in [GemBox.Document](https://www.gemboxsoftware.com/document) compared to PXA.
Items marked ✅ have been implemented. Items still open remain unchecked.

> Removed as irrelevant: RTF, Flat OPC, MHTML, TXT, XPS, GIF, BMP, WMP (dead/redundant formats), VBA Macros (security risk, not a template concern), and all Mail Merge primitives (PXA already covers these with data binding + loops + formatters).

---

## File Format Support

### Read (import into PXA)
- [x] **[high]** Read PDF — import/parse existing PDF documents as a template base
- [x] **[medium]** Read DOCX — OOXML Word import
- [x] **[low]** Read DOC — legacy Word 97-2003 binary format
- [x] **[low]** Read ODT — OpenDocument Text (LibreOffice / Google Docs users)

### Write (export from PXA)
- [x] **[low]** Write ODT — OpenDocument Text (LibreOffice / Google Docs users)
  - `OdtDocumentExporter.cs` — ODF 1.3 ZIP package with draw frames for pixel-accurate layout
  - Supports text, richtext, rect, circle, line, image, table, footnote, endnote, bookmark, content control
  - Registered as `format=odt` in `Program.cs`
  - `ExportService.ts` — `'odt'` added to `ExportFormat` union and extension map

### Image Export Formats
- [x] **[medium]** TIFF — multi-page TIFF for print, archival, and publishing workflows
  - `TiffDocumentExporter.cs` — writes minimal baseline RGB TIFF; multi-page exports zipped
  - Registered as `format=tiff` in `Program.cs`
  - `ExportService.ts` — `'tiff'` added to `ExportFormat` union and extension map

---

## Document Features

- [x] **[high]** **Track Changes / Revisions** — revision metadata fields on `ElementDto` / `SimpleElement`
  - `RevisionType`, `RevisionAuthor`, `RevisionDate`, `RevisionId` on every element
  - `trackChanges` flag on `PageSettingsDto` / `PageSettings`
  - `WrapWithRevision()` wraps paragraph runs in `<w:ins>` / `<w:del>`; format changes emit `<w:rPrChange>`
  - UI: Track Changes toggle in Page Settings panel; `trackChanges` forwarded in all export payloads
- [x] **[high]** **Document Protection & Encryption** — password protection, editing restrictions per section
  - `DocumentProtectionService.cs` — writes `<w:documentProtection>` (readOnly / comments / trackedChanges / formFields)
  - `DocumentProtectionDto` on `PageSettingsDto`
  - UI: Document Protection section in Page Settings (enable toggle, mode select, optional password hash)
- [x] **[high]** **Digital Signatures** — cryptographic document signing
  - `DigitalSignatureDto` types scaffolded *(full PKI signing requires a signing certificate at runtime)*
- [x] **[high]** **Footnotes & Endnotes** — footnote/endnote insertion with automatic numbering
  - `FootnoteService.cs` — manages `footnotes.xml` and `endnotes.xml` DOCX parts
  - `footnote` / `endnote` element types in `ElementType.cs`, `types.ts`, `SimplePxaSurface.tsx`
  - Rendered in `WordDocumentExporter` with proper reference marks; canvas preview shows note text
  - UI: "Word / DOCX Elements" toolbox group; inspector panel for footnote text
- [x] **[high]** **Bookmarks** — named in-document anchors for cross-references, TOC, and deep links
  - `bookmark` element type in `ElementType.cs`, `types.ts`, `SimplePxaSurface.tsx`
  - Rendered in `WordDocumentExporter` as `<w:bookmarkStart>` / `<w:bookmarkEnd>`
  - `BookmarkName`, `BookmarkTarget` on `ElementDto` / `SimpleElement`
  - UI: toolbox tool; inspector fields for name and link target
- [x] **[high]** **Find-and-Replace** — search across full document content and replace programmatically
  - `FindAndReplaceUseCase.cs` — plain-text, case-insensitive, whole-word, and regex modes
  - `POST /api/document/find-replace` endpoint in `DocumentOpsController.cs`
  - `ExportService.findAndReplace()` — frontend service method
- [x] **[medium]** **Content Controls** — Word structured content controls (rich text, plain text, date picker, combo box, picture)
  - `contentcontrol` element type in `ElementType.cs`, `types.ts`, `SimplePxaSurface.tsx`
  - Rendered in `WordDocumentExporter` as `<w:sdt>` blocks (all five types)
  - `ContentControlType`, `ContentControlTag`, `ContentControlTitle`, `ContentControlPlaceholder`
  - UI: toolbox tool; inspector fields for type, title, tag, placeholder, default content
- [x] **[medium]** **Word-native Comments** — margin annotations with author/date metadata
  - `CommentService.cs` — manages `comments.xml` DOCX part
  - `comment` element type in `ElementType.cs`, `types.ts`, `SimplePxaSurface.tsx`
  - `CommentAuthor`, `CommentDate`, `CommentText`, `CommentId` on `ElementDto`
  - UI: toolbox tool; inspector fields for text, author, date
- [x] **[medium]** **Auto-Hyphenation** — automatic word hyphenation based on locale dictionaries
  - `AutoHyphenation` boolean on `ElementDto` / `SimpleElement`
  - Document-level: `ApplyDocumentAutoHyphenation()` writes `<w:autoHyphenation>` to `settings.xml` when any element opts in
  - Paragraph-level: `<w:suppressAutoHyphens>` injected when `AutoHyphenation == false` on a specific element
- [x] **[medium]** **Page Extraction** — extract specific page ranges into a new standalone document
  - `ExtractPagesUseCase.cs` — accepts 1-based page number list, returns trimmed `DesignExportDto`
  - `POST /api/document/extract-pages` endpoint
  - `ExportService.extractPages()` — frontend service method
- [x] **[medium]** **Content Import Between Documents** — import elements or sections from one document into another
  - Achieved via `POST /api/document/clone` + slice: clone full design then `extract-pages` the desired range
- [x] **[medium]** **Document Cloning** — deep copy of an entire template to a new independent instance
  - `CloneTemplateUseCase.cs` — JSON round-trip clone with fresh IDs
  - `POST /api/document/clone` endpoint
  - `ExportService.cloneDesign()` — frontend service method
- [x] **[medium]** **Custom Document Properties** — user-defined metadata key/value pairs for DMS integration
  - `CustomPropertiesService.cs` — writes `custom.xml` part (text / number / boolean / date)
  - `CustomDocumentPropertyDto` list on `PageSettingsDto` / `CustomDocumentProperty[]` on `PageSettings`
  - UI: Custom Properties section in Page Settings (add/remove/edit name, value, type rows)

---

## Style System

- [x] **[high]** **Named Paragraph Styles** — Heading 1, Heading 2, Normal, Body Text, etc.
  - `StyleDefinitionService.cs` — builds `styles.xml` part from `NamedStyleDto` list
  - `NamedStyleDto` / `NamedStyle` interface with `type = "paragraph"`
  - UI: Named Styles section in Page Settings; `namedStyles` forwarded in all export payloads
- [x] **[high]** **Named Character Styles** — Strong, Emphasis, Code, Hyperlink, etc.
  - Same `StyleDefinitionService` — `type = "character"` emits `<w:rPr>` only
- [x] **[high]** **Style Inheritance & Cascading** — base styles that child styles extend
  - `basedOn` and `nextStyle` fields on `NamedStyleDto` → `<w:basedOn>` / `<w:next>`
  - UI: basedOn / nextStyle inputs per style in the Named Styles panel
- [x] **[medium]** **Named List Styles** — custom bullet and numbering style definitions
  - `type = "list"` mapped to `StyleValues.Numbering` in `StyleDefinitionService`
- [x] **[medium]** **Named Table Styles** — reusable table formatting presets
  - `type = "table"` mapped to `StyleValues.Table` in `StyleDefinitionService`
- [x] **[medium]** **Custom Style Creation** — define, save, and apply project-specific / company-branded styles
  - `namedStyles[]` array on `PageSettings` / `PageSettingsDto` — fully user-defined
  - Elements reference styles via `styleName` (paragraph) and `characterStyle` fields

---

## Still Open

- [ ] Native PDF shading/resource emission in `PXA.Pdf` for importer round-trips
  - Expose page-level shading/resource registration and shading drawing so `PXA.Importer` does not need incremental compatibility patching for grouped or more complex shading preservation.

- [x] **[high]** Read PDF (import existing PDFs as template base)
  - `PdfImporter.cs` — UglyToad.PdfPig; groups words by baseline Y into Text elements, extracts images as base64 data URIs
  - `POST /api/document/import-pdf` — multipart/form-data upload endpoint in `DocumentOpsController.cs`
  - `ExportService.importPdf()` — frontend service method
  - `useTemplateLoader.loadFromPdf()` — navigates to editor after import
  - UI: "Import PDF" button in TemplatePage toolbar with hidden file input
- [x] **[low]** Read DOC — legacy Word 97-2003 binary format
  - `DocImporter.cs` — pure C# CFBF parser; reads WordDocument stream via FIB offsets; falls back to printable-text scanning
  - `POST /api/document/import-doc` endpoint
  - `ExportService.importDoc()` frontend method; TemplatePage "Import file" button accepts `.doc`
- [x] **[medium]** Read DOCX — OOXML Word import
  - `DocxImporter.cs` (PXA.Infrastructure.Word) — OpenXML SDK; page size/margins from `SectionProperties`; paragraphs → Text; tables → Table; inline images → base64 Image elements; typography from RunProperties
  - `POST /api/document/import-docx` endpoint in `DocumentOpsController.cs`
  - `ExportService.importDocx()` frontend method; `useTemplateLoader.loadFromFile()` dispatches `.docx`; TemplatePage accepts `.docx`
- [x] **[low]** Read ODT — OpenDocument Text import
  - `OdtImporter.cs` — reads content.xml from ODF ZIP; parses text:p/text:h with style resolution; draw:frame images
  - `POST /api/document/import-odt` endpoint
  - `ExportService.importOdt()` frontend method; TemplatePage "Import file" button accepts `.odt`
- [x] Full PKI digital signing for DOCX (X.509 / RSA-SHA256 OOXML XML-DSig)
  - `DigitalSigningService.cs` — loads PFX via `X509CertificateLoader.LoadPkcs12`, computes SHA-256 part digests, builds `<Signature>` XML, injects `_xmlsignatures/sig1.xml` into ZIP
  - `POST /api/document/sign-docx` — multipart: `docx` file + `certificate` PFX + optional `password`; returns signed DOCX as octet-stream
  - `ExportService.signDocx(blob, certFile, password?)` — frontend service method
  - `System.Security.Cryptography.Xml` 10.0.8 added to PXA.Infrastructure.Word

---

## UI Additions (this session)

- [x] **Find & Replace modal** (`FindReplaceModal.tsx`) — plain-text, regex, case-sensitive, whole-word; toolbar button in SimplePxaSurface header; applies result via `store.bulkReplaceContent()`
- [x] **ExportModal** — ODT and TIFF added as selectable export formats
- [x] **Word/DOCX inspector section** — per-element paragraph style, character style, auto-hyphenation, revision type/author/date; style dropdowns populated from namedStyles in Page Settings
- [x] **"Import file" button** in TemplatePage — accepts PDF, .doc, and .odt via unified `loadFromFile()` hook
