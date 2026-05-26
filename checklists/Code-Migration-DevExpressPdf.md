# Canvas Migration: DevExpress PDF

## V1 Implementation Status

- [x] V1 scope: deterministic C# source-to-source migration for simple generated PDFs using the DevExpress PdfDocumentProcessor/PdfGraphics API.
- [x] Roslyn-backed migration connected through `Canvas.WebApi` via framework id `DevExpress`.
- [x] Status upgraded from reporting pilot to **full** converter.
- [x] `new PdfDocumentProcessor()` → `var document = new PdfDocument()`.
- [x] `processor.CreateEmptyDocument()` → removed (document created by constructor).
- [x] `using var graphics = processor.CreateGraphics()` → removed (Canvas uses PdfPage surface directly).
- [x] `graphics.DrawString(text, font, brush, x, y)` → deferred, emitted as `page.DrawTextFromTop(text, x, y, fontSize)` after AddPage.
- [x] `graphics.DrawLine(pen, x1, y1, x2, y2)` → deferred, emitted as `page.DrawLine(x1, y1, x2, y2)`.
- [x] `graphics.DrawRectangle(pen, x, y, w, h)` → deferred, emitted as `page.DrawRectangle(x, y, w, h)`.
- [x] `processor.RenderNewPage(...)` → `var page = document.AddPage()` + all deferred draw calls repositioned after it.
- [x] `processor.SaveDocument(path)` → `document.Save(path)`.
- [x] All `DevExpress.*` usings removed; `using Canvas.Pdf;` inserted.
- [x] Existing-PDF processing (`LoadDocument`, `AppendDocument`, `DeletePage`, `InsertPage`) kept with `CANMIGDEVEXP021` warning.
- [x] Forms, signatures, encryption, annotations, bookmarks emit `CANMIGDEVEXP022` warning.
- [x] Report export (`XtraReport`, `ExportToPdf`) emit `CANMIGDEVEXP020` warning.
- [x] WebApi conversion response includes migrated code, diagnostics, and summary counts.
- [ ] V1 does not preserve font size from `DXFont` when the font is passed as an identifier (defaults to 12).
- [ ] V1 does not preserve pen colour or brush colour.
- [ ] V1 does not handle `DrawRectangle(pen, RectangleF)` (2-arg form); only the 5-arg `(pen, x, y, w, h)` form.
- [ ] Future hardening: extract font size from `new DXFont("family", size)` constructors.
- [ ] Future hardening: replace syntax-only matching with semantic matching before broad rollout.

## Package / API Identification

- [x] NuGet packages:
  - [x] `DevExpress.Pdf`
  - [x] `DevExpress.Drawing`
  - [x] `DevExpress.XtraReports.UI` (report export — manual)
- [x] Namespaces removed: `DevExpress.Pdf`, `DevExpress.Drawing`, `DevExpress.XtraReports.UI`
- [x] Classes fully converted:
  - [x] `PdfDocumentProcessor` → `PdfDocument`
  - [x] `CreateEmptyDocument()` → removed
  - [x] `CreateGraphics()` → removed
  - [x] `RenderNewPage(...)` → `AddPage()` + repositioned draw calls
  - [x] `DrawString(text, font, brush, x, y)` → `DrawTextFromTop`
  - [x] `DrawLine(pen, x1, y1, x2, y2)` → `DrawLine`
  - [x] `DrawRectangle(pen, x, y, w, h)` → `DrawRectangle`
  - [x] `SaveDocument(path)` → `Save(path)`
- [ ] Classes kept as-is (manual migration):
  - [ ] `PdfAcroForm` / `PdfFormField`
  - [ ] `PdfSignature` / `PdfDocumentSigner`
  - [ ] `PdfEncryptionOptions`
  - [ ] `PdfAnnotation` / `PdfBookmark`
  - [ ] `XtraReport` / `PdfExportOptions`
  - [ ] `LoadDocument` / `AppendDocument` / `DeletePage` / `InsertPage`

## Key Design Note: Statement Reordering

DevExpress uses an unusual API shape where **draw calls come before `RenderNewPage`**:

```csharp
// DevExpress order: draw → render page
graphics.DrawString(...)   // draw first
processor.RenderNewPage()  // page created after drawing
```

Canvas.Pdf requires **draw calls to come after `AddPage()`**:

```csharp
// Canvas order: add page → draw
var page = document.AddPage();  // page first
page.DrawTextFromTop(...)        // draw after
```

The rewriter handles this by deferring draw calls to a list, then emitting them immediately after the `AddPage()` statement that replaces `RenderNewPage`.

## Roslyn Implementation

- [x] `DevExpressPdfMigration` uses `DevExpressRewriter : CSharpSyntaxRewriter`.
- [x] Pre-scan phase: `FindProcessorVariable`, `FindGraphicsVariable`, `FindSaveTarget`.
- [x] `VisitCompilationUnit` overridden (not `VisitGlobalStatement`) to allow one-to-many statement expansion when `RenderNewPage` is encountered.
- [x] `TransformGlobal`: dispatches each `GlobalStatementSyntax` to the right transformation.
- [x] `TryConvertDrawCall`: converts draw calls on `_graphicsVar` to Canvas calls and defers them in `_deferredDrawCalls`.
- [x] `TryExtractDxFontSize`: extracts font size from `new DXFont("family", size)` constructors.
- [x] `IsCreationDeclaration`: matches `var x = new TypeName(...)` (handles `using var` too).
- [x] `IsDeclarationWithCall`: matches `var x = obj.Method(...)` (used for `CreateGraphics`).
- [x] `IsMethodCallOn`: matches `variable.Method(...)` expression statements.
- [x] `ScanForUnsupportedIdentifiers`: post-rewrite scan for forms/signatures/report-export type names.

## Mapping Table

| DevExpress PDF API / pattern | Canvas.Pdf replacement | Notes |
| --- | --- | --- |
| `using DevExpress.Pdf[.*];` | *(removed)* + `using Canvas.Pdf;` | All DevExpress.* namespaces stripped |
| `new PdfDocumentProcessor()` | `new PdfDocument()` | `using var` keyword also handled |
| `processor.CreateEmptyDocument()` | *(removed)* | Document created by constructor |
| `using var graphics = processor.CreateGraphics()` | *(removed)* | Canvas uses PdfPage surface directly |
| `graphics.DrawString(text, font, brush, x, y)` | `page.DrawTextFromTop(text, x, y, 12)` | Deferred; placed after AddPage; font size from DXFont ctor if available |
| `graphics.DrawLine(pen, x1, y1, x2, y2)` | `page.DrawLine(x1, y1, x2, y2)` | Deferred; placed after AddPage |
| `graphics.DrawRectangle(pen, x, y, w, h)` | `page.DrawRectangle(x, y, w, h)` | Deferred; placed after AddPage |
| `processor.RenderNewPage(paperSize, graphics)` | `var page = document.AddPage();` + draw calls | Draw calls emitted immediately after |
| `processor.SaveDocument(path)` | `document.Save(path)` | Path arg preserved |

## Diagnostic IDs

| ID | Severity | Meaning |
| --- | --- | --- |
| `CANMIGDEVEXP001` | Info | `new PdfDocumentProcessor()` → `new PdfDocument()` |
| `CANMIGDEVEXP002` | Info | `CreateEmptyDocument()` removed |
| `CANMIGDEVEXP003` | Info | `CreateGraphics()` removed |
| `CANMIGDEVEXP004` | Info | `RenderNewPage()` → `document.AddPage()` + draw calls repositioned |
| `CANMIGDEVEXP005` | Info | `DrawString(...)` → `page.DrawTextFromTop(...)` |
| `CANMIGDEVEXP006` | Info | `DrawLine(...)` → `page.DrawLine(...)` |
| `CANMIGDEVEXP007` | Info | `DrawRectangle(...)` → `page.DrawRectangle(...)` |
| `CANMIGDEVEXP008` | Info | `SaveDocument(...)` → `document.Save(...)` |
| `CANMIGDEVEXP020` | Warning | Report export workflow requires manual migration |
| `CANMIGDEVEXP021` | Warning | Existing-PDF processing/page editing APIs are outside v1 |
| `CANMIGDEVEXP022` | Warning | Forms, signatures, encryption, annotations, or bookmarks are outside v1 |

## Unsupported / Manual Follow-Up

- [ ] Font family/colour from `DXFont` / `DXBrush`
- [ ] Pen colour and stroke width
- [ ] `DrawRectangle(pen, RectangleF)` (2-arg form)
- [ ] AcroForms, document merge/split
- [ ] Digital signatures
- [ ] Encryption
- [ ] Printing/rendering workflows
- [ ] Report export (XtraReport → Canvas generation)

## Sample Input

```csharp
using DevExpress.Pdf;
using DevExpress.Drawing;

using var processor = new PdfDocumentProcessor();
processor.CreateEmptyDocument();
using var graphics = processor.CreateGraphics();

// Draw calls happen before RenderNewPage in DevExpress
graphics.DrawString("Invoice #2024", new DXFont("Arial", 18), DXBrushes.Black, 40, 750);
graphics.DrawLine(DXPens.Black, 40, 720, 555, 720);
graphics.DrawString("Thank you for your order.", new DXFont("Arial", 12), DXBrushes.Black, 40, 690);
graphics.DrawRectangle(DXPens.Black, 40, 620, 200, 60);

processor.RenderNewPage(PdfPaperSize.A4, graphics);
processor.SaveDocument(outputPath);
```

## Expected Canvas.Pdf Output

```csharp
using Canvas.Pdf;

var document = new PdfDocument();
var page = document.AddPage();
page.DrawTextFromTop("Invoice #2024", 40, 750, 18);
page.DrawLine(40, 720, 555, 720);
page.DrawTextFromTop("Thank you for your order.", 40, 690, 12);
page.DrawRectangle(40, 620, 200, 60);
document.Save(outputPath);
```

> **Note:** `DXPens.Black` pen argument is dropped — Canvas.Pdf uses default black stroke colour.

## Code Fix Checklist

- [x] Remove `DevExpress.*` usings, add `using Canvas.Pdf;`
- [x] Replace `new PdfDocumentProcessor()` with `new PdfDocument()`
- [x] Remove `processor.CreateEmptyDocument()`
- [x] Remove `using var graphics = processor.CreateGraphics()`
- [x] Convert `graphics.DrawString(text, font, brush, x, y)` → `page.DrawTextFromTop(text, x, y, fontSize)`
- [x] Convert `graphics.DrawLine(pen, x1, y1, x2, y2)` → `page.DrawLine(x1, y1, x2, y2)`
- [x] Convert `graphics.DrawRectangle(pen, x, y, w, h)` → `page.DrawRectangle(x, y, w, h)`
- [x] Replace `processor.RenderNewPage(...)` with `var page = document.AddPage();` + deferred draw calls
- [x] Replace `processor.SaveDocument(path)` with `document.Save(path)`
- [ ] Extract font size from `new DXFont("family", size)` → `fontSize` argument
- [ ] Map pen colour to `strokeColor` argument

## Tests Checklist

- [x] Basic generation workflow → Canvas code (PdfDocument, AddPage, DrawTextFromTop, Save)
- [x] Line and rectangle drawing → Canvas DrawLine/DrawRectangle
- [x] Draw calls repositioned after AddPage (ordering assertion)
- [x] Existing-PDF processing → CANMIGDEVEXP021 warning
- [x] Forms/signatures/encryption → CANMIGDEVEXP022 warning
- [x] Report export workflow → CANMIGDEVEXP020 warning
- [x] WebApi migration-service smoke test
- [ ] Snapshot before/after migration sample
- [ ] Multi-page sample (multiple RenderNewPage calls)
