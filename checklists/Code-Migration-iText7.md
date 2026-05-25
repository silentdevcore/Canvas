# Canvas Migration: iText7

## V1 Pilot Analysis

- [x] V1 scope is limited to deterministic C# source-to-source migration for simple generated PDFs.
- [x] Roslyn-backed migration is connected through `Canvas.WebApi` via framework id `iText7`.
- [x] Basic document lifecycle is covered: `PdfWriter` + kernel `PdfDocument` + layout `Document` becomes `Canvas.Pdf.PdfDocument`.
- [x] Save targets are preserved for simple path/stream writer variables.
- [x] First-page creation is covered, including `PageSize.A4`, `PageSize.A3`, `PageSize.LETTER`, and `.Rotate()`.
- [x] Simple flowing text is migrated from `document.Add(new Paragraph(...))` to `DrawTextFromTop`.
- [x] Simple coordinate text is migrated from left-aligned `ShowTextAligned(...)` to `DrawText`.
- [x] Simple `PdfCanvas` line, rectangle, filled rectangle, chain text, and separated text-state statements are migrated.
- [x] Unsupported table/layout/security/form/signature usage produces diagnostics for manual follow-up.
- [x] WebApi conversion response includes migrated code, diagnostics, and summary counts.
- [ ] V1 does not preserve iText font, color, stroke width, opacity, margins, leading, or alignment state.
- [ ] V1 does not compile-check output when unsupported iText statements intentionally remain for manual migration.
- [ ] Future hardening: preserve vendor usings when unsupported vendor statements remain, or wrap unsupported remnants in a report-only block.
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

- [x] Add `src/Canvas.Migration.iText7`
- [x] Add `tests/Canvas.Migration.iText7.Tests`
- [x] Add projects to `Canvas.sln`
- [x] Implement first source migration entry point: `IText7Migration`
- [x] Convert Hello World sample end to end
- [x] Fold `PdfWriter(path)` into final `document.Save(path)`
- [x] Fold kernel `PdfDocument(writer)` into Canvas `PdfDocument`
- [x] Convert layout `Document(pdf)` into Canvas document plus first page
- [x] Convert simple `document.Add(new Paragraph("..."))`
- [x] Emit warnings for `Table`
- [x] Emit warnings for signatures/forms/security-style APIs
- [x] Map `Document(pdf, PageSize.A4/A3/LETTER)` to Canvas page presets
- [x] Map `PageSize.*.Rotate()` to `landscape: true`
- [x] Add realistic invoice-style end-to-end fixture
- [x] Convert simple left-aligned `ShowTextAligned(...)` coordinate text
- [x] Warn for center/right `ShowTextAligned(...)` anchor alignment
- [x] Convert simple kernel `PdfCanvas.MoveTo(...).LineTo(...).Stroke()` line drawing
- [x] Convert simple kernel `PdfCanvas.Rectangle(...).Stroke()/Fill()` rectangle drawing
- [x] Convert simple kernel `PdfCanvas.BeginText().MoveText(...).ShowText(...).EndText()` text drawing
- [x] Convert separated kernel `PdfCanvas` text state sequence: `BeginText(); MoveText(...); ShowText(...); EndText();`
- [x] Remove `PdfCanvas` local variables only when all usages are migrated
- [x] Verified with `dotnet test tests/Canvas.Migration.iText7.Tests/Canvas.Migration.iText7.Tests.csproj --no-restore --no-build`: `15/15` passed
- [x] Connect WebApi iText7 converter to the Roslyn migration engine
- [x] Add support for explicit coordinate text via `ShowTextAligned(...)`
- [x] Add support for simple kernel `PdfCanvas` drawing APIs
- [x] Add support for simple chain-style kernel `PdfCanvas` text APIs
- [x] Add support for simple separated-statement kernel `PdfCanvas` text APIs
- [x] Add WebApi migration-service smoke test for iText7 summary/diagnostics
- [x] Add final combined v1 fixture covering page size, positioned text, canvas shapes/text, save, and table warning
- [x] Verified with `dotnet test tests/Canvas.Api.Tests/Canvas.Api.Tests.csproj --no-restore --no-build`: `16/16` passed
- [ ] Replace syntax-only matching with semantic matching before broad rollout

## Mapping Table Placeholders

| iText7 API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `new PdfWriter(pathOrStream)` | `document.Save(pathOrStream)` | Code fix candidate | Implemented for simple writer variable chain |
| `new PdfDocument(writer)` | `new Canvas.Pdf.PdfDocument()` | Code fix candidate | Implemented when `writer` is a simple local variable |
| `new Document(pdf)` | `var document = new PdfDocument(); var page = document.AddPage();` | Code fix candidate | Implemented for simple local variable chain |
| `new Document(pdf, PageSize.A4)` | `document.AddPage(PdfPagePreset.A4, false)` | Code fix candidate | Supports A4, A3, and Letter |
| `new Document(pdf, PageSize.A4.Rotate())` | `document.AddPage(PdfPagePreset.A4, true)` | Code fix candidate | Landscape flag maps rotated page sizes |
| `document.Add(new Paragraph(text))` | `page.DrawTextFromTop(text, 40, 40, 12)` | Code fix candidate | Uses starter fixed position until flow layout exists |
| `document.ShowTextAligned(new Paragraph(text), x, y, TextAlignment.LEFT)` | `page.DrawText(text, x, y, 12)` | Code fix candidate | iText explicit coordinates use PDF bottom-left coordinates |
| `document.ShowTextAligned(..., TextAlignment.CENTER/RIGHT)` | Keep and warn | Manual | Anchor-aligned text needs width-aware positioning review |
| `canvas.MoveTo(x1, y1).LineTo(x2, y2).Stroke()` | `page.DrawLine(x1, y1, x2, y2, 1)` | Code fix candidate | Requires simple local `PdfCanvas` variable |
| `canvas.Rectangle(x, y, w, h).Stroke()` | `page.DrawRectangle(x, y, w, h, 1, false)` | Code fix candidate | Uses iText bottom-left coordinates directly |
| `canvas.Rectangle(x, y, w, h).Fill()` | `page.DrawRectangle(x, y, w, h, 1, true)` | Code fix candidate | Fill color defaults to Canvas black until color state mapping exists |
| `canvas.BeginText().MoveText(x, y).ShowText(text).EndText()` | `page.DrawText(text, x, y, 12)` | Code fix candidate | Supports chain-style text state only |
| `canvas.BeginText(); canvas.MoveText(x, y); canvas.ShowText(text); canvas.EndText();` | `page.DrawText(text, x, y, 12)` | Code fix candidate | Supports exact four-statement sequence only |
| `document.Add(new Table(...))` | Canvas table API | Manual | Map after table API review |

## Diagnostic IDs

| ID | Severity | Meaning | Code fix |
| --- | --- | --- | --- |
| `CANMIGITEXT001` | Info | `PdfWriter` construction was folded into Canvas save | Yes |
| `CANMIGITEXT002` | Info | Kernel `PdfDocument` construction was folded into Canvas document | Yes |
| `CANMIGITEXT003` | Info | Simple `Paragraph` addition was migrated to `DrawTextFromTop` | Yes |
| `CANMIGITEXT004` | Info | Layout `Document` construction was migrated to Canvas document | Yes |
| `CANMIGITEXT005` | Warning | `Table` usage requires manual table migration | No |
| `CANMIGITEXT006` | Warning | Signatures, encryption, forms, metadata, or existing-PDF processing are outside v1 scope | No |
| `CANMIGITEXT007` | Info | Writer target was migrated to `document.Save(...)` | Yes |
| `CANMIGITEXT008` | Info | iText7 `PageSize` was migrated to Canvas `PdfPagePreset` | Yes |
| `CANMIGITEXT009` | Info | Left-aligned `ShowTextAligned` was migrated to Canvas `DrawText` | Yes |
| `CANMIGITEXT010` | Warning | Center/right `ShowTextAligned` anchor alignment needs manual review | No |
| `CANMIGITEXT011` | Info | Simple `PdfCanvas` line was migrated to Canvas `DrawLine` | Yes |
| `CANMIGITEXT012` | Info | Simple `PdfCanvas` rectangle was migrated to Canvas `DrawRectangle` | Yes |
| `CANMIGITEXT013` | Info | Supported `PdfCanvas` variable was removed after all usages were migrated | Yes |
| `CANMIGITEXT014` | Info | Simple `PdfCanvas` text chain was migrated to Canvas `DrawText` | Yes |
| `CANMIGITEXT015` | Info | Separated `PdfCanvas` text state statements were migrated to Canvas `DrawText` | Yes |

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

## Expected Canvas.Pdf Output Snippets

```csharp
using Canvas.Pdf;

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
- [x] Add `using Canvas.Pdf`
- [x] Preserve writer save target for `document.Save(...)`
- [x] Map simple page size presets
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
- [x] Snapshot before/after migration sample
