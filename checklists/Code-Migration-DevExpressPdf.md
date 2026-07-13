# PXA Migration: DevExpress PDF

## V1 Implementation Status

- [x] V1 scope: deterministic C# source-to-source migration for simple generated PDFs using the DevExpress PdfDocumentProcessor/PdfGraphics API.
- [x] Roslyn-backed migration connected through `PXA.WebApi` via framework id `DevExpress`.
- [x] Status upgraded from reporting pilot to **full** converter.
- [x] `new PdfDocumentProcessor()` → `var document = new PdfDocument()`.
- [x] `processor.CreateEmptyDocument()` → removed (document created by constructor).
- [x] `using var graphics = processor.CreateGraphics()` → removed (PXA uses PdfPage surface directly).
- [x] `graphics.DrawString(text, font, brush, x, y)` → deferred, emitted as `page.DrawTextFromTop(text, x, y, fontSize)` after AddPage.
- [x] `graphics.DrawLine(pen, x1, y1, x2, y2)` → deferred, emitted as `page.DrawLine(x1, y1, x2, y2)`.
- [x] `graphics.DrawRectangle(pen, x, y, w, h)` → deferred, emitted as `page.DrawRectangle(x, y, w, h)`.
- [x] `processor.RenderNewPage(...)` → `var page = document.AddPage()` + all deferred draw calls repositioned after it.
- [x] `processor.SaveDocument(path)` → `document.Save(path)`.
- [x] All `DevExpress.*` usings removed; `using PXA.Pdf;` inserted.
- [x] Existing-PDF processing (`LoadDocument`, `AppendDocument`, `DeletePage`, `InsertPage`) kept with `CANMIGDEVEXP021` warning.
- [x] Forms, signatures, annotations, bookmarks emit `CANMIGDEVEXP022` warning.
- [x] Encryption (`PdfEncryptionOptions`) emits dedicated `CANMIGDEVEXP024` guidance (PXA now supports it via `PdfSaveOptions.Encryption`).
- [x] Report export (`XtraReport`, `ExportToPdf`) emit `CANMIGDEVEXP020` warning.
- [x] WebApi conversion response includes migrated code, diagnostics, and summary counts.
- [x] V2 preserves font size from `DXFont` when the font is passed as an identifier (pre-scan recovery; defaults to 12 only when unknown).
- [x] V2 preserves pen colour and brush colour (mapped to PXA `strokeColor`/`FillColor`).
- [x] V2 handles `DrawRectangle(pen, RectangleF)` (2-arg form) when the rectangle is constructed inline.
- [x] V2 extracts font size from `new DXFont("family", size)` constructors and font-variable declarations.
- [x] V2 uses hybrid semantic matching (local-symbol tracking) with syntactic vendor-name fallback.

## V2 Hardening Plan

Moves the provider from "deterministic single-page V1" to a more robust V2. Work is isolated to
`src/Migrations/PDF/PXA.Migration.Pdf.Code.DevExpress/DevExpressPdfMigration.cs` and its test project.

### 1. Multi-page fix (correctness bug)

- [x] `RenderNewPage` always emitted `var page = document.AddPage();`, so two `RenderNewPage` calls
      produced two `var page` declarations → duplicate-local compile error.
- [x] Fix: add `_pageDeclared` flag — first page emits `var page = document.AddPage();`, subsequent
      pages emit `page = document.AddPage();`. Per-page deferred-draw accumulation already correct.
- [x] Test `Migrate_MultiplePages_ReusesPageVariable`: exactly one `var page`, a second
      `page = document.AddPage();`, each page's draws follow its own `AddPage()`.

### 2. Preserve pen / brush colour

PXA.Pdf supports colour: `DrawLine(..., strokeColor)`, `DrawRectangle(..., strokeColor, fillColor)`,
text colour via `DrawTextFromTop(text, x, topY, PdfDrawTextOptions { FillColor })`.

- [x] New helper `MapDxColor(ExpressionSyntax) -> string?` (returns `null` for default black / unknown):
  - [x] Named `DXBrushes.X` / `DXPens.X` / `DXColor.X` → `Black`→`PdfColor.Black` (null), `White`→`PdfColor.White`,
        `Gray`/`Grey`→`PdfColor.Gray`, `Red`→`PdfColor.RedColor`, `Green`→`PdfColor.GreenColor`, `Blue`→`PdfColor.BlueColor`.
  - [x] `DXColor.FromArgb(r,g,b)` / `FromArgb(a,r,g,b)` → `PdfColor.FromRgb(r,g,b)` (drop alpha).
  - [x] `new DXPen(color, width)` → recurse colour arg + surface width as `lineWidth`.
  - [x] `new DXSolidBrush(color)` → recurse colour arg.
- [x] `DrawLine`: pen `args[0]` → strokeColor/width; longer overload only when non-default.
- [x] `DrawRectangle` (5-arg): pen `args[0]` → strokeColor.
- [x] `DrawString`: brush `args[2]` → `new PdfDrawTextOptions { FontSize = N, FillColor = color }`; keep
      short 4-arg form when colour is default.
- [x] Emit Info `CANMIGDEVEXP009` when a colour is applied.

### 3. Rectangle 2-arg form + font-size from variable

- [x] `DrawRectangle(pen, RectangleF)` (2-arg): inline `new RectangleF(x,y,w,h)` → decompose to normal
      `DrawRectangle(x,y,w,h)`; variable rect → keep + Warning `CANMIGDEVEXP023`.
- [x] Font size from variable: pre-scan `Dictionary<string,string>` of font-local → `DXFont` ctor size;
      `TryExtractDxFontSize` falls back to it when the font arg is an identifier. Default `"12"` when unknown.

### 4. Semantic symbol matching (hybrid)

- [x] Build a `CSharpCompilation` (tree + core references) and obtain a `SemanticModel` in `Migrate`.
- [x] Resolve processor / graphics / page locals to `ISymbol`s; match member-access receivers by
      `SymbolEqualityComparer` instead of by string name. Vendor type/method names stay syntactic.
- [x] Fall back to string-name matching when the symbol is unresolved (never worse than V1).
- [x] Test: reassigned processor variable / `this.`-qualified access still migrates correctly.

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

PXA.Pdf requires **draw calls to come after `AddPage()`**:

```csharp
// PXA order: add page → draw
var page = document.AddPage();  // page first
page.DrawTextFromTop(...)        // draw after
```

The rewriter handles this by deferring draw calls to a list, then emitting them immediately after the `AddPage()` statement that replaces `RenderNewPage`.

## Roslyn Implementation

- [x] `DevExpressPdfMigration` uses `DevExpressRewriter : CSharpSyntaxRewriter`.
- [x] Pre-scan phase: `FindProcessorVariable`, `FindGraphicsVariable`, `FindSaveTarget`.
- [x] `VisitCompilationUnit` overridden (not `VisitGlobalStatement`) to allow one-to-many statement expansion when `RenderNewPage` is encountered.
- [x] `TransformGlobal`: dispatches each `GlobalStatementSyntax` to the right transformation.
- [x] `TryConvertDrawCall`: converts draw calls on `_graphicsVar` to PXA calls and defers them in `_deferredDrawCalls`.
- [x] `TryExtractDxFontSize`: extracts font size from `new DXFont("family", size)` constructors.
- [x] `IsCreationDeclaration`: matches `var x = new TypeName(...)` (handles `using var` too).
- [x] `IsDeclarationWithCall`: matches `var x = obj.Method(...)` (used for `CreateGraphics`).
- [x] `IsMethodCallOn`: matches `variable.Method(...)` expression statements.
- [x] `ScanForUnsupportedIdentifiers`: post-rewrite scan for forms/signatures/report-export type names.

## Mapping Table

| DevExpress PDF API / pattern | PXA.Pdf replacement | Notes |
| --- | --- | --- |
| `using DevExpress.Pdf[.*];` | *(removed)* + `using PXA.Pdf;` | All DevExpress.* namespaces stripped |
| `new PdfDocumentProcessor()` | `new PdfDocument()` | `using var` keyword also handled |
| `processor.CreateEmptyDocument()` | *(removed)* | Document created by constructor |
| `using var graphics = processor.CreateGraphics()` | *(removed)* | PXA uses PdfPage surface directly |
| `graphics.DrawString(text, font, brush, x, y)` | `page.DrawTextFromTop(text, x, y, fontSize)` | Deferred; font size from DXFont ctor or font variable; non-black brush → `PdfDrawTextOptions { FillColor }` |
| `graphics.DrawLine(pen, x1, y1, x2, y2)` | `page.DrawLine(x1, y1, x2, y2)` | Deferred; non-default pen → `(.., lineWidth, strokeColor)` |
| `graphics.DrawRectangle(pen, x, y, w, h)` | `page.DrawRectangle(x, y, w, h)` | Deferred; non-default pen → `(.., lineWidth, false, strokeColor)` |
| `graphics.DrawRectangle(pen, RectangleF)` | `page.DrawRectangle(x, y, w, h)` | Inline `new RectangleF(...)` decomposed; variable → CANMIGDEVEXP023 |
| `processor.RenderNewPage(paperSize, graphics)` | `var page = document.AddPage(<size>);` + draw calls | A4→default, A3/Letter→`PdfPagePreset`, Legal/A5→explicit pts, `(w,h,graphics)`→`AddPage(w,h)`; unmapped→A4 + `CANMIGDEVEXP026` |
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
| `CANMIGDEVEXP009` | Info | Pen/brush colour mapped to PXA colour argument |
| `CANMIGDEVEXP010` | Info | DevExpress encryption mapped to `PdfSaveOptions.Encryption` |
| `CANMIGDEVEXP020` | Warning | Report export workflow requires manual migration |
| `CANMIGDEVEXP021` | Warning | Existing-PDF processing/page editing APIs are outside v1 |
| `CANMIGDEVEXP022` | Warning | Forms, signatures, annotations, or bookmarks are outside v1 |
| `CANMIGDEVEXP023` | Warning | `DrawRectangle(pen, RectangleF)` bounds could not be decomposed |
| `CANMIGDEVEXP024` | Warning | Encryption present but not auto-mappable (no 2-arg `SaveDocument`) — apply `PdfSaveOptions.Encryption` manually |
| `CANMIGDEVEXP025` | Info | `DXFont` declaration removed — font size inlined into `DrawTextFromTop` |
| `CANMIGDEVEXP026` | Warning | `PdfPaperSize.X` has no PXA preset — defaulted to A4 (use `AddPage(width, height)`) |

## Unsupported / Manual Follow-Up

- [ ] Font family from `DXFont` (colour now mapped in V2)
- [x] Pen/brush colour and stroke width (V2)
- [x] `DrawRectangle(pen, RectangleF)` (2-arg form, inline rectangle — V2)
- [ ] AcroForms, document merge/split
- [ ] Digital signatures
- [x] Encryption — PXA.Pdf now supports it (`PdfSaveOptions.Encryption`); migration emits `CANMIGDEVEXP024` guidance
- [ ] Printing/rendering workflows
- [ ] Report export (XtraReport → PXA generation)

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

## Expected PXA.Pdf Output

```csharp
using PXA.Pdf;

var document = new PdfDocument();
var page = document.AddPage();
page.DrawTextFromTop("Invoice #2024", 40, 750, 18);
page.DrawLine(40, 720, 555, 720);
page.DrawTextFromTop("Thank you for your order.", 40, 690, 12);
page.DrawRectangle(40, 620, 200, 60);
document.Save(outputPath);
```

> **Note:** `DXPens.Black` maps to the PXA default stroke colour, so the short-form call is emitted.
> Non-black pens/brushes (e.g. `DXPens.Red`, `DXColor.FromArgb(...)`) are mapped to explicit PXA
> colour arguments in V2.

## Code Fix Checklist

- [x] Remove `DevExpress.*` usings, add `using PXA.Pdf;`
- [x] Replace `new PdfDocumentProcessor()` with `new PdfDocument()`
- [x] Remove `processor.CreateEmptyDocument()`
- [x] Remove `using var graphics = processor.CreateGraphics()`
- [x] Convert `graphics.DrawString(text, font, brush, x, y)` → `page.DrawTextFromTop(text, x, y, fontSize)`
- [x] Convert `graphics.DrawLine(pen, x1, y1, x2, y2)` → `page.DrawLine(x1, y1, x2, y2)`
- [x] Convert `graphics.DrawRectangle(pen, x, y, w, h)` → `page.DrawRectangle(x, y, w, h)`
- [x] Replace `processor.RenderNewPage(...)` with `var page = document.AddPage();` + deferred draw calls
- [x] Replace `processor.SaveDocument(path)` with `document.Save(path)`
- [x] Extract font size from `new DXFont("family", size)` → `fontSize` argument (inline + font-variable pre-scan)
- [x] Map pen colour to `strokeColor` argument

## Tests Checklist

- [x] Basic generation workflow → PXA code (PdfDocument, AddPage, DrawTextFromTop, Save)
- [x] Line and rectangle drawing → PXA DrawLine/DrawRectangle
- [x] Draw calls repositioned after AddPage (ordering assertion)
- [x] Existing-PDF processing → CANMIGDEVEXP021 warning
- [x] Forms/signatures/encryption → CANMIGDEVEXP022 warning
- [x] Report export workflow → CANMIGDEVEXP020 warning
- [x] WebApi migration-service smoke test
- [x] Multi-page sample (multiple RenderNewPage calls)
- [x] Named pen colour → PXA colour argument + CANMIGDEVEXP009
- [x] Default black colour keeps short-form call (regression guard)
- [x] `DXColor.FromArgb(...)` → `PdfColor.FromRgb(...)`
- [x] Brush on `DrawString` → `PdfDrawTextOptions { FillColor }`
- [x] Inline `RectangleF` decomposed; variable `RectangleF` → CANMIGDEVEXP023
- [x] Font passed as variable recovers font size
- [ ] Snapshot before/after migration sample
