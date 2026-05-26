# Canvas Migration: DsPdf / Document Solutions

## V1 Pilot Analysis

- [x] DsPdf / Document Solutions is close to Canvas.Pdf for simple PDF generation: document, page, graphics, text, images, shapes, and save.
- [x] Legacy GrapeCity naming and newer Document Solutions naming must both be accepted during detection.
- [x] Advanced layout, AcroForms, annotations, compliance, security, signatures, redaction, and existing-PDF editing should remain report-only/manual in v1.
- [x] First implementation is a Roslyn-backed reporting pilot, not an automatic code rewrite.

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
  - [x] `Page`
  - [x] `Graphics`
  - [x] `TextFormat`
  - [x] `Image`
  - [x] `TableRenderer`
  - [x] `LayoutHost`
  - [x] `TextLayout`

## Roslyn Prototype Status

- [x] Add `src/Canvas.Migration.DsPdf`
- [x] Add `tests/Canvas.Migration.DsPdf.Tests`
- [x] Add WebApi converter integration
- [x] Add API service smoke test
- [x] Report deterministic candidates while preserving original source
- [x] Warn on unsupported/manual DsPdf feature areas

## Mapping Table

| DsPdf API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `new GcPdfDocument()` | `new Canvas.Pdf.PdfDocument()` | Report-only in v1 | Strong future code-fix candidate. |
| `doc.NewPage()`, `doc.AddPage(...)` | `document.AddPage(...)` | Report-only in v1 | Review page size and orientation. |
| `page.Graphics.DrawString(...)` | `page.DrawText(...)` / `page.DrawTextFromTop(...)` | Report-only in v1 | Review coordinate origin, text layout rectangle, and `TextFormat`. |
| `new TextFormat(...)` | Canvas text parameters | Report-only in v1 | Font family, size, style, and color need mapping. |
| `page.Graphics.DrawImage(...)` | `page.DrawImage(...)` | Report-only in v1 | Review image resource and scaling semantics. |
| `DrawLine(...)`, `DrawRectangle(...)`, `FillRectangle(...)`, `DrawEllipse(...)`, `DrawPolygon(...)`, `DrawPath(...)` | Canvas shape/path drawing | Report-only in v1 | Geometry and stroke/fill styles need review. |
| `doc.Save(...)` | `document.Save(...)` | Report-only in v1 | Review save options and stream/path overloads. |

## Unsupported / Manual Follow-Up

- [x] Advanced text layout
- [x] Complex table rendering
- [x] AcroForms
- [x] Annotations
- [x] PDF/A and compliance options
- [x] Redaction
- [x] Signature APIs
- [x] Security/encryption
- [x] Existing PDF editing, page import, and document merge

## Analyzer Diagnostics

| Diagnostic | Severity | Meaning |
| --- | --- | --- |
| `CANMIGDSPDF001` | Info | `GcPdfDocument` construction detected. |
| `CANMIGDSPDF002` | Info | Page creation detected. |
| `CANMIGDSPDF003` | Info | Text drawing candidate detected. |
| `CANMIGDSPDF004` | Info | `TextFormat` usage detected. |
| `CANMIGDSPDF005` | Info | Image drawing candidate detected. |
| `CANMIGDSPDF006` | Info | Shape/path drawing candidate detected. |
| `CANMIGDSPDF007` | Info | Save/export target detected. |
| `CANMIGDSPDF020` | Warning | Advanced layout/table APIs need manual migration. |
| `CANMIGDSPDF021` | Warning | Existing-PDF editing, page import, or merge APIs need manual migration. |
| `CANMIGDSPDF022` | Warning | Compliance, security, signature, or redaction APIs need manual migration. |
| `CANMIGDSPDF023` | Warning | Forms, annotations, layout, PDF/A, signatures, security, or redaction identifiers detected. |

## Sample Input Snippets

```csharp
using GrapeCity.Documents.Pdf;
using GrapeCity.Documents.Drawing;

var document = new GcPdfDocument();
var page = document.NewPage();
page.Graphics.DrawString("Hello", new TextFormat(), new PointF(40, 40));
page.Graphics.DrawImage(image, new RectangleF(40, 120, 200, 80));
page.Graphics.DrawRectangle(pen, new RectangleF(40, 620, 200, 80));
document.Save(path);
```

## Expected Canvas.Pdf Output Snippets

```csharp
// Canvas.Pdf migration report: DsPdf / Document Solutions
// - new GcPdfDocument(...) detected. Candidate Canvas rewrite starts with `var document = new PdfDocument();`.
// - NewPage(...) detected. Candidate Canvas rewrite is `var page = document.AddPage(...)` after page size/orientation review.
// - TextFormat detected. Map font family, size, style, and color to Canvas text parameters where possible.
// - DrawString(...) detected. Candidate Canvas rewrite is `page.DrawText(...)` or `page.DrawTextFromTop(...)` after coordinate/layout review.
// - DrawImage(...) detected. Candidate Canvas rewrite is `page.DrawImage(...)` after image sizing/resource review.
// - DrawRectangle(...) detected. Candidate Canvas rewrite is a Canvas shape/path drawing call after geometry review.
// - Save(...) detected. Candidate Canvas rewrite ends with `document.Save(...)`; review DsPdf save options.
```

## Analyzer Diagnostics Checklist

- [x] Detect DsPdf/GcPdf package generation
- [x] Detect document creation
- [x] Detect page creation
- [x] Detect graphics text calls
- [x] Detect graphics image calls
- [x] Detect graphics shape calls
- [x] Warn on compliance/security APIs
- [x] Warn on forms/annotations APIs
- [x] Warn on complex layout APIs
- [x] Warn on existing-PDF editing/import/merge APIs

## Code Fix Checklist

- [ ] Replace basic document creation
- [ ] Replace page creation
- [ ] Replace simple text drawing
- [ ] Add `using Canvas.Pdf`
- [ ] Report coordinate-system assumptions
- [ ] Preserve unsupported calls with diagnostics

## Tests Checklist

- [x] Basic document/page/text/save sample
- [x] Image and shape drawing sample
- [x] Advanced layout/table diagnostic sample
- [x] Existing-PDF editing diagnostic sample
- [x] Forms/compliance/security/redaction diagnostic sample
- [x] WebApi smoke test
- [ ] Snapshot before/after migration sample
- [ ] Real package identification sample from a customer repository
