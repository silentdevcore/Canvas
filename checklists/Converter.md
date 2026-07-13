# Export Converters — Implementation Checklist

> **Goal:** Let users export any PXA design to HTML, XML, Word (.docx), Excel (.xlsx),
> Image (PNG / JPEG / SVG), CSV, and Markdown — in addition to the existing PDF and JSON.
> The frontend sends the same `DesignExportDto` payload to a single new endpoint; the backend
> dispatches it to a format-specific converter and streams the file back.

---

## Architecture Overview

```
Frontend (ExportService.ts)
  └── POST /api/templates/export?format=html
        └── ExportController (PXA.WebApi)
              └── ExportDocumentUseCase (PXA.Application)
                    └── IDocumentExporter  ←  one implementation per format
                          ├── HtmlDocumentExporter     (PXA.Infrastructure.Converters)
                          ├── XmlDocumentExporter      (PXA.Infrastructure.Converters)
                          ├── WordDocumentExporter     (PXA.Infrastructure.Word)
                          ├── ExcelDocumentExporter    (PXA.Infrastructure.Sheet)
                          ├── ImageDocumentExporter    (PXA.Infrastructure.Converters)
                          ├── SvgDocumentExporter      (PXA.Infrastructure.Converters)
                          ├── CsvDocumentExporter      (PXA.Infrastructure.Converters)
                          └── MarkdownDocumentExporter (PXA.Infrastructure.Converters)
```

**Input** (already exists): `DesignExportDto` in `PXA.WebApi/Infrastructure/DesignExportDto.cs`
**Existing PDF path**: `POST /api/templates/render-design` → keep unchanged for backward compat
**New path**: `POST /api/templates/export?format=<key>` → returns file with correct MIME type

---

## Phase 1 — Shared Contract (PXA.Core)

- [x] Add `IDocumentExporter` interface to `PXA.Core/Abstractions/`:
  ```csharp
  public interface IDocumentExporter
  {
      string FormatKey { get; }          // "html", "xml", "word", "excel", "png", "svg", "csv", "md"
      string MimeType  { get; }          // "text/html", "application/vnd.openxmlformats..."
      string FileExtension { get; }      // ".html", ".docx", etc.
      byte[] Export(DesignExportDto design);
  }
  ```
- [x] Add `ExportFormat` enum (or string constants) to `PXA.Core/Primitives/` for type-safe dispatch
- [x] Add `ExportCapabilities` record to `IRendererCapabilities` or a new `IExporterCapabilities`
  - Fields: `SupportsMultiPage`, `SupportsImages`, `SupportsRichText`, `SupportsFormFields`

> **Note:** `DesignExportDto` was moved from `PXA.WebApi.Infrastructure` →
> `PXA.Core.Contracts` so all infrastructure projects can access it. The old file in
> PXA.WebApi now has a `global using PXA.Core.Contracts;` for backward compatibility.
> New fields added: `Href`, `LinkTarget`, `ButtonAction`, `NumberValue/Style/Currency/Locale`,
> `ListStyle`, `ArrowDirection`, `ArrowRotation`.

---

## Phase 2 — Application Layer (PXA.Application)

- [x] Create `ExportDocumentRequest` in `PXA.Application/UseCases/`:
  ```csharp
  public record ExportDocumentRequest(DesignExportDto Design, string Format);
  ```
- [x] Create `ExportDocumentUseCase` that:
  - Receives `IEnumerable<IDocumentExporter>` via DI (registered per format)
  - Resolves the right exporter by `FormatKey`
  - Throws `NotSupportedException` for unknown formats with a clear message listing supported ones
  - Returns `ExportResult { byte[] Data, string MimeType, string FileName }`
- [x] Register all exporters in `Program.cs` via `services.AddScoped<IDocumentExporter, HtmlDocumentExporter>()` etc.

---

## Phase 3 — API Endpoint (PXA.WebApi)

- [x] Add `ExportController` at `PXA.WebApi/Controllers/ExportController.cs`:
  ```
  POST /api/export?format={key}
  Body: DesignExportDto
  Returns: File (binary) with correct Content-Disposition header
  ```
- [x] Accept `format` as query param — default to `"pdf"` if omitted
- [x] Return `415 Unsupported Media Type` when the format key is unknown, with a JSON list of supported formats
- [x] Add `GET /api/export/formats` → returns list of `{ key, label, mimeType, extension, supportsMultiPage }` for the frontend to build the export menu dynamically
- [x] Wire `ExportDocumentUseCase` in DI alongside existing use cases in `Program.cs`

---

## Phase 4 — Format Converters

### 4.1 HTML  (`PXA.Infrastructure.Converters`)
- [x] `HtmlDocumentExporter : IDocumentExporter` — FormatKey `"html"`
- [x] Walk each `ElementDto` and emit positioned `<div>` / `<span>` elements with inline CSS
  - `text` → `<div style="position:absolute; left:{x}px; top:{y}px; ...">content</div>`
  - `image` → `<img src="{src}" style="object-fit:{fitMode}...">`
  - `table` → `<table>` with `<thead>` / `<tbody>`, zebra rows via nth-child CSS
  - `rect` / `circle` / `shape` → `<div>` with border-radius
  - `line` → `<div>` styled with width + rotation
  - `richtext` → emit `htmlContent` verbatim inside a container `<div>`
  - `qrcode` → placeholder `<div>` with data-qr-value attribute (let client render with JS)
  - `link` → `<a href="{href}" target="{target}">`
  - `button` → `<a href="{buttonAction}" class="btn">`
  - All others → comment `<!-- {type} not yet supported in HTML export -->`
- [x] Wrap page in `<div class="canvas-page" style="width:{w}px; height:{h}px; position:relative">`
- [x] Emit a minimal embedded `<style>` block (reset, page container, font fallbacks)
- [x] Multi-page: each page in its own `.canvas-page` div, separated by a page-break style

### 4.2 XML  (`PXA.Infrastructure.Converters`)
- [x] `XmlDocumentExporter : IDocumentExporter` — FormatKey `"xml"`
- [x] Use `System.Xml.Linq` (built-in, no new package)
- [x] Schema:
  ```xml
  <PxaDocument name="…" version="1.0">
    <PageSettings width="595" height="842" orientation="portrait" />
    <Pages>
      <Page id="page-1" index="0">
        <Elements>
          <Element id="…" type="Text" x="…" y="…" width="…" height="…">
            <Content>Hello</Content>
            <Style fontSize="16" color="#111827" fontWeight="bold" />
          </Element>
          …
        </Elements>
      </Page>
    </Pages>
  </PxaDocument>
  ```
- [x] Map every element type to its XML representation — properties go as child elements or attributes
- [x] Include `XmlDeclaration` (`<?xml version="1.0" encoding="utf-8"?>`)
- [x] Return UTF-8 bytes with MIME `application/xml`

### 4.3 Word (.docx)  (`PXA.Infrastructure.Word`)
- [x] Add NuGet: **`DocumentFormat.OpenXml`** (OpenXML SDK — MIT, no runtime cost)
- [x] `WordDocumentExporter : IDocumentExporter` — FormatKey `"word"`
- [x] Build a `WordprocessingDocument` in a `MemoryStream`
- [x] Map PXA elements → Word constructs:
  - `text` / `richtext` → `<w:p><w:r><w:t>` paragraph with run properties (bold, italic, font size, color)
  - `table` → `<w:tbl>` with rows/cells; header row → `<w:trPr><w:tblHeader/>`
  - `signature` → signature line paragraph with underline styling + label
  - `pagenumber` → Word field `{ PAGE }` / `{ NUMPAGES }` via `SimpleField`
  - `link` → `HyperlinkRelationship` + `<w:hyperlink>`
  - `checkbox` / `field` → inline text with label + underscore placeholder
  - Unsupported (arrow, draw, chart, qrcode) → descriptive paragraph in italic
- [x] Set document metadata (title, author) from `PageSettings.Metadata`
- [x] Return MIME `application/vnd.openxmlformats-officedocument.wordprocessingml.document`

### 4.4 Excel (.xlsx)  (`PXA.Infrastructure.Sheet`)
- [x] Add NuGet: **`ClosedXML`** (MIT) — simpler API than raw OpenXML for spreadsheets
- [x] `ExcelDocumentExporter : IDocumentExporter` — FormatKey `"excel"`
- [x] Strategy: each PXA `table` element → dedicated worksheet; other text elements → first sheet summary
- [x] For each `table` element:
  - Sheet name = element `name` ?? `"Table {n}"`
  - `cellData[row][col]` → cell value; apply header row bold + background; zebra rows via pattern fill
  - `columnWidths` → `ws.Column(i).Width = colWidth / 7.5` (approx px→Excel width)
  - `columnAlignments` → `cell.Style.Alignment.Horizontal`
- [x] For non-table content (text blocks) → "Summary" sheet with element type + content in two columns
- [x] `optionlist` / `dropdown` → sheet column with all options listed
- [x] Return MIME `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`

### 4.5 PNG / JPEG  (`PXA.Infrastructure.Converters`)
- [x] Add NuGet: **`SkiaSharp`** (MIT; platform-native, no Chrome needed)
- [x] `ImageDocumentExporter : IDocumentExporter` — FormatKey `"png"` (and `JpegDocumentExporter` for `"jpeg"`)
- [x] For each page: create `SKBitmap` at `(width × dpi/72, height × dpi/72)` (default 150 dpi)
  - `text` → `SKPaint` + `SKCanvas.DrawText` with font size × scale
  - `rect` / `shape` / `circle` → `SKCanvas.DrawRect` / `DrawOval` with fill + stroke
  - `line` → `SKCanvas.DrawLine`
  - `image` → decode base64 data URL → `SKCanvas.DrawImage`
  - `table` → iterate cellData, draw cells as rects + text
  - `arrow` → draw line + filled triangle tip
  - All others → bounding-box placeholder rect with label text
- [x] Multi-page: zip all page images into a single `.zip` file, MIME `application/zip`
- [x] Single-page: return raw PNG/JPEG bytes

### 4.6 SVG  (`PXA.Infrastructure.Converters`)
- [x] `SvgDocumentExporter : IDocumentExporter` — FormatKey `"svg"`
- [x] Use `System.Xml.Linq` (no new package)
- [x] Emit one `<svg viewBox="0 0 {w} {h}">` per page; multi-page → zip
- [x] Map elements:
  - `text` → `<text x y font-size fill>`
  - `rect` / `shape` → `<rect x y width height rx fill stroke>`
  - `circle` → `<ellipse cx cy rx ry fill stroke>`
  - `line` → `<line x1 y1 x2 y2 stroke>`
  - `image` → `<image x y width height href="{src}" preserveAspectRatio>`
  - `richtext` → `<foreignObject>` with embedded HTML
  - `table` → `<g>` of rects + text elements per cell
  - `link` → `<a href>` wrapping child element

### 4.7 CSV  (`PXA.Infrastructure.Converters`)
- [x] `CsvDocumentExporter : IDocumentExporter` — FormatKey `"csv"`
- [x] Target: tabular data only — each `table` element → its own CSV section
- [x] Each table prefixed with `# Table: {name}` comment row
- [x] `cellData[row][col]` → RFC 4180 CSV (quote fields containing comma/quote/newline)
- [x] Non-table elements → single "Metadata" section at top: `type, x, y, content`
- [x] Return UTF-8 with BOM (`\xEF\xBB\xBF`) so Excel opens it correctly
- [x] MIME `text/csv`; filename `{designName}.csv`

### 4.8 Markdown  (`PXA.Infrastructure.Converters`)
- [x] `MarkdownDocumentExporter : IDocumentExporter` — FormatKey `"md"`
- [x] Walk elements sorted by Y position (top-to-bottom reading order)
- [x] Map:
  - `text` with large fontSize (≥ 24) → `## Heading`; medium (≥ 18) → `### Heading`
  - `text` normal → paragraph
  - `richtext` → strip HTML tags, keep bold/italic via regex → Markdown equivalents
  - `table` → GFM pipe table `| col | col |` with header separator row
  - `image` → `![alt](src)`
  - `link` → `[text](href)`
  - `optionlist` ordered → `1. item`; unordered → `- item` (respect `listStyle`)
  - `checkbox` → `- [x] label` or `- [ ] label`
  - `line` / `pageboundary` → `---`
  - `note` → `> **title**\n> body` (blockquote)
  - `button` → `[label](buttonAction)` if action set, else `**label**`
  - Others → `<!-- {type}: {content} -->`
- [x] Pages separated by `---\n<!-- Page {n} -->\n---`
- [x] MIME `text/markdown`; filename `{designName}.md`

---

## Phase 5 — Frontend Updates

### 5.1 ExportService.ts
- [x] Add `ExportFormat` union type:
  ```ts
  export type ExportFormat = 'pdf' | 'json' | 'html' | 'xml' | 'word' | 'excel' | 'png' | 'jpeg' | 'svg' | 'csv' | 'md';
  ```
- [x] Add `static async exportViaBackend(format, template, pages, sharedElements, pageSettings, onProgress)`:
  - Builds payload (reuses existing `pages`/`sharedElements` mapping)
  - `POST /api/export?format={format}`
  - Streams response as blob → triggers browser download with correct extension
  - Handles `415` error: throws descriptive error with list of supported formats
- [x] Keep `exportToJSON` and `exportToPDF` as-is (backward compat)
- [x] Add `static async listSupportedFormats(): Promise<FormatInfo[]>`:
  - `GET /api/export/formats` → cached in module-level variable (call once)

### 5.2 Export Modal UI
- [x] Create `ExportModal.tsx` component in `src/components/Editor/`:
  - Trigger: "More formats…" entry in the existing export dropdown in `LivePreview.tsx`
  - Layout: grouped list of format cards (Documents / Data / Images / Text)
  - Each card: icon + format name + one-line description + "Export" button
  - Show a spinner + progress text while waiting for backend response
  - Show error state (red card border + retry button) if export fails
  - Show "Done!" confirmation state for 2.5 s after success
- [x] Format cards open `ExportModal` from `LivePreview`'s export dropdown

### 5.3 Editor Integration
- [x] "More formats…" button added to the existing export dropdown in `LivePreview.tsx`
- [x] Persist last-used format in `localStorage` so the modal reopens on the right format

---

## Phase 6 — DI Registration (Program.cs)

- [x] Register `ExportDocumentUseCase` as scoped
- [x] Register each `IDocumentExporter` implementation as scoped:
  ```csharp
  services.AddScoped<IDocumentExporter, HtmlDocumentExporter>();
  services.AddScoped<IDocumentExporter, XmlDocumentExporter>();
  services.AddScoped<IDocumentExporter, WordDocumentExporter>();
  services.AddScoped<IDocumentExporter, ExcelDocumentExporter>();
  services.AddScoped<IDocumentExporter, ImageDocumentExporter>();
  services.AddScoped<IDocumentExporter, JpegDocumentExporter>();
  services.AddScoped<IDocumentExporter, SvgDocumentExporter>();
  services.AddScoped<IDocumentExporter, CsvDocumentExporter>();
  services.AddScoped<IDocumentExporter, MarkdownDocumentExporter>();
  ```
- [x] Add project references in `PXA.WebApi/PXA.WebApi.csproj`:
  - `PXA.Infrastructure.Word` ✓
  - `PXA.Infrastructure.Sheet` ✓
  - `PXA.Infrastructure.Converters` ✓
- [x] Add NuGet packages:
  - [x] `DocumentFormat.OpenXml` 3.5.1 → `PXA.Infrastructure.Word`
  - [x] `ClosedXML` 0.105.0 → `PXA.Infrastructure.Sheet`
  - [x] `SkiaSharp` 3.119.2 → `PXA.Infrastructure.Converters`

---

## Phase 7 — Testing

- [x] Unit test each exporter with a minimal 1-page `DesignExportDto` (one text element + one table)
  - Assert byte array is non-empty
  - Assert MIME type matches
  - For XML/HTML/SVG/CSV/MD: parse output and assert key content is present
- [x] `ExportDocumentUseCaseTests`: resolves by key, case-insensitive, unknown throws, null throws, all 9 formats, capabilities, filename from design name
- [x] Integration tests (`PXA.Api.Tests`) — 15 tests: POST html/xml/csv/md/word/excel/png → 200, unknown → 415, formats list → 9 entries with capability fields
- [ ] Frontend: manual smoke test for each format card — download file, open in target app

---

## Format × Feature Support Matrix

| Feature              | PDF | HTML | XML | Word | Excel | PNG | SVG | CSV | MD |
|----------------------|:---:|:----:|:---:|:----:|:-----:|:---:|:---:|:---:|:--:|
| Multi-page           | ✓   | ✓    | ✓   | ✓    | ✓     | zip | zip | ✓   | ✓  |
| Images               | ✓   | ✓    | ref | ✓    | ✓     | ✓   | ✓   | —   | ref|
| Tables               | ✓   | ✓    | ✓   | ✓    | ✓     | ✓   | ✓   | ✓   | ✓  |
| Rich text            | ✓   | ✓    | ✓   | ✓    | —     | ✓   | ~   | —   | ~  |
| Form fields          | ✓   | ✓    | ✓   | ✓    | —     | ✓   | —   | —   | ~  |
| Links / buttons      | ✓   | ✓    | ref | ✓    | ✓     | —   | ✓   | —   | ✓  |
| Watermark            | ✓   | ~    | ✓   | ✓    | —     | ✓   | ✓   | —   | —  |
| Charts               | ✓   | js   | ✓   | ~    | ✓     | ✓   | ~   | ✓   | —  |
| Arrow / Draw         | ✓   | ~    | ✓   | —    | —     | ✓   | ✓   | —   | —  |
| Page numbering       | ✓   | js   | ✓   | ✓    | —     | ✓   | —   | —   | —  |

`✓` full support · `~` partial/approximation · `ref` reference only · `—` not applicable · `js` client-side JS needed · `zip` multi-page as zip archive

---

## File Map — New Files Created

| File | Project | Status |
|------|---------|--------|
| `PXA.Core/Contracts/DesignExportDto.cs` | PXA.Core | ✅ done |
| `PXA.Core/Abstractions/IDocumentExporter.cs` | PXA.Core | ✅ done |
| `PXA.Core/Primitives/ExportFormat.cs` | PXA.Core | ✅ done |
| `PXA.Application/UseCases/ExportDocumentUseCase.cs` | PXA.Application | ✅ done |
| `PXA.Application/UseCases/ExportDocumentRequest.cs` | PXA.Application | ✅ done |
| `PXA.WebApi/Controllers/ExportController.cs` | PXA.WebApi | ✅ done |
| `PXA.Infrastructure.Converters/HtmlDocumentExporter.cs` | PXA.Infrastructure.Converters | ✅ done |
| `PXA.Infrastructure.Converters/XmlDocumentExporter.cs` | PXA.Infrastructure.Converters | ✅ done |
| `PXA.Infrastructure.Converters/ImageDocumentExporter.cs` | PXA.Infrastructure.Converters | ✅ done |
| `PXA.Infrastructure.Converters/SvgDocumentExporter.cs` | PXA.Infrastructure.Converters | ✅ done |
| `PXA.Infrastructure.Converters/CsvDocumentExporter.cs` | PXA.Infrastructure.Converters | ✅ done |
| `PXA.Infrastructure.Converters/MarkdownDocumentExporter.cs` | PXA.Infrastructure.Converters | ✅ done |
| `PXA.Infrastructure.Converters/StyleExtensions.cs` | PXA.Infrastructure.Converters | ✅ done |
| `PXA.Infrastructure.Word/WordDocumentExporter.cs` | PXA.Infrastructure.Word | ✅ done |
| `PXA.Infrastructure.Word/StyleExtensions.cs` | PXA.Infrastructure.Word | ✅ done |
| `PXA.Infrastructure.Sheet/ExcelDocumentExporter.cs` | PXA.Infrastructure.Sheet | ✅ done |
| `PXA.Infrastructure.Sheet/StyleExtensions.cs` | PXA.Infrastructure.Sheet | ✅ done |
| `pxa-designer/src/components/Editor/ExportModal.tsx` | Frontend | ✅ done |

---

## Files Modified

| File | Change | Status |
|------|--------|--------|
| `PXA.WebApi/Program.cs` | Registered `ExportDocumentUseCase` + 9 exporters in DI | ✅ done |
| `PXA.WebApi/PXA.WebApi.csproj` | Added project references to Word, Sheet, Converters | ✅ done |
| `PXA.WebApi/Infrastructure/DesignExportDto.cs` | Replaced with `global using PXA.Core.Contracts` | ✅ done |
| `PXA.Infrastructure.Word/PXA.Infrastructure.Word.csproj` | Added `DocumentFormat.OpenXml` 3.5.1 | ✅ done |
| `PXA.Infrastructure.Sheet/PXA.Infrastructure.Sheet.csproj` | Added `ClosedXML` 0.105.0 | ✅ done |
| `PXA.Infrastructure.Converters/PXA.Infrastructure.Converters.csproj` | Added `SkiaSharp` 3.119.2 | ✅ done |
| `pxa-designer/src/services/ExportService.ts` | Added `ExportFormat` type, `exportViaBackend`, `listSupportedFormats` | ✅ done |
| `pxa-designer/src/components/Preview/LivePreview.tsx` | Added "More formats…" entry + `ExportModal` integration | ✅ done |
| `pxa-designer/src/styles/index.css` | Added `.export-modal-*` styles | ✅ done |

---

## Remaining / Future Work

- [x] `ExportCapabilities` record on `IExporterCapabilities` (Phase 1 item)
- [x] Persist last-used export format in `localStorage`
- [x] Unit tests (Phase 7) — 41 tests passing in `PXA.Export.Tests`
- [x] Integration tests — 15 tests in `PXA.Api.Tests` (HTTP 200, formats JSON, 415, case-insensitive key, capabilities fields)
- [x] Image exporter: fetch remote URLs via `HttpClient` (`HttpImageCache` with in-process cache)
- [x] JPEG quality / PNG DPI as optional query params (`?dpi=300&quality=85`) — threaded through `ExportOptions`
- [x] Word: embed images via `ImagePart` + DrawingML (data URLs + remote URLs via HttpClient)
- [x] HTML: include QR code rendering script (`qrcode.js` CDN + inline init) when design contains qrcode elements
