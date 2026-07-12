# PXA Migration: DsPdf / Document Solutions

## V1 Implementation Status

- [x] V1 scope: deterministic C# source-to-source migration for simple generated PDFs using DsPdf/GcPdf.
- [x] Roslyn-backed migration connected through `PXA.WebApi` via framework id `DsPdf`.
- [x] Status upgraded from pilot to **full** converter.
- [x] Basic document lifecycle: `new GcPdfDocument()` → `new PdfDocument()`.
- [x] `doc.NewPage()` / `doc.AddPage()` → `document.AddPage()`.
- [x] `doc.Save(path)` → `document.Save(path)`.
- [x] `page.Graphics.DrawString(text, new TextFormat { FontSize = N }, new PointF(x, y))` → `page.DrawTextFromTop(text, x, y, N)`.
- [x] `page.Graphics.DrawString(text, new TextFormat(), new PointF(x, y))` → `page.DrawTextFromTop(text, x, y, 12)` (default font size).
- [x] `page.Graphics.DrawLine(pen, x1, y1, x2, y2)` → `page.DrawLineFromTop(x1, y1, x2, y2)`.
- [x] `page.Graphics.DrawLine(pen, new PointF(x1,y1), new PointF(x2,y2))` → `page.DrawLineFromTop(x1, y1, x2, y2)`.
- [x] `page.Graphics.DrawRectangle(pen, new RectangleF(x, y, w, h))` → `page.DrawRectangleFromTop(x, y, w, h)`.
- [x] `page.Graphics.FillRectangle(brush, new RectangleF(x, y, w, h))` → `page.DrawRectangleFromTop(x, y, w, h, 1, true)`.
- [x] All `DS.Documents.*` / `GrapeCity.Documents.*` usings removed; `using PXA.Pdf;` added.
- [x] `DrawImage` — kept with warning (out of v1 scope).
- [x] `DrawEllipse`, `DrawPolygon`, `DrawPath` — kept with warnings.
- [x] Existing-PDF editing (`Load`, `DeletePage`, `MergeWithDocument`, etc.) → warnings.
- [x] Compliance/security APIs (`Sign`, `Encrypt`, `SaveAsPdfA`, `Redact`, etc.) → warnings.
- [x] Advanced layout identifiers (`TableRenderer`, `LayoutHost`, `TextLayout`, `AcroForm`, etc.) → warnings.
- [ ] V1 does not preserve font family, color, or stroke width.
- [ ] V1 does not handle RectangleF-based DrawString layout rectangles beyond origin extraction.
- [ ] Future hardening: replace syntax-only matching with semantic matching before broad rollout.

## Package / API Identification

- [x] NuGet packages:
  - [x] `DS.Documents.Pdf`
  - [x] Legacy `GrapeCity.Documents.Pdf`
- [x] Common namespaces to detect:
  - [x] `DS.Documents.Pdf`
  - [x] `DS.Documents.Drawing`
  - [x] Legacy `GrapeCity.Documents.Pdf`
  - [x] Legacy `GrapeCity.Documents.Drawing`
  - [x] Legacy `GrapeCity.Documents.Layout`
- [x] Common classes to detect:
  - [x] `GcPdfDocument`
  - [x] `TextFormat`
  - [x] `PointF`
  - [x] `RectangleF`
  - [x] `TableRenderer`
  - [x] `LayoutHost`
  - [x] `TextLayout`

## Roslyn Prototype Status

- [x] Add `src/PXA.Migration.DsPdf`
- [x] Add `tests/PXA.Migration.DsPdf.Tests`
- [x] Add projects to `PXA.sln`
- [x] Implement source migration entry point: `DsPdfMigration` as a real `CSharpSyntaxRewriter`
- [x] Pre-scan phase: find document variable (from `new GcPdfDocument()`), page variables (from `doc.NewPage()/AddPage()`), save target
- [x] Convert `GcPdfDocument` construction
- [x] Convert `NewPage()/AddPage()` page creation
- [x] Convert `page.Graphics.DrawString(...)` with PointF position and optional TextFormat FontSize
- [x] Convert `page.Graphics.DrawLine(...)` — 5-arg and PointF forms
- [x] Convert `page.Graphics.DrawRectangle(...)` — RectangleF and 5-arg forms
- [x] Convert `page.Graphics.FillRectangle(...)` — RectangleF and 5-arg forms (fill: true)
- [x] Warn and keep `DrawImage`
- [x] Warn and keep `DrawEllipse/DrawPolygon/DrawPath`
- [x] Warn and keep existing-PDF editing APIs
- [x] Warn and keep compliance/security APIs
- [x] Scan for unsupported identifiers (AcroForm, TableRenderer, etc.)
- [x] Remove DS.Documents.*/GrapeCity.Documents.* usings, add `using PXA.Pdf;`
- [x] Connect WebApi DsPdf converter to the Roslyn migration engine
- [x] Verified with `dotnet test tests/PXA.Migration.DsPdf.Tests`: `10/10` passed
- [x] Verified with `dotnet test tests/PXA.Api.Tests`: `22/22` passed

## Mapping Table

| DsPdf API / pattern | PXA.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `new GcPdfDocument()` | `new PXA.Pdf.PdfDocument()` | Automatic | |
| `doc.NewPage()` / `doc.AddPage()` | `document.AddPage()` | Automatic | |
| `page.Graphics.DrawString(text, new TextFormat(), new PointF(x, y))` | `page.DrawTextFromTop(text, x, y, 12)` | Automatic | Default font size 12 |
| `page.Graphics.DrawString(text, new TextFormat { FontSize = N }, new PointF(x, y))` | `page.DrawTextFromTop(text, x, y, N)` | Automatic | FontSize extracted from initializer |
| `page.Graphics.DrawLine(pen, x1, y1, x2, y2)` | `page.DrawLineFromTop(x1, y1, x2, y2)` | Automatic | |
| `page.Graphics.DrawLine(pen, new PointF(x1,y1), new PointF(x2,y2))` | `page.DrawLineFromTop(x1, y1, x2, y2)` | Automatic | |
| `page.Graphics.DrawRectangle(pen, new RectangleF(x, y, w, h))` | `page.DrawRectangleFromTop(x, y, w, h)` | Automatic | |
| `page.Graphics.FillRectangle(brush, new RectangleF(x, y, w, h))` | `page.DrawRectangleFromTop(x, y, w, h, 1, true)` | Automatic | |
| `doc.Save(path)` | `document.Save(path)` | Automatic | |
| `page.Graphics.DrawImage(...)` | Kept + warning | Manual | Out of v1 scope |
| `DrawEllipse/DrawPolygon/DrawPath` | Kept + warning | Manual | Out of v1 scope |
| Existing-PDF editing APIs | Kept + warning | Manual | Load, Delete, Merge, Import |
| Compliance/security APIs | Kept + warning | Manual | Sign, Encrypt, SaveAsPdfA, Redact |

## Diagnostic IDs

| ID | Severity | Meaning | Code fix |
| --- | --- | --- | --- |
| `CANMIGDSPDF001` | Info | `GcPdfDocument` → `PdfDocument` | Yes |
| `CANMIGDSPDF002` | Info | `doc.NewPage()/AddPage()` → `document.AddPage()` | Yes |
| `CANMIGDSPDF003` | Info | `page.Graphics.DrawString(...)` → `page.DrawTextFromTop(...)` | Yes |
| `CANMIGDSPDF005` | Warning | `DrawImage` requires manual migration | No |
| `CANMIGDSPDF006` | Info/Warning | Line/rectangle/shape drawing converted or flagged | Info for supported; Warning for unsupported |
| `CANMIGDSPDF007` | Info | `doc.Save(path)` → `document.Save(path)` | Yes |
| `CANMIGDSPDF021` | Warning | Existing-PDF editing/page import/merge outside v1 | No |
| `CANMIGDSPDF022` | Warning | Compliance, security, signature, or redaction outside v1 | No |
| `CANMIGDSPDF023` | Warning | Advanced forms/layout/annotations identifiers detected | No |

## Unsupported / Manual Follow-Up

- [ ] Font family and color mapping
- [ ] Stroke width from pen/brush parameters
- [ ] DrawImage
- [ ] DrawEllipse, DrawPolygon, DrawPath
- [ ] AcroForms
- [ ] Annotations
- [ ] PDF/A and compliance options
- [ ] Redaction
- [ ] Signature APIs
- [ ] Security/encryption
- [ ] Existing PDF editing, page import, and document merge

## Sample Input Snippets

```csharp
using GrapeCity.Documents.Pdf;
using GrapeCity.Documents.Drawing;

var doc = new GcPdfDocument();
var page = doc.NewPage();
page.Graphics.DrawString("Invoice #2024", new TextFormat { FontSize = 18 }, new PointF(72, 72));
page.Graphics.DrawLine(pen, 72, 100, 540, 100);
page.Graphics.DrawRectangle(pen, new RectangleF(72, 200, 468, 300));
doc.Save(outputPath);
```

## Expected PXA.Pdf Output Snippets

```csharp
using PXA.Pdf;

var document = new PdfDocument();
var page = document.AddPage();
page.DrawTextFromTop("Invoice #2024", 72, 72, 18);
page.DrawLineFromTop(72, 100, 540, 100);
page.DrawRectangleFromTop(72, 200, 468, 300);
document.Save(outputPath);
```

## Tests Checklist

- [x] Basic document/page/text/save sample
- [x] Font size extraction from TextFormat initializer
- [x] DrawLine — 5-arg form
- [x] DrawLine — PointF form
- [x] DrawRectangle and FillRectangle
- [x] DrawImage warning
- [x] Advanced layout/table warning
- [x] Existing-PDF editing warning
- [x] Forms/compliance/security warning
- [x] Realistic invoice end-to-end fixture
- [x] WebApi migration-service smoke test
