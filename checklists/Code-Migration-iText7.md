# PXA Migration: iText7

## V1 Implementation Status

- [x] V1 scope: deterministic C# source-to-source migration for simple generated PDFs using iText7.
- [x] Roslyn-backed migration connected through `PXA.WebApi` via framework id `iText7`.
- [x] Status upgraded from pilot to **full** converter.
- [x] Basic document lifecycle: `PdfWriter` + kernel `PdfDocument` + layout `Document` → `PXA.Pdf.PdfDocument`.
- [x] Save targets preserved for simple path/stream writer variables.
- [x] First-page creation including `PageSize.A4`, `PageSize.A3`, `PageSize.LETTER`, and `.Rotate()`.
- [x] `document.Add(new Paragraph(text))` → `page.DrawTextFromTop(text, 40, 40, 12)`.
- [x] `document.Add(new Paragraph(text).SetFontSize(N))` → `page.DrawTextFromTop(text, 40, 40, N)` — font size preserved.
- [x] Fluent Paragraph chains (`.SetBold()`, `.SetFont()`, etc.) unwrapped; text and font size extracted; other styling silently dropped.
- [x] Left-aligned `ShowTextAligned(new Paragraph(text), x, y, LEFT)` → `page.DrawText(text, x, y, 12)`.
- [x] `ShowTextAligned(new Paragraph(text).SetFontSize(N), ...)` → font size preserved.
- [x] `PdfCanvas.MoveTo(...).LineTo(...).Stroke()` → `page.DrawLine(...)`.
- [x] `PdfCanvas.Rectangle(...).Stroke()` → `page.DrawRectangle(..., false)`.
- [x] `PdfCanvas.Rectangle(...).Fill()` → `page.DrawRectangle(..., true)`.
- [x] `PdfCanvas.BeginText().MoveText(...).ShowText(...).EndText()` chain → `page.DrawText(...)`.
- [x] Separated `BeginText; MoveText; ShowText; EndText` sequence → `page.DrawText(...)`.
- [x] `document.Close()` → removed (PXA.Pdf does not require explicit close).
- [x] `document.SetMargins(...)` → removed (PXA.Pdf margins configured differently).
- [x] `PdfCanvas` variable removed when all usages are supported.
- [x] Table, signatures, forms, encryption → warnings for manual migration.
- [x] WebApi conversion response includes migrated code, diagnostics, and summary counts.
- [ ] V1 does not preserve font family, color, stroke width, opacity, leading, or alignment state.
- [ ] V1 does not compile-check output when unsupported iText statements intentionally remain.
- [ ] Future hardening: replace syntax-only matching with semantic matching before broad rollout.

## Package / API Identification

- [x] NuGet packages:
  - [x] `itext7`
  - [x] `itext7.bouncy-castle-adapter`
  - [ ] Other project-specific iText packages
- [x] Common namespaces to detect:
  - [x] `iText.Kernel.Pdf`
  - [x] `iText.Layout`
  - [x] `iText.Layout.Element`
  - [ ] `iText.Layout.Properties`
  - [x] `iText.Signatures`
  - [x] `iText.Forms`
- [x] Common classes to detect:
  - [x] `PdfWriter`
  - [x] `PdfDocument`
  - [x] `Document`
  - [ ] `PageSize`
  - [x] `Paragraph`
  - [x] `Table`
  - [ ] `Image`

## Roslyn Prototype Status

- [x] Add `src/PXA.Migration.Pdf.Code.IText7`
- [x] Add `tests/PXA.Migration.Pdf.Code.IText7.Tests`
- [x] Add projects to `PXA.sln`
- [x] Implement first source migration entry point: `IText7Migration`
- [x] Convert Hello World sample end to end
- [x] Fold `PdfWriter(path)` into final `document.Save(path)`
- [x] Fold kernel `PdfDocument(writer)` into PXA `PdfDocument`
- [x] Convert layout `Document(pdf)` into PXA document plus first page
- [x] Convert simple `document.Add(new Paragraph("..."))`
- [x] Emit warnings for `Table`
- [x] Emit warnings for signatures/forms/security-style APIs
- [x] Map `Document(pdf, PageSize.A4/A3/LETTER)` to PXA page presets
- [x] Map `PageSize.*.Rotate()` to `landscape: true`
- [x] Add realistic invoice-style end-to-end fixture
- [x] Convert simple left-aligned `ShowTextAligned(...)` coordinate text
- [x] Warn for center/right `ShowTextAligned(...)` anchor alignment
- [x] Convert simple kernel `PdfCanvas.MoveTo(...).LineTo(...).Stroke()` line drawing
- [x] Convert simple kernel `PdfCanvas.Rectangle(...).Stroke()/Fill()` rectangle drawing
- [x] Convert simple kernel `PdfCanvas.BeginText().MoveText(...).ShowText(...).EndText()` text drawing
- [x] Convert separated kernel `PdfCanvas` text state sequence: `BeginText(); MoveText(...); ShowText(...); EndText();`
- [x] Remove `PdfCanvas` local variables only when all usages are migrated
- [x] Verified with `dotnet test tests/PXA.Migration.Pdf.Code.IText7.Tests/PXA.Migration.Pdf.Code.IText7.Tests.csproj --no-restore --no-build`: `15/15` passed
- [x] Connect WebApi iText7 converter to the Roslyn migration engine
- [x] Add support for explicit coordinate text via `ShowTextAligned(...)`
- [x] Add support for simple kernel `PdfCanvas` drawing APIs
- [x] Add support for simple chain-style kernel `PdfCanvas` text APIs
- [x] Add support for simple separated-statement kernel `PdfCanvas` text APIs
- [x] Add WebApi migration-service smoke test for iText7 summary/diagnostics
- [x] Add final combined v1 fixture covering page size, positioned text, canvas shapes/text, save, and table warning
- [x] `document.Close()` removal + `CANMIGITEXT016` diagnostic
- [x] `document.SetMargins(...)` removal + `CANMIGITEXT017` diagnostic
- [x] `Paragraph.SetFontSize(N)` font size extraction → `CANMIGITEXT018` diagnostic
- [x] Paragraph fluent chain unwrapping (`.SetBold()`, `.SetFont()`, etc. ignored, text and size extracted)
- [x] `ShowTextAligned` with `SetFontSize` chaining
- [x] Verified with `dotnet test tests/PXA.Migration.Pdf.Code.IText7.Tests`: `20/20` passed
- [x] Verified with `dotnet test tests/PXA.Api.Tests`: `22/22` passed
- [ ] Replace syntax-only matching with semantic matching before broad rollout

## Mapping Table Placeholders

| iText7 API / pattern | PXA.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `new PdfWriter(pathOrStream)` | `document.Save(pathOrStream)` | Automatic | Implemented for simple writer variable chain |
| `new PdfDocument(writer)` | `new PXA.Pdf.PdfDocument()` | Automatic | Implemented when `writer` is a simple local variable |
| `new Document(pdf)` | `var document = new PdfDocument(); var page = document.AddPage();` | Automatic | |
| `new Document(pdf, PageSize.A4)` | `document.AddPage(PdfPagePreset.A4, false)` | Automatic | Supports A4, A3, and Letter |
| `new Document(pdf, PageSize.A4.Rotate())` | `document.AddPage(PdfPagePreset.A4, true)` | Automatic | Landscape flag maps rotated page sizes |
| `document.Close()` | *(removed)* | Automatic | PXA.Pdf does not require explicit close |
| `document.SetMargins(...)` | *(removed)* | Automatic | Configure margins via PXA.Pdf page options |
| `document.Add(new Paragraph(text))` | `page.DrawTextFromTop(text, 40, 40, 12)` | Automatic | Starter fixed position |
| `document.Add(new Paragraph(text).SetFontSize(N))` | `page.DrawTextFromTop(text, 40, 40, N)` | Automatic | Font size extracted from SetFontSize chain |
| `document.Add(new Paragraph(text).SetBold().SetFontSize(N)...)` | `page.DrawTextFromTop(text, 40, 40, N)` | Automatic | Fluent chain unwrapped; SetFontSize preserved; other styling dropped |
| `document.ShowTextAligned(new Paragraph(text), x, y, TextAlignment.LEFT)` | `page.DrawText(text, x, y, 12)` | Automatic | iText explicit coordinates use PDF bottom-left |
| `document.ShowTextAligned(new Paragraph(text).SetFontSize(N), x, y, LEFT)` | `page.DrawText(text, x, y, N)` | Automatic | Font size extracted |
| `document.ShowTextAligned(..., TextAlignment.CENTER/RIGHT)` | Kept + warning | Manual | Anchor-aligned text needs width-aware positioning |
| `canvas.MoveTo(x1, y1).LineTo(x2, y2).Stroke()` | `page.DrawLine(x1, y1, x2, y2, 1)` | Automatic | Requires simple local `PdfCanvas` variable |
| `canvas.Rectangle(x, y, w, h).Stroke()` | `page.DrawRectangle(x, y, w, h, 1, false)` | Automatic | |
| `canvas.Rectangle(x, y, w, h).Fill()` | `page.DrawRectangle(x, y, w, h, 1, true)` | Automatic | |
| `canvas.BeginText().MoveText(x, y).ShowText(text).EndText()` | `page.DrawText(text, x, y, 12)` | Automatic | Chain-style |
| `canvas.BeginText(); canvas.MoveText(x, y); canvas.ShowText(text); canvas.EndText();` | `page.DrawText(text, x, y, 12)` | Automatic | Exact 4-statement sequence |
| `document.Add(new Table(...))` | Kept + warning | Manual | Map after PXA table API review |

## Diagnostic IDs

| ID | Severity | Meaning | Code fix |
| --- | --- | --- | --- |
| `CANMIGITEXT001` | Info | `PdfWriter` construction was folded into PXA save | Yes |
| `CANMIGITEXT002` | Info | Kernel `PdfDocument` construction was folded into PXA document | Yes |
| `CANMIGITEXT003` | Info | Simple `Paragraph` addition was migrated to `DrawTextFromTop` | Yes |
| `CANMIGITEXT004` | Info | Layout `Document` construction was migrated to PXA document | Yes |
| `CANMIGITEXT005` | Warning | `Table` usage requires manual table migration | No |
| `CANMIGITEXT006` | Warning | Signatures, encryption, forms, metadata, or existing-PDF processing are outside v1 scope | No |
| `CANMIGITEXT007` | Info | Writer target was migrated to `document.Save(...)` | Yes |
| `CANMIGITEXT008` | Info | iText7 `PageSize` was migrated to PXA `PdfPagePreset` | Yes |
| `CANMIGITEXT009` | Info | Left-aligned `ShowTextAligned` was migrated to PXA `DrawText` | Yes |
| `CANMIGITEXT010` | Warning | Center/right `ShowTextAligned` anchor alignment needs manual review | No |
| `CANMIGITEXT011` | Info | Simple `PdfCanvas` line was migrated to PXA `DrawLine` | Yes |
| `CANMIGITEXT012` | Info | Simple `PdfCanvas` rectangle was migrated to PXA `DrawRectangle` | Yes |
| `CANMIGITEXT013` | Info | Supported `PdfCanvas` variable was removed after all usages were migrated | Yes |
| `CANMIGITEXT014` | Info | Simple `PdfCanvas` text chain was migrated to PXA `DrawText` | Yes |
| `CANMIGITEXT015` | Info | Separated `PdfCanvas` text state statements were migrated to PXA `DrawText` | Yes |
| `CANMIGITEXT016` | Info | `document.Close()` removed — PXA.Pdf does not require explicit closing | Yes |
| `CANMIGITEXT017` | Info | `document.SetMargins(...)` removed — configure margins via PXA.Pdf page options | Yes |
| `CANMIGITEXT018` | Info | `Paragraph.SetFontSize(N)` mapped to `fontSize` argument in draw call | Yes |

## Unsupported / Manual Follow-Up

- [ ] Tagged PDF
- [ ] Advanced layout renderers
- [ ] Form fields
- [ ] Digital signatures
- [ ] Encryption
- [ ] Custom fonts requiring embedding policy
- [ ] Font and color state migration
- [ ] Stroke width and graphics-state migration
- [ ] Margin/leading/alignment layout-state migration

## Sample Input Snippets

```csharp
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;

using var writer = new PdfWriter(path);
using var pdf = new PdfDocument(writer);
using var document = new Document(pdf);
document.Add(new Paragraph("Hello"));
```

## Expected PXA.Pdf Output Snippets

```csharp
using PXA.Pdf;

var document = new PdfDocument();
var page = document.AddPage();
page.DrawText("Hello", 40, 800);
document.Save(path);
```

## Analyzer Diagnostics Checklist

- [x] Detect iText document construction
- [x] Detect text additions
- [x] Detect explicit positioned text
- [x] Detect simple kernel canvas drawing
- [x] Detect page size usage
- [x] Warn on advanced layout features
- [x] Warn on unsupported PDF features

## Code Fix Checklist

- [x] Replace basic document creation
- [x] Replace basic page creation
- [x] Replace simple paragraph text with starter top-left text drawing
- [x] Replace simple left-aligned `ShowTextAligned(...)`
- [x] Replace simple `PdfCanvas` line and rectangle drawing
- [x] Replace simple chain-style `PdfCanvas` text drawing
- [x] Replace simple separated-statement `PdfCanvas` text drawing
- [x] Add `using PXA.Pdf`
- [x] Preserve writer save target for `document.Save(...)`
- [x] Map simple page size presets
- [x] Remove `document.Close()`
- [x] Remove `document.SetMargins(...)`
- [x] Preserve `Paragraph.SetFontSize(N)` in `DrawTextFromTop` and `DrawText` calls
- [x] Unwrap fluent Paragraph chains to extract text and font size
- [ ] Preserve comments and surrounding code

## Tests Checklist

- [x] Basic document sample
- [x] Explicit page size sample
- [x] Simple text sample
- [x] Explicit positioned text sample
- [x] Center/right positioned text warning sample
- [x] Simple `PdfCanvas` line sample
- [x] Simple `PdfCanvas` rectangle sample
- [x] Simple `PdfCanvas` text chain sample
- [x] Separated `PdfCanvas` text state sample
- [x] Incomplete separated `PdfCanvas` text state keeps canvas variable
- [x] Mixed supported `PdfCanvas` shape/text usages remove canvas variable
- [x] Unsupported `PdfCanvas` usage keeps variable
- [x] Unsupported table/layout diagnostic sample
- [x] Unsupported forms/signatures diagnostic sample
- [x] Realistic invoice-style end-to-end fixture
- [x] Final combined v1 fixture
- [x] WebApi migration-service smoke test
- [x] `document.Close()` removal test
- [x] `document.SetMargins()` removal test
- [x] `Paragraph.SetFontSize(N)` font size extraction test
- [x] Fluent Paragraph chain with mixed styling (SetBold + SetFontSize) test
- [x] Full invoice fixture with all new features combined
- [x] Snapshot before/after migration sample
